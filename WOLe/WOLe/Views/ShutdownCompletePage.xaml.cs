using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ShutdownCompletePage : Page, IWizardPage
    {
        public ShutdownCompletePage()
        {
            this.InitializeComponent();
        }

        public bool ValidateAndSave()
        {
            return true;
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            var cfg = ProvisioningState.Current ?? new ProvisioningConfig();
            cfg.Step4Completed = true;
            ProvisioningState.Update(cfg);
            WizardService.GoToWelcome();
        }
    }
}
