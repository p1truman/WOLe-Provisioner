using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class SinricPage : Page, IWizardPage
    {
        public SinricPage()
        {
            InitializeComponent();

            var cfg = ProvisioningState.Current;

            AppKeyTextBox.Text = cfg.SinricAppKey ?? "";
            AppSecretTextBox.Text = cfg.SinricAppSecret ?? "";
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(AppKeyTextBox.Text) ||
                string.IsNullOrWhiteSpace(AppSecretTextBox.Text))
            {
                ErrorText.Text = "Both App Key and App Secret are required.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            var cfg = ProvisioningState.Current;

            cfg.SinricAppKey = AppKeyTextBox.Text.Trim();
            cfg.SinricAppSecret = AppSecretTextBox.Text.Trim();

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}
