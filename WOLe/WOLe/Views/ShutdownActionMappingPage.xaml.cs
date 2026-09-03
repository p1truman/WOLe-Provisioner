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

        private void LoadInitialState()
        {
            UsesLaunchApp1Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp1Path);
            UsesLaunchApp2Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp2Path);
            UsesLaunchApp3Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp3Path);
            UsesLaunchApp4Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp4Path);
            UsesLaunchApp5Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp5Path);
            UsesLaunchApp6Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp6Path);
            UsesLaunchApp7Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp7Path);
            UsesLaunchApp8Toggle.IsOn = !string.IsNullOrWhiteSpace(Pc.LaunchApp8Path);

            SetRowEnabled(LaunchApp1ComboBox, BrowseLaunchApp1Button, UsesLaunchApp1Toggle.IsOn);
            SetRowEnabled(LaunchApp2ComboBox, BrowseLaunchApp2Button, UsesLaunchApp2Toggle.IsOn);
            SetRowEnabled(LaunchApp3ComboBox, BrowseLaunchApp3Button, UsesLaunchApp3Toggle.IsOn);
            SetRowEnabled(LaunchApp4ComboBox, BrowseLaunchApp4Button, UsesLaunchApp4Toggle.IsOn);
            SetRowEnabled(LaunchApp5ComboBox, BrowseLaunchApp5Button, UsesLaunchApp5Toggle.IsOn);
            SetRowEnabled(LaunchApp6ComboBox, BrowseLaunchApp6Button, UsesLaunchApp6Toggle.IsOn);
            SetRowEnabled(LaunchApp7ComboBox, BrowseLaunchApp7Button, UsesLaunchApp7Toggle.IsOn);
            SetRowEnabled(LaunchApp8ComboBox, BrowseLaunchApp8Button, UsesLaunchApp8Toggle.IsOn);
        }

        private static void SetRowEnabled(ComboBox cb, Button btn, bool enabled)
        {
            cb.IsEnabled  = enabled;
            btn.IsEnabled = enabled;
        }

        private static void UpdateComboTooltip(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path
                && !string.IsNullOrWhiteSpace(path))
                ToolTipService.SetToolTip(combo, path);
            else
                ToolTipService.SetToolTip(combo, null);
        }

        private async Task LoadAppsAsync()
        {
            _discoveredApps = await Task.Run(AppDiscovery.GetInstalledApps);

            AppPickerHelper.PopulateAppComboBox(LaunchApp1ComboBox, _discoveredApps, Pc.LaunchApp1Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp2ComboBox, _discoveredApps, Pc.LaunchApp2Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp3ComboBox, _discoveredApps, Pc.LaunchApp3Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp4ComboBox, _discoveredApps, Pc.LaunchApp4Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp5ComboBox, _discoveredApps, Pc.LaunchApp5Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp6ComboBox, _discoveredApps, Pc.LaunchApp6Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp7ComboBox, _discoveredApps, Pc.LaunchApp7Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp8ComboBox, _discoveredApps, Pc.LaunchApp8Path);

            UpdateComboTooltip(LaunchApp1ComboBox);
            UpdateComboTooltip(LaunchApp2ComboBox);
            UpdateComboTooltip(LaunchApp3ComboBox);
            UpdateComboTooltip(LaunchApp4ComboBox);
            UpdateComboTooltip(LaunchApp5ComboBox);
            UpdateComboTooltip(LaunchApp6ComboBox);
            UpdateComboTooltip(LaunchApp7ComboBox);
            UpdateComboTooltip(LaunchApp8ComboBox);
        }

        // ── Toggle handlers ────────────────────────────────────────────────

        private void UsesLaunchApp1Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp1ComboBox, BrowseLaunchApp1Button, UsesLaunchApp1Toggle.IsOn);
            if (!UsesLaunchApp1Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp1ComboBox, null);
        }
        private void UsesLaunchApp2Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp2ComboBox, BrowseLaunchApp2Button, UsesLaunchApp2Toggle.IsOn);
            if (!UsesLaunchApp2Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp2ComboBox, null);
        }
        private void UsesLaunchApp3Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp3ComboBox, BrowseLaunchApp3Button, UsesLaunchApp3Toggle.IsOn);
            if (!UsesLaunchApp3Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp3ComboBox, null);
        }
        private void UsesLaunchApp4Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp4ComboBox, BrowseLaunchApp4Button, UsesLaunchApp4Toggle.IsOn);
            if (!UsesLaunchApp4Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp4ComboBox, null);
        }
        private void UsesLaunchApp5Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp5ComboBox, BrowseLaunchApp5Button, UsesLaunchApp5Toggle.IsOn);
            if (!UsesLaunchApp5Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp5ComboBox, null);
        }
        private void UsesLaunchApp6Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp6ComboBox, BrowseLaunchApp6Button, UsesLaunchApp6Toggle.IsOn);
            if (!UsesLaunchApp6Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp6ComboBox, null);
        }
        private void UsesLaunchApp7Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp7ComboBox, BrowseLaunchApp7Button, UsesLaunchApp7Toggle.IsOn);
            if (!UsesLaunchApp7Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp7ComboBox, null);
        }
        private void UsesLaunchApp8Toggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            SetRowEnabled(LaunchApp8ComboBox, BrowseLaunchApp8Button, UsesLaunchApp8Toggle.IsOn);
            if (!UsesLaunchApp8Toggle.IsOn) ToolTipService.SetToolTip(LaunchApp8ComboBox, null);
        }

        // ── ComboBox handlers ──────────────────────────────────────────────

        private void LaunchApp1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp1ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp1Path = path;
            UpdateComboTooltip(LaunchApp1ComboBox);
        }
        private void LaunchApp2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp2ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp2Path = path;
            UpdateComboTooltip(LaunchApp2ComboBox);
        }
        private void LaunchApp3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp3ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp3Path = path;
            UpdateComboTooltip(LaunchApp3ComboBox);
        }
        private void LaunchApp4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp4ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp4Path = path;
            UpdateComboTooltip(LaunchApp4ComboBox);
        }
        private void LaunchApp5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp5ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp5Path = path;
            UpdateComboTooltip(LaunchApp5ComboBox);
        }
        private void LaunchApp6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp6ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp6Path = path;
            UpdateComboTooltip(LaunchApp6ComboBox);
        }
        private void LaunchApp7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp7ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp7Path = path;
            UpdateComboTooltip(LaunchApp7ComboBox);
        }
        private void LaunchApp8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp8ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path) Pc.LaunchApp8Path = path;
            UpdateComboTooltip(LaunchApp8ComboBox);
        }

        // ── Browse buttons ─────────────────────────────────────────────────

        private async void BrowseLaunchApp1_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp1Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp1ComboBox, path);
            UpdateComboTooltip(LaunchApp1ComboBox);
        }
        private async void BrowseLaunchApp2_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp2Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp2ComboBox, path);
            UpdateComboTooltip(LaunchApp2ComboBox);
        }
        private async void BrowseLaunchApp3_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp3Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp3ComboBox, path);
            UpdateComboTooltip(LaunchApp3ComboBox);
        }
        private async void BrowseLaunchApp4_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp4Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp4ComboBox, path);
            UpdateComboTooltip(LaunchApp4ComboBox);
        }
        private async void BrowseLaunchApp5_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp5Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp5ComboBox, path);
            UpdateComboTooltip(LaunchApp5ComboBox);
        }
        private async void BrowseLaunchApp6_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp6Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp6ComboBox, path);
            UpdateComboTooltip(LaunchApp6ComboBox);
        }
        private async void BrowseLaunchApp7_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp7Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp7ComboBox, path);
            UpdateComboTooltip(LaunchApp7ComboBox);
        }
        private async void BrowseLaunchApp8_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            Pc.LaunchApp8Path = path; AppPickerHelper.SetComboBoxToCustomPath(LaunchApp8ComboBox, path);
            UpdateComboTooltip(LaunchApp8ComboBox);
        }

        // ── Validation ─────────────────────────────────────────────────────

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var checks = new[]
            {
                (UsesLaunchApp1Toggle.IsOn, Pc.LaunchApp1Path, "Launch App 1"),
                (UsesLaunchApp2Toggle.IsOn, Pc.LaunchApp2Path, "Launch App 2"),
                (UsesLaunchApp3Toggle.IsOn, Pc.LaunchApp3Path, "Launch App 3"),
                (UsesLaunchApp4Toggle.IsOn, Pc.LaunchApp4Path, "Launch App 4"),
                (UsesLaunchApp5Toggle.IsOn, Pc.LaunchApp5Path, "Launch App 5"),
                (UsesLaunchApp6Toggle.IsOn, Pc.LaunchApp6Path, "Launch App 6"),
                (UsesLaunchApp7Toggle.IsOn, Pc.LaunchApp7Path, "Launch App 7"),
                (UsesLaunchApp8Toggle.IsOn, Pc.LaunchApp8Path, "Launch App 8"),
            };

            foreach (var (isOn, path, label) in checks)
            {
                if (isOn && string.IsNullOrWhiteSpace(path))
                {
                    ErrorText.Text       = $"Please select an application for {label}.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }
            }

            ProvisioningState.Update(ProvisioningState.Current);
            return true;
        }
    }
}