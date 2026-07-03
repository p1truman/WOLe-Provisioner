using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class WelcomePage : Page
    {
        public WelcomePage()
        {
            InitializeComponent();
            Loaded += WelcomePage_Loaded;
            ActualThemeChanged += WelcomePage_ActualThemeChanged;
        }

        private void WelcomePage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateImagesForTheme();
            UpdateCompletionIndicators();
        }

        private void WelcomePage_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateImagesForTheme();
        }

        private void UpdateImagesForTheme()
        {
            var isDark = ActualTheme == ElementTheme.Dark;

            var logoPath = isDark
                ? "ms-appx:///Assets/WelcomeLogo-Dark.png"
                : "ms-appx:///Assets/WelcomeLogo-Light.png";

            var compatibilityPath = isDark
                ? "ms-appx:///Assets/compatability_dark.png"
                : "ms-appx:///Assets/compatability_light.png";

            WelcomeLogo.Source = new BitmapImage(new Uri(logoPath));
            CompatibilityImage.Source = new BitmapImage(new Uri(compatibilityPath));
        }

        private void WoleSetup_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentMode = WizardMode.FullWoleSetup;
            WizardService.StartFrom(PageKeys.WifiPage);
        }

        private void SystemConfig_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentMode = WizardMode.FullWoleSetup;
            WizardService.StartFrom(PageKeys.SystemConfigPageOne);
        }

        private void SinricAccount_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentMode = WizardMode.FullWoleSetup;
            WizardService.StartFrom(PageKeys.SinricAccountPage);
        }

        private void EnhancedActions_Click(object sender, RoutedEventArgs e)
        {
            App.CurrentMode = WizardMode.ShutdownOnly;
            WizardService.StartFrom(PageKeys.SystemConfigPageOne);
        }

        private void UpdateCompletionIndicators()
        {
            var cfg = ProvisioningState.Current;

            SystemConfigCompleteIcon.Visibility = cfg?.Step1Completed == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            SinricAccountCompleteIcon.Visibility = cfg?.Step2Completed == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            WoleSetupCompleteIcon.Visibility = cfg?.Step3Completed == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            EnhancedActionsCompleteIcon.Visibility = cfg?.Step4Completed == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}