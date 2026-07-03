using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class FinalPage : Page, WOLe.Provisioner.Models.IWizardPage
    {
        public FinalPage()
        {
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var cfg = ProvisioningState.Current ?? new ProvisioningConfig();
            cfg.Step3Completed = true;
            ProvisioningState.Update(cfg);
            WizardService.GoToWelcome();
        }

        public bool ValidateAndSave()
        {
            // Nothing to validate on the final page
            return true;
        }
    }
}
