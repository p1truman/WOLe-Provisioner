using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;

namespace WOLe.Provisioner.Views
{
    public sealed partial class SystemConfigPageOne : Page, IWizardPage
    {
        public SystemConfigPageOne()
        {
            InitializeComponent();
        }

        public bool ValidateAndSave()
        {
            // Add validation later if needed.
            return true;
        }
    }
}
