using WOLe.Provisioner.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ShutdownSecretPage : Page, IWizardPage
    {
        public ShutdownSecretPage()
        {
            InitializeComponent();

            var cfg = ProvisioningState.Current;

            if (App.CurrentMode != WizardMode.ShutdownOnly)
            {
                HeaderText.Text = "WOL-e Device Secret";
                DescriptionText.Text = "Enter a secret key your WOL-e device will use for action authentication.";
            }
            else
            {
                HeaderText.Text = "Enhanced Actions Secret";
                DescriptionText.Text = "Enter the secret key this PC will use for enhanced action authentication.\n\nThis must match the secret key configured during WOL-e device setup.";
            }

            SecretBox.Text = cfg.ShutdownSecret ?? "";
        }

        public bool ValidateAndSave()
        {
            // Hide previous errors
            ErrorText.Visibility = Visibility.Collapsed;

            // Validation: Secret is required
            if (string.IsNullOrWhiteSpace(SecretBox.Text))
            {
                ErrorText.Text = "You must enter your shutdown secret to continue.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            // Save
            var cfg = ProvisioningState.Current;
            cfg.ShutdownSecret = SecretBox.Text.Trim();
            ProvisioningState.Update(cfg);

            return true;
        }
    }
}
