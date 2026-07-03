using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ShutdownPcConfigPage : Page, IWizardPage
    {
        public ShutdownPcConfigPage()
        {
            InitializeComponent();

            var cfg = ProvisioningState.Current;

            // PC1 ONLY — show and load IP/Port
            if (cfg.Pcs != null && cfg.Pcs.Count > 0)
            {
                IpBox.Visibility = Visibility.Visible;
                PortBox.Visibility = Visibility.Visible;

                IpBox.Text = cfg.Pcs[0].IpAddress ?? "";
                PortBox.Text = cfg.Pcs[0].Port.ToString();
            }
            else
            {
                // Other PCs — hide fields
                IpBox.Visibility = Visibility.Collapsed;
                PortBox.Visibility = Visibility.Collapsed;
            }
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var cfg = ProvisioningState.Current;

            // Only PC1 has editable IP/Port
            if (cfg.Pcs != null && cfg.Pcs.Count > 0)
            {
                string ip = IpBox.Text.Trim();
                string portText = PortBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(ip))
                {
                    ErrorText.Text = "IP address is required.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                if (!int.TryParse(portText, out int port) || port < 1 || port > 65535)
                {
                    ErrorText.Text = "Port must be between 1 and 65535.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                cfg.Pcs[0].IpAddress = ip;
                cfg.Pcs[0].Port = port;
            }

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}
