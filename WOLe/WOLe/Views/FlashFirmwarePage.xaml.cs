using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace WOLe.Provisioner.Views
{
    public sealed partial class FlashFirmwarePage : Page, IWizardPage
    {
        private readonly FirmwareBuilder _builder = new();
        private readonly FlashService _flasher = new();

        private readonly IProgress<string> _progress;
        private Timer _portTimer;

        private bool _flashSuccessful = false;

        public FlashFirmwarePage()
        {
            InitializeComponent();

            ProvisioningState.Ensure();

            _progress = new Progress<string>(msg =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    LogTextBox.Text += msg + Environment.NewLine;
                    LogScroll?.ChangeView(null, double.MaxValue, null);
                });
            });

            _portTimer = new Timer(_ => DispatcherQueue.TryEnqueue(LoadPorts), null, 300, 1000);
        }

        private bool IsProvisioningComplete()
        {
            var cfg = ProvisioningState.Current;
            if (cfg == null)
                return false;

            return !string.IsNullOrWhiteSpace(cfg.WifiSsid)
                && !string.IsNullOrWhiteSpace(cfg.WifiPassword)
                && !string.IsNullOrWhiteSpace(cfg.SinricAppKey)
                && !string.IsNullOrWhiteSpace(cfg.SinricAppSecret)
                && !string.IsNullOrWhiteSpace(cfg.ShutdownSecret)
                && cfg.Pcs != null
                && cfg.Pcs.Count > 0;
        }

        private void LoadPorts()
        {
            var ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                PortCombo.Items.Clear();
                PortCombo.PlaceholderText = "No COM ports detected";
                return;
            }

            var existing = PortCombo.Items.Select(i => i.ToString()).ToArray();
            if (!ports.SequenceEqual(existing))
            {
                PortCombo.Items.Clear();
                foreach (var port in ports)
                    PortCombo.Items.Add(port);

                PortCombo.SelectedIndex = 0;
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPorts();
        }

        private async void FlashButton_Click(object sender, RoutedEventArgs e)
        {
            _flashSuccessful = false;
            ErrorText.Visibility = Visibility.Collapsed;

            LogTextBox.Text = "";
            Progress.Visibility = Visibility.Visible;
            Progress.IsIndeterminate = false;
            Progress.Value = 0;

            if (PortCombo.SelectedItem is null)
            {
                ShowError("ERROR: No COM port selected.");
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            ProvisioningState.Ensure();

            if (!IsProvisioningComplete())
            {
                ShowError("ERROR: Provisioning is not complete.");
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            var cfg = ProvisioningState.Current;

            Log("Building firmware...");
            string sketchPath;

            try
            {
                sketchPath = _builder.BuildFirmware(cfg);
                Log($"Firmware built: {sketchPath}");
            }
            catch (Exception ex)
            {
                ShowError($"ERROR building firmware: {ex.Message}");
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            var port = PortCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(port))
            {
                ShowError("ERROR: No COM port selected.");
                Progress.Visibility = Visibility.Collapsed;
                return;
            }

            Log($"Flashing to {port}...");

            try
            {
                Progress.Value = 30;
                await _flasher.FlashAsync(sketchPath, port, _progress);
                Progress.Value = 100;

                Log("Flash complete!");
                _flashSuccessful = true;
            }
            catch (Exception ex)
            {
                ShowError($"ERROR flashing firmware: {ex.Message}");
                Progress.Visibility = Visibility.Collapsed;
            }
        }

        private void Log(string msg)
        {
            LogTextBox.Text += msg + Environment.NewLine;
            LogScroll?.ChangeView(null, double.MaxValue, null);
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        public bool ValidateAndSave()
        {
            if (!_flashSuccessful)
            {
                ShowError("You must flash the firmware before proceeding.");
                return false;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            return true;
        }
    }
}
