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

            // Boxes are ALWAYS visible — user must always be able to
            // confirm or enter IP and port regardless of prior steps.
            IpBox.Visibility   = Visibility.Visible;
            PortBox.Visibility = Visibility.Visible;

            var cfg = ProvisioningState.Current;

            // Pre-populate IP if available — priority:
            //   1. ShutdownPcIp (forwarded from Optimise in flow 2)
            //   2. Pcs[0].IpAddress (forwarded from Optimise in flow 1, or prior save)
            //   3. Blank — user types it in manually
            string ip = "";

            if (!string.IsNullOrWhiteSpace(cfg.ShutdownPcIp))
                ip = cfg.ShutdownPcIp;
            else if (cfg.Pcs != null && cfg.Pcs.Count > 0 &&
                     !string.IsNullOrWhiteSpace(cfg.Pcs[0].IpAddress))
                ip = cfg.Pcs[0].IpAddress;

            IpBox.Text = ip;

            // Port always defaults to 5050 unless a value was previously saved.
            int port = 5050;
            if (cfg.ShutdownPcPort.HasValue && cfg.ShutdownPcPort.Value > 0)
                port = cfg.ShutdownPcPort.Value;
            else if (cfg.Pcs != null && cfg.Pcs.Count > 0 && cfg.Pcs[0].Port > 0)
                port = cfg.Pcs[0].Port;

            PortBox.Text = port.ToString();
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var cfg = ProvisioningState.Current;

            string ip       = IpBox.Text.Trim();
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

            // Save into both locations so all downstream consumers find the value.
            cfg.ShutdownPcIp   = ip;
            cfg.ShutdownPcPort = port;

            cfg.Pcs ??= new System.Collections.Generic.List<PcConfig>();
            if (cfg.Pcs.Count == 0)
                cfg.Pcs.Add(new PcConfig());

            cfg.Pcs[0].IpAddress = ip;
            cfg.Pcs[0].Port      = port;

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}