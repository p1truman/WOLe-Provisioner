using Microsoft.UI.Xaml;
using WOLe.Provisioner.Services;
using WOLe.Provisioner.Models;

namespace WOLe.Provisioner
{
    public partial class App : Application
    {
        public static WizardMode CurrentMode { get; set; } = WizardMode.FullWoleSetup;

        public App()
        {
            this.InitializeComponent();

            // Load provisioning state
            ProvisioningState.Ensure();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            var window = new MainWindow();
            MainWindow = window;
            m_window = window;
            window.Activate();
        }

        public static Window? MainWindow { get; private set; }

        private Window? m_window;
    }
}
