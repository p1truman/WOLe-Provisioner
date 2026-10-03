using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class SystemConfigPageOne : Page, IWizardPage
    {
        public SystemConfigPageOne()
        {
            InitializeComponent();

            // Flow marker text
            FlowSubtitleText.Text = App.CurrentMode == WizardMode.FullWoleSetup
                ? "To begin, youll need to complete the BIOS configuration steps and Network Provisioning below. "
                : "If you've previously attented to the steps below or require lauch app mapping reassignment, click next.";

            // Subscribe to the provisioning control's events.
            // When the user clicks Optimise and a static IP is successfully applied,
            // forward the values into ProvisioningState for the appropriate flow.
            UnifiedProvisioningControl.ProvisionedIpChanged  += OnProvisionedIpChanged;
            UnifiedProvisioningControl.ProvisionedMacChanged += OnProvisionedMacChanged;
        }

        private void OnProvisionedIpChanged(string ip)
        {
            var cfg = ProvisioningState.Current;

            if (App.CurrentMode == WizardMode.FullWoleSetup)
            {
                // Step 1 flow — forward IP into PC1 credentials for PcConfigPage
                cfg.Pcs ??= new List<PcConfig>();
                if (cfg.Pcs.Count == 0)
                    cfg.Pcs.Add(new PcConfig());

                cfg.Pcs[0].IpAddress = ip;
            }
            else
            {
                // Step 4 flow — forward IP into ShutdownPcIp for ShutdownPcConfigPage
                cfg.ShutdownPcIp = ip;
            }

            ProvisioningState.Update(cfg);
        }

        private void OnProvisionedMacChanged(string mac)
        {
            // MAC is only relevant in the Step 1 (FullWoleSetup) flow —
            // the enhanced actions installer does not need a MAC address.
            if (App.CurrentMode != WizardMode.FullWoleSetup)
                return;

            var cfg = ProvisioningState.Current;

            cfg.Pcs ??= new List<PcConfig>();
            if (cfg.Pcs.Count == 0)
                cfg.Pcs.Add(new PcConfig());

            cfg.Pcs[0].MacAddress = mac;

            ProvisioningState.Update(cfg);
        }

        public bool ValidateAndSave()
        {
            return true;
        }
    }
}