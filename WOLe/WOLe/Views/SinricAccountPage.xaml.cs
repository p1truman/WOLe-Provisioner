using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.System;
using WOLe.Provisioner.Models;

namespace WOLe.Provisioner.Views
{
    public sealed partial class SinricAccountPage : Page, IWizardPage
    {
        public SinricAccountPage()
        {
            InitializeComponent();
        }

        private async void ContinueToSinric_Click(object sender, RoutedEventArgs e)
        {
            var launched = await Launcher.LaunchUriAsync(new Uri("https://sinric.pro"));
            LaunchErrorText.Visibility = launched ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ValidateAndSave()
        {
            return true;
        }
    }
}
