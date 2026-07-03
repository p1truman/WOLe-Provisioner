using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WOLe.Provisioner.Views
{
    public sealed partial class WifiPage : Page, IWizardPage
    {
        public WifiPage()
        {
            InitializeComponent();

            var cfg = ProvisioningState.Current;

            SsidTextBox.Text = cfg.WifiSsid ?? "";
            PasswordBox.Password = cfg.WifiPassword ?? "";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Use the wizard's back navigation system
            WizardService.GoBack();
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(SsidTextBox.Text) ||
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ErrorText.Text = "Please enter both SSID and password.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            var cfg = ProvisioningState.Current;

            cfg.WifiSsid = SsidTextBox.Text.Trim();
            cfg.WifiPassword = PasswordBox.Password.Trim();

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}
