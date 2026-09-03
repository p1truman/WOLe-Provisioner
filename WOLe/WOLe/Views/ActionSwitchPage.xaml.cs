using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WOLe.Provisioner.Views
{
    public sealed partial class ActionSwitchPage : Page, IWizardPage
    {
        private EnhancedActionTarget? _target;
        private List<AppDiscovery.AppEntry> _discoveredApps = new();

        private string _launchApp1Path = "";
        private string _launchApp2Path = "";
        private string _launchApp3Path = "";
        private string _launchApp4Path = "";
        private string _launchApp5Path = "";
        private string _launchApp6Path = "";
        private string _launchApp7Path = "";
        private string _launchApp8Path = "";

        private static readonly (string Label, string Token)[] AllActions =
        {
            ("Restart",       "restart"),
            ("Sleep",         "sleep"),
            ("Hibernate",     "hibernate"),
            ("Lock",          "lock"),
            ("Screen Off",    "screenoff"),
            ("Mute",          "mute"),
            ("Unmute",        "unmute"),
            ("Volume Up",     "volumeup"),
            ("Volume Down",   "volumedown"),
            ("Launch App 1",  "launchapp1"),
            ("Launch App 2",  "launchapp2"),
            ("Launch App 3",  "launchapp3"),
            ("Launch App 4",  "launchapp4"),
            ("Launch App 5",  "launchapp5"),
            ("Launch App 6",  "launchapp6"),
            ("Launch App 7",  "launchapp7"),
            ("Launch App 8",  "launchapp8"),
        };

        public ActionSwitchPage()
        {
            InitializeComponent();
            LoadInitialState();
            _ = LoadAppsAsync();
        }

        private void LoadInitialState()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var cfg = ProvisioningState.Current;
            _target = WizardService.GetCurrentEnhancedActionTarget();

            if (_target == null || _target.PcIndex >= cfg.Pcs.Count)
            {
                ErrorText.Text = "No enhanced device configuration target was found.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var pc = cfg.Pcs[_target.PcIndex];
            pc.EnhancedDevices ??= new();

            if (_target.EnhancedDeviceIndex < 0 || _target.EnhancedDeviceIndex >= pc.EnhancedDevices.Count)
            {
                ErrorText.Text = "Enhanced device configuration target is invalid.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            var enhanced = pc.EnhancedDevices[_target.EnhancedDeviceIndex];

            ContextTitleText.Text = $"PC {_target.PcIndex + 1} – Enhanced Device {_target.EnhancedDeviceIndex + 1}";
            DeviceLabelText.Text  = $"PC {_target.PcIndex + 1}";

            BuildCombo(OnActionComboBox);
            BuildCombo(OffActionComboBox);

            SetInitialSelection(OnActionComboBox,  enhanced.ActionOn);
            SetInitialSelection(OffActionComboBox, enhanced.ActionOff);

            ApplyDisabledStates();

            OnActionComboBox.SelectionChanged  += ActionCombo_SelectionChanged;
            OffActionComboBox.SelectionChanged += ActionCombo_SelectionChanged;

            if (cfg.Pcs.Count > 0)
            {
                var pc0 = cfg.Pcs[0];
                _launchApp1Path = pc0.LaunchApp1Path ?? "";
                _launchApp2Path = pc0.LaunchApp2Path ?? "";
                _launchApp3Path = pc0.LaunchApp3Path ?? "";
                _launchApp4Path = pc0.LaunchApp4Path ?? "";
                _launchApp5Path = pc0.LaunchApp5Path ?? "";
                _launchApp6Path = pc0.LaunchApp6Path ?? "";
                _launchApp7Path = pc0.LaunchApp7Path ?? "";
                _launchApp8Path = pc0.LaunchApp8Path ?? "";
            }

            UpdatePc1AppPanels();
        }

        private void BuildCombo(ComboBox combo)
        {
            combo.Items.Clear();
            foreach (var (label, token) in AllActions)
                combo.Items.Add(new ComboBoxItem { Content = label, Tag = token });
        }

        private void SetInitialSelection(ComboBox combo, string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) { combo.SelectedIndex = -1; return; }
            foreach (ComboBoxItem item in combo.Items)
            {
                if ((string)item.Tag == token) { combo.SelectedItem = item; return; }
            }
            combo.SelectedIndex = -1;
        }

        private void ApplyDisabledStates()
        {
            var cfg      = ProvisioningState.Current;
            var reserved = GetReservedActionsForPc(cfg, _target!.PcIndex, _target.EnhancedDeviceIndex);
            string? on   = GetSelectedToken(OnActionComboBox);
            string? off  = GetSelectedToken(OffActionComboBox);
            DisableItems(OnActionComboBox,  reserved, on);
            DisableItems(OffActionComboBox, reserved, off);
        }

        private void DisableItems(ComboBox combo, HashSet<string> reserved, string? selected)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                string token = (string)item.Tag;
                if (token == selected) { item.IsEnabled = true; continue; }
                item.IsEnabled = !reserved.Contains(token);
            }
        }

        private static string? GetSelectedToken(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem cbi && cbi.Tag is string token)
                return token;
            return null;
        }

        private void ActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyDisabledStates();
            UpdatePc1AppPanels();
        }

        private void UpdatePc1AppPanels()
        {
            if (_target == null || _target.PcIndex != 0)
            {
                Pc1AppBindingsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            string? on  = GetSelectedToken(OnActionComboBox);
            string? off = GetSelectedToken(OffActionComboBox);

            bool usesApp1 = on == "launchapp1" || off == "launchapp1";
            bool usesApp2 = on == "launchapp2" || off == "launchapp2";
            bool usesApp3 = on == "launchapp3" || off == "launchapp3";
            bool usesApp4 = on == "launchapp4" || off == "launchapp4";
            bool usesApp5 = on == "launchapp5" || off == "launchapp5";
            bool usesApp6 = on == "launchapp6" || off == "launchapp6";
            bool usesApp7 = on == "launchapp7" || off == "launchapp7";
            bool usesApp8 = on == "launchapp8" || off == "launchapp8";

            LaunchApp1Panel.Visibility = usesApp1 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp2Panel.Visibility = usesApp2 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp3Panel.Visibility = usesApp3 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp4Panel.Visibility = usesApp4 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp5Panel.Visibility = usesApp5 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp6Panel.Visibility = usesApp6 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp7Panel.Visibility = usesApp7 ? Visibility.Visible : Visibility.Collapsed;
            LaunchApp8Panel.Visibility = usesApp8 ? Visibility.Visible : Visibility.Collapsed;

            Pc1AppBindingsPanel.Visibility =
                (usesApp1 || usesApp2 || usesApp3 || usesApp4 ||
                 usesApp5 || usesApp6 || usesApp7 || usesApp8)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private async Task LoadAppsAsync()
        {
            _discoveredApps = await Task.Run(AppDiscovery.GetInstalledApps);

            AppPickerHelper.PopulateAppComboBox(LaunchApp1ComboBox, _discoveredApps, _launchApp1Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp2ComboBox, _discoveredApps, _launchApp2Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp3ComboBox, _discoveredApps, _launchApp3Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp4ComboBox, _discoveredApps, _launchApp4Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp5ComboBox, _discoveredApps, _launchApp5Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp6ComboBox, _discoveredApps, _launchApp6Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp7ComboBox, _discoveredApps, _launchApp7Path);
            AppPickerHelper.PopulateAppComboBox(LaunchApp8ComboBox, _discoveredApps, _launchApp8Path);

            SetPathText(LaunchApp1PathText, _launchApp1Path);
            SetPathText(LaunchApp2PathText, _launchApp2Path);
            SetPathText(LaunchApp3PathText, _launchApp3Path);
            SetPathText(LaunchApp4PathText, _launchApp4Path);
            SetPathText(LaunchApp5PathText, _launchApp5Path);
            SetPathText(LaunchApp6PathText, _launchApp6Path);
            SetPathText(LaunchApp7PathText, _launchApp7Path);
            SetPathText(LaunchApp8PathText, _launchApp8Path);
        }

        private static void SetPathText(TextBlock tb, string path)
        {
            tb.Text       = path;
            tb.Visibility = string.IsNullOrWhiteSpace(path) ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── ComboBox handlers ──────────────────────────────────────────────

        private void LaunchApp1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp1ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp1Path = path; SetPathText(LaunchApp1PathText, path); }
        }
        private void LaunchApp2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp2ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp2Path = path; SetPathText(LaunchApp2PathText, path); }
        }
        private void LaunchApp3ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp3ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp3Path = path; SetPathText(LaunchApp3PathText, path); }
        }
        private void LaunchApp4ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp4ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp4Path = path; SetPathText(LaunchApp4PathText, path); }
        }
        private void LaunchApp5ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp5ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp5Path = path; SetPathText(LaunchApp5PathText, path); }
        }
        private void LaunchApp6ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp6ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp6Path = path; SetPathText(LaunchApp6PathText, path); }
        }
        private void LaunchApp7ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp7ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp7Path = path; SetPathText(LaunchApp7PathText, path); }
        }
        private void LaunchApp8ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LaunchApp8ComboBox.SelectedItem is ComboBoxItem cbi && cbi.Tag is string path)
            { _launchApp8Path = path; SetPathText(LaunchApp8PathText, path); }
        }

        // ── Browse buttons ─────────────────────────────────────────────────

        private async void BrowseLaunchApp1_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp1Path = path; SetPathText(LaunchApp1PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp1ComboBox, path);
        }
        private async void BrowseLaunchApp2_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp2Path = path; SetPathText(LaunchApp2PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp2ComboBox, path);
        }
        private async void BrowseLaunchApp3_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp3Path = path; SetPathText(LaunchApp3PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp3ComboBox, path);
        }
        private async void BrowseLaunchApp4_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp4Path = path; SetPathText(LaunchApp4PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp4ComboBox, path);
        }
        private async void BrowseLaunchApp5_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp5Path = path; SetPathText(LaunchApp5PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp5ComboBox, path);
        }
        private async void BrowseLaunchApp6_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp6Path = path; SetPathText(LaunchApp6PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp6ComboBox, path);
        }
        private async void BrowseLaunchApp7_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp7Path = path; SetPathText(LaunchApp7PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp7ComboBox, path);
        }
        private async void BrowseLaunchApp8_Click(object sender, RoutedEventArgs e)
        {
            var path = await AppPickerHelper.PickExecutableAsync(); if (path == null) return;
            _launchApp8Path = path; SetPathText(LaunchApp8PathText, path);
            AppPickerHelper.SetComboBoxToCustomPath(LaunchApp8ComboBox, path);
        }

        // ── Save ───────────────────────────────────────────────────────────

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (_target == null) return false;

            var cfg = ProvisioningState.Current;
            if (_target.PcIndex >= cfg.Pcs.Count) return false;

            var pc = cfg.Pcs[_target.PcIndex];
            pc.EnhancedDevices ??= new();
            if (_target.EnhancedDeviceIndex >= pc.EnhancedDevices.Count) return false;

            string? on  = GetSelectedToken(OnActionComboBox);
            string? off = GetSelectedToken(OffActionComboBox);

            if (string.IsNullOrWhiteSpace(on) || string.IsNullOrWhiteSpace(off))
            {
                ErrorText.Text = "Please select ON and OFF actions.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (on == off)
            {
                ErrorText.Text = "ON and OFF actions must be different.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            var reserved = GetReservedActionsForPc(cfg, _target.PcIndex, _target.EnhancedDeviceIndex);
            if (reserved.Contains(on) || reserved.Contains(off))
            {
                ErrorText.Text = "Selected actions are already in use by another enhanced device on this PC.";
                ErrorText.Visibility = Visibility.Visible;
                return false;
            }

            if (_target.PcIndex == 0)
            {
                var appChecks = new[]
                {
                    ("launchapp1", _launchApp1Path, "Launch App 1"),
                    ("launchapp2", _launchApp2Path, "Launch App 2"),
                    ("launchapp3", _launchApp3Path, "Launch App 3"),
                    ("launchapp4", _launchApp4Path, "Launch App 4"),
                    ("launchapp5", _launchApp5Path, "Launch App 5"),
                    ("launchapp6", _launchApp6Path, "Launch App 6"),
                    ("launchapp7", _launchApp7Path, "Launch App 7"),
                    ("launchapp8", _launchApp8Path, "Launch App 8"),
                };

                foreach (var (token, appPath, label) in appChecks)
                {
                    if ((on == token || off == token) && string.IsNullOrWhiteSpace(appPath))
                    {
                        ErrorText.Text = $"Please select an application for {label}.";
                        ErrorText.Visibility = Visibility.Visible;
                        return false;
                    }
                }
            }

            var enhanced = pc.EnhancedDevices[_target.EnhancedDeviceIndex];
            enhanced.ActionOn  = on!;
            enhanced.ActionOff = off!;

            if (_target.EnhancedDeviceIndex == 0)
            {
                pc.ActionDeviceId = enhanced.DeviceId ?? "";
                pc.ActionOn       = enhanced.ActionOn;
                pc.ActionOff      = enhanced.ActionOff;
            }

            if (_target.PcIndex == 0)
            {
                var pc0 = cfg.Pcs[0];
                pc0.EnhancedDevices ??= new();

                pc0.LaunchApp1Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp1" || d.ActionOff == "launchapp1") ? _launchApp1Path : "";
                pc0.LaunchApp2Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp2" || d.ActionOff == "launchapp2") ? _launchApp2Path : "";
                pc0.LaunchApp3Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp3" || d.ActionOff == "launchapp3") ? _launchApp3Path : "";
                pc0.LaunchApp4Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp4" || d.ActionOff == "launchapp4") ? _launchApp4Path : "";
                pc0.LaunchApp5Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp5" || d.ActionOff == "launchapp5") ? _launchApp5Path : "";
                pc0.LaunchApp6Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp6" || d.ActionOff == "launchapp6") ? _launchApp6Path : "";
                pc0.LaunchApp7Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp7" || d.ActionOff == "launchapp7") ? _launchApp7Path : "";
                pc0.LaunchApp8Path = pc0.EnhancedDevices.Any(d => d.ActionOn == "launchapp8" || d.ActionOff == "launchapp8") ? _launchApp8Path : "";
            }

            ProvisioningState.Update(cfg);
            return true;
        }

        private static HashSet<string> GetReservedActionsForPc(ProvisioningConfig cfg, int pcIndex, int excludeIndex)
        {
            var set = new HashSet<string>();
            if (pcIndex < 0 || pcIndex >= cfg.Pcs.Count) return set;

            var pc = cfg.Pcs[pcIndex];
            pc.EnhancedDevices ??= new();

            for (int i = 0; i < pc.EnhancedDevices.Count; i++)
            {
                if (i == excludeIndex) continue;
                var dev = pc.EnhancedDevices[i];
                if (!string.IsNullOrWhiteSpace(dev.ActionOn))  set.Add(dev.ActionOn);
                if (!string.IsNullOrWhiteSpace(dev.ActionOff)) set.Add(dev.ActionOff);
            }

            return set;
        }
    }
}