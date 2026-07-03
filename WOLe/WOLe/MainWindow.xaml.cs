using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using WOLe.Provisioner.Views;

namespace WOLe.Provisioner
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            appWindow.Resize(new Windows.Graphics.SizeInt32(1050, 840));
            appWindow.SetIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

            NavigationService.Initialize(RootFrame);
            NavigationService.Navigate(typeof(SplashPage));

            RootFrame.Navigated += RootFrame_Navigated;
        }

        private void RootFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var page = e.SourcePageType;

            WizardFooter.Visibility = page == typeof(WelcomePage) || page == typeof(SplashPage)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (page == typeof(FinalPage) || page == typeof(ShutdownCompletePage))
            {
                NextButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (page == typeof(SplashPage))
            {
                NextButton.Visibility = Visibility.Collapsed;
                BackButton.Visibility = Visibility.Collapsed;
                return;
            }

            NextButton.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Visible;

            bool isDonePage =
                (page == typeof(SystemConfigPageOne) && App.CurrentMode == WizardMode.FullWoleSetup) ||
                page == typeof(SinricAccountPage);

            NextButton.Content = isDonePage ? "Done" : "Next";
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            WizardService.GoBack();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (RootFrame.Content is SystemConfigPageOne step1Page)
            {
                if (!step1Page.ValidateAndSave())
                    return;

                if (App.CurrentMode == WizardMode.FullWoleSetup)
                {
                    SetStepComplete(cfg => cfg.Step1Completed = true);
                    WizardService.GoToWelcome();
                }
                else
                {
                    WizardService.GoNext(RootFrame.Content);
                }

                return;
            }

            if (RootFrame.Content is SinricAccountPage step2Page)
            {
                if (!step2Page.ValidateAndSave())
                    return;

                SetStepComplete(cfg => cfg.Step2Completed = true);
                WizardService.GoToWelcome();
                return;
            }

            WizardService.GoNext(RootFrame.Content);
        }

        private static void SetStepComplete(Action<ProvisioningConfig> setFlag)
        {
            var cfg = ProvisioningState.Current ?? new ProvisioningConfig();
            setFlag(cfg);
            ProvisioningState.Update(cfg);
        }
    }
}