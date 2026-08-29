using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using System.IO;
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
                    // Strip any line that contains the temp firmware folder path
                    if (!ContainsSensitivePath(msg))
                    {
                        LogTextBox.Text += msg + Environment.NewLine;
                        LogScroll?.ChangeView(null, double.MaxValue, null);
                    }
                });
            });

            _portTimer = new Timer(_ => DispatcherQueue.TryEnqueue(LoadPorts), null, 300, 1000);
        }

        // Returns true if a log line contains the firmware temp folder path
        // so it can be suppressed from the visible log output.
        private static bool ContainsSensitivePath(string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return false;

            var tempFirmwareRoot = Path.Combine(Path.GetTempPath(), "WOL-e");
            return msg.IndexOf(tempFirmwareRoot, StringComparison.OrdinalIgnoreCase) >= 0;
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

            FlashButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;

            if (PortCombo.SelectedItem is null)
            {
                ShowError("ERROR: No COM port selected.");
                Progress.Visibility = Visibility.Collapsed;
                FlashButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                return;
            }

            ProvisioningState.Ensure();

            if (!IsProvisioningComplete())
            {
                ShowError("ERROR: Provisioning is not complete.");
                Progress.Visibility = Visibility.Collapsed;
                FlashButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                return;
            }

            var cfg = ProvisioningState.Current;

            // ----------------------------------------------------------------
            //  STEP 1 — Build firmware silently (path is never shown in the log)
            // ----------------------------------------------------------------
            Log("Building firmware...");
            string sketchPath;

            try
            {
                sketchPath = _builder.BuildFirmware(cfg);
                // Deliberately do NOT log sketchPath — location is kept hidden
                Log("Firmware built successfully.");
            }
            catch (Exception ex)
            {
                ShowError($"ERROR building firmware: {ex.Message}");
                Progress.Visibility = Visibility.Collapsed;
                FlashButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
                return;
            }

            Progress.Value = 20;

            // ----------------------------------------------------------------
            //  STEP 2 — Flash to device
            // ----------------------------------------------------------------
            var port = PortCombo.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(port))
            {
                ShowError("ERROR: No COM port selected.");
                Progress.Visibility = Visibility.Collapsed;
                FlashButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
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

                // ----------------------------------------------------------------
                //  STEP 3 — Clean up temp firmware folder after successful flash
                // ----------------------------------------------------------------
                CleanupFirmwareFolder(sketchPath);
            }
            catch (Exception ex)
            {
                ShowError($"ERROR flashing firmware: {ex.Message}");
                Progress.Visibility = Visibility.Collapsed;
            }
            finally
            {
                FlashButton.IsEnabled = true;
                RefreshButton.IsEnabled = true;
            }
        }

        private void CleanupFirmwareFolder(string sketchPath)
        {
            try
            {
                // Delete the sketch folder (e.g. ...\Temp\WOL-e\WOL-e_Firmware\)
                var sketchFolder = Path.GetDirectoryName(sketchPath);
                if (!string.IsNullOrEmpty(sketchFolder) && Directory.Exists(sketchFolder))
                {
                    Directory.Delete(sketchFolder, true);
                    Log("Firmware files cleaned up.");
                }

                // Also delete the parent WOL-e temp folder if it is now empty
                var parentFolder = Path.Combine(Path.GetTempPath(), "WOL-e");
                if (Directory.Exists(parentFolder) &&
                    Directory.GetFileSystemEntries(parentFolder).Length == 0)
                {
                    Directory.Delete(parentFolder, false);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal — log without exposing paths
                Log($"WARN: Could not clean up firmware files: {ex.Message}");
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