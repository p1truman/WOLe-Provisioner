using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ShutdownActionMappingPage : Page, IWizardPage
    {
        private List<AppDiscovery.AppEntry> _discoveredApps = new();

        private bool _isLoaded = false;

        private PcConfig Pc => ProvisioningState.Current.Pcs[0];

        public ShutdownActionMappingPage()
        {
            InitializeComponent();
            ProvisioningState.Ensure();
            LoadInitialState();
            _ = LoadAppsAsync();
            _isLoaded = true;
        }

        // ───────────────────────────────────────────────
        // INITIAL STATE
        // ───────────────────────────────────────────────

        private void LoadInitialState()
        {
            UsesLaunchApp1Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp1Path);
            UsesLaunchApp2Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp2Path);
            UsesLaunchApp3Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp3Path);
            UsesLaunchApp4Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp4Path);

            LaunchApp1PathText.Text = Pc.LaunchApp1Path;
            LaunchApp2PathText.Text = Pc.LaunchApp2Path;
            LaunchApp3PathText.Text = Pc.LaunchApp3Path;
            LaunchApp4PathText.Text = Pc.LaunchApp4Path;

            LaunchApp1PathText.Visibility =
                UsesLaunchApp1Toggle.IsOn && !string.IsNullOrWhiteSpace(Pc.LaunchApp1Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            LaunchApp2PathText.Visibility =
                UsesLaunchApp2Toggle.IsOn && !string.IsNullOrWhiteSpace(Pc.LaunchApp2Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            LaunchApp3PathText.Visibility =
                UsesLaunchApp3Toggle.IsOn && !string.IsNullOrWhiteSpace(Pc.LaunchApp3Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            LaunchApp4PathText.Visibility =
                UsesLaunchApp4Toggle.IsOn && !string.IsNullOrWhiteSpace(Pc.LaunchApp4Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            // Enable/disable controls based on toggle state
            LaunchApp1ComboBox.IsEnabled = UsesLaunchApp1Toggle.IsOn;
            BrowseLaunchApp1Button.IsEnabled = UsesLaunchApp1Toggle.IsOn;

            LaunchApp2ComboBox.IsEnabled = UsesLaunchApp2Toggle.IsOn;
            BrowseLaunchApp2Button.IsEnabled = UsesLaunchApp2Toggle.IsOn;

            LaunchApp3ComboBox.IsEnabled = UsesLaunchApp3Toggle.IsOn;
            BrowseLaunchApp3Button.IsEnabled = UsesLaunchApp3Toggle.IsOn;

            LaunchApp4ComboBox.IsEnabled = UsesLaunchApp4Toggle.IsOn;
            BrowseLaunchApp4Button.IsEnabled = UsesLaunchApp4Toggle.IsOn;
        }

        // ───────────────────────────────────────────────
        // LOAD APPS
        // ───────────────────────────────────────────────

        private async Task LoadAppsAsync()
        {
            _discoveredApps = await Task.Run(AppDiscovery.GetInstalledApps);

            AppPickerHelper.PopulateAppComboBox(LaunchApp1ComboBox, _discoveredApps, Pc.LaunchApp1Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp2ComboBox, _discoveredApps, Pc.LaunchApp2Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp3ComboBox, _discoveredApps, Pc.LaunchApp3Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp4ComboBox, _discoveredApps, Pc.LaunchApp4Path);
        }

        // ───────────────────────────────────────────────
        // TOGGLE HANDLERS (inline layout)
        // ───────────────────────────────────────────────

        private void UsesLaunchApp1Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool enabled = UsesLaunchApp1Toggle.IsOn;

            LaunchApp1ComboBox.IsEnabled = enabled;
            BrowseLaunchApp1Button.IsEnabled = enabled;

            // DO NOT clear Pc.LaunchApp1Path anymore

            LaunchApp1PathText.Visibility =
                enabled && !string.IsNullOrWhiteSpace(Pc.LaunchApp1Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UsesLaunchApp2Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool enabled = UsesLaunchApp2Toggle.IsOn;

            LaunchApp2ComboBox.IsEnabled = enabled;
            BrowseLaunchApp2Button.IsEnabled = enabled;

            LaunchApp2PathText.Visibility =
                enabled && !string.IsNullOrWhiteSpace(Pc.LaunchApp2Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UsesLaunchApp3Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool enabled = UsesLaunchApp3Toggle.IsOn;

            LaunchApp3ComboBox.IsEnabled = enabled;
            BrowseLaunchApp3Button.IsEnabled = enabled;

            LaunchApp3PathText.Visibility =
                enabled && !string.IsNullOrWhiteSpace(Pc.LaunchApp3Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UsesLaunchApp4Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            bool enabled = UsesLaunchApp4Toggle.IsOn;

            LaunchApp4ComboBox.IsEnabled = enabled;
            BrowseLaunchApp4Button.IsEnabled = enabled;

            LaunchApp4PathText.Visibility =
                enabled && !string.IsNullOrWhiteSpace(Pc.LaunchApp4Path)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        // ───────────────────────────────────────────────
        // COMBOBOX HANDLERS
        // ───────────────────────────────────────────────

        private void LaunchApp1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp1ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            {
                Pc.LaunchApp1Path = path;
                LaunchApp1PathText.Text = path;
                LaunchApp1PathText.Visibility = Visibility.Visible;
            }
        }

        private void LaunchApp2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp2ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            {
                Pc.LaunchApp2Path = path;
                LaunchApp2PathText.Text = path;
                LaunchApp2PathText.Visibility = Visibility.Visible;
            }
        }

        private void LaunchApp3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp3ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            {
                Pc.LaunchApp3Path = path;
                LaunchApp3PathText.Text = path;
                LaunchApp3PathText.Visibility = Visibility.Visible;
            }
        }

        private void LaunchApp4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp4ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            {
                Pc.LaunchApp4Path = path;
                LaunchApp4PathText.Text = path;
                LaunchApp4PathText.Visibility = Visibility.Visible;
            }
        }

        // ───────────────────────────────────────────────
        // BROWSE BUTTONS
        // ───────────────────────────────────────────────

        private async void BrowseLaunchApp1_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync();
            if (path == null) return;

            Pc.LaunchApp1Path = path;
            LaunchApp1PathText.Text = path;
            LaunchApp1PathText.Visibility = Visibility.Visible;
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp1ComboBox, path);
        }

        private async void BrowseLaunchApp2_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync();
            if (path == null) return;

            Pc.LaunchApp2Path = path;
            LaunchApp2PathText.Text = path;
            LaunchApp2PathText.Visibility = Visibility.Visible;
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp2ComboBox, path);
        }

        private async void BrowseLaunchApp3_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync();
            if (path == null) return;

            Pc.LaunchApp3Path = path;
            LaunchApp3PathText.Text = path;
            LaunchApp3PathText.Visibility = Visibility.Visible;
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp3ComboBox, path);
        }

        private async void BrowseLaunchApp4_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync();
            if (path == null) return;

            Pc.LaunchApp4Path = path;
            LaunchApp4PathText.Text = path;
            LaunchApp4PathText.Visibility = Visibility.Visible;
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp4ComboBox, path);
        }

        // ───────────────────────────────────────────────
        // VALIDATION
        // ───────────────────────────────────────────────

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (UsesLaunchApp1Toggle.IsOn && string.IsNullOrWhiteSpace(Pc.LaunchApp1Path))
            {
                ErrorText.Text = "Please select an application for Launch App 1.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (UsesLaunchApp2Toggle.IsOn && string.IsNullOrWhiteSpace(Pc.LaunchApp2Path))
            {
                ErrorText.Text = "Please select an application for Launch App 2.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (UsesLaunchApp3Toggle.IsOn && string.IsNullOrWhiteSpace(Pc.LaunchApp3Path))
            {
                ErrorText.Text = "Please select an application for Launch App 3.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (UsesLaunchApp4Toggle.IsOn && string.IsNullOrWhiteSpace(Pc.LaunchApp4Path))
            {
                ErrorText.Text = "Please select an application for Launch App 4.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            ProvisioningState.Update(ProvisioningState.Current);
            return true;
        }
    }
}
