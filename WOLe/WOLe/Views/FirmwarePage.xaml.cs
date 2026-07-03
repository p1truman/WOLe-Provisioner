using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Services;
using WOLe.Provisioner.Models;
using Microsoft.UI; // for Colors
using Microsoft.UI.Xaml.Media;
using System;

namespace WOLe.Provisioner.Views
{
    public sealed partial class FirmwarePage : Page, IWizardPage
    {
        private readonly FirmwareBuilder _builder = new();

        public FirmwarePage()
        {
            InitializeComponent();
            ProvisioningState.Ensure();
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

        private void GenerateFirmware_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Visibility = Visibility.Visible;

            if (!IsProvisioningComplete())
            {
                StatusText.Text = "Provisioning is not complete. Please fill in all required fields.";
                StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["ConfigErrorBrush"];
                return;
            }

            try
            {
                var path = _builder.BuildFirmware(ProvisioningState.Current);
                OutputPathTextBox.Text = path;

                StatusText.Text = "Firmware generated successfully.";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
            }
            catch (Exception ex)
            {
                StatusText.Text = $"ERROR: {ex.Message}";
                StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["ConfigErrorBrush"];
            }
        }

        public bool ValidateAndSave()
        {
            if (string.IsNullOrWhiteSpace(OutputPathTextBox.Text))
            {
                StatusText.Text = "Firmware must be generated before proceeding.";
                StatusText.Foreground = (SolidColorBrush)Application.Current.Resources["ConfigErrorBrush"];
                StatusText.Visibility = Visibility.Visible;
                return false;
            }

            return true;
        }
    }
}
