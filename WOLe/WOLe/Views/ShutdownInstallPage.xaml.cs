using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using System;
using System.Threading.Tasks;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ShutdownInstallPage : Page, IWizardPage
    {
        private bool _installCompleted = false;
        private bool _skipSelected = false;

        public ShutdownInstallPage()
        {
            InitializeComponent();

            if (App.CurrentMode == WizardMode.FullWoleSetup)
            {
                SkipButton.Visibility = Visibility.Visible;

                HeaderText.Text = "Enhanced Actions Installer";
                DescriptionText.Text =
                    "This installer will enable full enhanced actions compatability on this PC.\n\n" +
                    "You can skip this step if you only require Wake-On-LAN functionality.";
            }
            else
            {
                SkipButton.Visibility = Visibility.Collapsed;

                HeaderText.Text = "Enhanced Actions Installer";
                DescriptionText.Text =
                    "This installer will enable full enhanced actions functionality on this PC.";
            }
        }

        private async void Install_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            LogBlock.Text = "";

            InstallButton.IsEnabled = false;
            SkipButton.IsEnabled = false;

            Progress.Visibility = Visibility.Visible;
            Progress.IsIndeterminate = true;
            Progress.Value = 0;

            try
            {
                var installer = new ActionsServerInstaller();

                installer.Log += msg =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        LogBlock.Text += msg + Environment.NewLine;
                        LogScroll.ChangeView(null, LogScroll.ScrollableHeight, null);
                    });
                };

                await Task.Run(() => installer.InstallShutdownService(ProvisioningState.Current, App.CurrentMode));

                _installCompleted = true;
                ProvisioningState.Current.ShutdownServiceInstalled = true;
                ProvisioningState.Update(ProvisioningState.Current);

                LogBlock.Text += Environment.NewLine + "Enhanced actions installation completed." + Environment.NewLine;

                Progress.IsIndeterminate = false;
                Progress.Value = 100;
            }
            catch (Exception ex)
            {
                LogBlock.Text += Environment.NewLine + $"ERROR: {ex.Message}" + Environment.NewLine;
                ErrorText.Text = "Installation failed. See log for details.";
                ErrorText.Visibility = Visibility.Visible;

                InstallButton.IsEnabled = true;
                SkipButton.IsEnabled = true;

                Progress.IsIndeterminate = false;
                Progress.Visibility = Visibility.Collapsed;
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e)
        {
            _skipSelected = true;

            ProvisioningState.Current.ShutdownServiceInstalled = false;
            ProvisioningState.Update(ProvisioningState.Current);

            ErrorText.Visibility = Visibility.Collapsed;

            // Immediately advance to the next wizard page
            WizardService.GoNext(this);
        }

        public bool ValidateAndSave()
        {
            if (App.CurrentMode == WizardMode.FullWoleSetup)
            {
                if (_installCompleted || _skipSelected)
                    return true;

                ErrorText.Text = "Please install enhanced actions or skip this step.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (!_installCompleted)
            {
                ErrorText.Text = "You must install enhanced actions to continue.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            return true;
        }
    }
}
