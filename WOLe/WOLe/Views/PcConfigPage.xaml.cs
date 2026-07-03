using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;
using System.Collections.ObjectModel;

namespace WOLe.Provisioner.Views
{
    public sealed partial class PcConfigPage : Page, IWizardPage
    {
        private readonly ObservableCollection<PcRow> _rows = new();
        private bool _initializing = true;

        private const int MaxEnhancedDevices = 2;

        public PcConfigPage()
        {
            InitializeComponent();

            PcItemsControl_NoScroll.ItemsSource = _rows;
            PcItemsControl_Scroll.ItemsSource = _rows;

            LoadInitialState();

            _initializing = false;
        }

        private void LoadInitialState()
        {
            var cfg = ProvisioningState.Current;
            var pcs = cfg.Pcs ?? new List<PcConfig>();

            _rows.Clear();

            int count = pcs.Count;
            if (count < 1) count = 1;
            if (count > 5) count = 5;

            PcCountComboBox.SelectedIndex = count - 1;
            PopulateRows(count, pcs);
            ToggleScroll(count);
        }

        private void PopulateRows(int count, List<PcConfig> pcs)
        {
            _rows.Clear();

            for (int i = 0; i < count; i++)
            {
                PcConfig? pc = i < pcs.Count ? pcs[i] : null;
                var row = new PcRow
                {
                    DisplayName = $"PC {i + 1}",
                    PowerDeviceId = pc?.PowerDeviceId ?? "",
                    MacAddress = pc?.MacAddress ?? "",
                    IpAddress = pc?.IpAddress ?? "",
                    Port = pc != null ? pc.Port.ToString() : "5050"
                };

                var enhancedIds = new List<string>();

                if (pc?.EnhancedDevices != null)
                {
                    enhancedIds.AddRange(
                        pc.EnhancedDevices
                          .Select(d => d?.DeviceId?.Trim() ?? "")
                          .Where(id => !string.IsNullOrWhiteSpace(id))
                          .Take(MaxEnhancedDevices));
                }

                // REMOVED: fallback from ActionDeviceId -> EnhancedDevices
                // if (enhancedIds.Count == 0 && !string.IsNullOrWhiteSpace(pc?.ActionDeviceId))
                //     enhancedIds.Add(pc.ActionDeviceId.Trim());

                for (int enhancedIndex = 0; enhancedIndex < enhancedIds.Count; enhancedIndex++)
                {
                    row.EnhancedDevices.Add(new EnhancedDeviceRow
                    {
                        Header = GetEnhancedHeader(enhancedIndex),
                        DeviceId = enhancedIds[enhancedIndex]
                    });
                }

                _rows.Add(row);
            }
        }

        private void PcCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing)
                return;

            if (PcCountComboBox.SelectedItem is ComboBoxItem item &&
                int.TryParse(item.Content.ToString(), out int count))
            {
                if (count < 1) count = 1;
                if (count > 5) count = 5;

                var cfg = ProvisioningState.Current;
                var pcs = cfg.Pcs ?? new List<PcConfig>();
                PopulateRows(count, pcs);
                ToggleScroll(count);
            }
        }

        private void AddEnhancedDevice_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (sender is not Button button || button.Tag is not PcRow row)
                return;

            if (row.EnhancedDevices.Count >= MaxEnhancedDevices)
            {
                ErrorText.Text = $"You can add up to {MaxEnhancedDevices} Enhanced Devices per PC.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            row.EnhancedDevices.Add(new EnhancedDeviceRow
            {
                Header = GetEnhancedHeader(row.EnhancedDevices.Count)
            });
        }

        private void RemoveEnhancedDevice_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (sender is not Button button || button.Tag is not PcRow row)
                return;

            if (row.EnhancedDevices.Count > 0)
                row.EnhancedDevices.RemoveAt(row.EnhancedDevices.Count - 1);
        }

        private static string GetEnhancedHeader(int index) => $"Enhanced Device {index + 1}";

        private void ToggleScroll(int count)
        {
            NonScrollContainer.Visibility = count == 1 ? Visibility.Visible : Visibility.Collapsed;
            ScrollContainer.Visibility = count == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var cfg = ProvisioningState.Current;
            var existingPcs = new List<PcConfig>(cfg.Pcs ?? new List<PcConfig>());
            var existingByPowerDeviceId = new Dictionary<string, PcConfig>(System.StringComparer.OrdinalIgnoreCase);
            var existingByEnhancedDeviceId = new Dictionary<string, (PcConfig Pc, EnhancedDeviceConfig Enhanced)>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var existingPc in existingPcs)
            {
                if (!string.IsNullOrWhiteSpace(existingPc.PowerDeviceId))
                    existingByPowerDeviceId[existingPc.PowerDeviceId] = existingPc;

                existingPc.EnhancedDevices ??= new();

                // REMOVED: fallback that created EnhancedDevices from ActionDeviceId
                // if (existingPc.EnhancedDevices.Count == 0 && !string.IsNullOrWhiteSpace(existingPc.ActionDeviceId))
                // {
                //     existingPc.EnhancedDevices.Add(new EnhancedDeviceConfig
                //     {
                //         DeviceId = existingPc.ActionDeviceId,
                //         ActionOn = existingPc.ActionOn,
                //         ActionOff = existingPc.ActionOff
                //     });
                // }

                foreach (var enhanced in existingPc.EnhancedDevices)
                {
                    if (!string.IsNullOrWhiteSpace(enhanced.DeviceId))
                        existingByEnhancedDeviceId[enhanced.DeviceId] = (existingPc, enhanced);
                }
            }

            cfg.Pcs ??= new();
            cfg.Pcs.Clear();

            for (int rowIndex = 0; rowIndex < _rows.Count; rowIndex++)
            {
                var row = _rows[rowIndex];
                var trimmedPowerDeviceId = row.PowerDeviceId.Trim();

                // Required fields
                if (string.IsNullOrWhiteSpace(trimmedPowerDeviceId) ||
                    string.IsNullOrWhiteSpace(row.MacAddress) ||
                    string.IsNullOrWhiteSpace(row.IpAddress) ||
                    string.IsNullOrWhiteSpace(row.Port))
                {
                    ErrorText.Text = "Power Device ID, MAC Address, IP Address, and Port are required.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                // Validate port
                if (!int.TryParse(row.Port, out int port))
                {
                    ErrorText.Text = $"Invalid port: {row.Port}";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

                PcConfig? existingPc = null;
                if (!string.IsNullOrWhiteSpace(trimmedPowerDeviceId))
                    existingByPowerDeviceId.TryGetValue(trimmedPowerDeviceId, out existingPc);

                if (existingPc == null)
                {
                    var firstEnhancedId = row.EnhancedDevices
                        .Select(d => d.DeviceId?.Trim() ?? "")
                        .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

                    if (!string.IsNullOrWhiteSpace(firstEnhancedId) &&
                        existingByEnhancedDeviceId.TryGetValue(firstEnhancedId, out var existingEnhancedPc))
                    {
                        existingPc = existingEnhancedPc.Pc;
                    }
                }

                existingPc ??= rowIndex < existingPcs.Count ? existingPcs[rowIndex] : null;

                var enhancedDevices = new List<EnhancedDeviceConfig>();
                foreach (var enhancedRow in row.EnhancedDevices.Take(MaxEnhancedDevices))
                {
                    var trimmedEnhancedDeviceId = (enhancedRow.DeviceId ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(trimmedEnhancedDeviceId))
                        continue;

                    string actionOn = "";
                    string actionOff = "";
                    if (existingByEnhancedDeviceId.TryGetValue(trimmedEnhancedDeviceId, out var existingEnhanced))
                    {
                        actionOn = existingEnhanced.Enhanced.ActionOn ?? "";
                        actionOff = existingEnhanced.Enhanced.ActionOff ?? "";
                    }

                    enhancedDevices.Add(new EnhancedDeviceConfig
                    {
                        DeviceId = trimmedEnhancedDeviceId,
                        ActionOn = actionOn,
                        ActionOff = actionOff
                    });
                }

                cfg.Pcs.Add(new PcConfig
                {
                    Name = row.DisplayName,
                    PowerDeviceId = trimmedPowerDeviceId,
                    ActionDeviceId = enhancedDevices.Count > 0 ? enhancedDevices[0].DeviceId : existingPc?.ActionDeviceId ?? "",
                    EnhancedDevices = enhancedDevices,
                    MacAddress = row.MacAddress.Trim(),
                    IpAddress = row.IpAddress.Trim(),
                    Port = port,
                    ActionOn = enhancedDevices.Count > 0 ? enhancedDevices[0].ActionOn : existingPc?.ActionOn ?? "",
                    ActionOff = enhancedDevices.Count > 0 ? enhancedDevices[0].ActionOff : existingPc?.ActionOff ?? "",
                    LaunchApp1Path = existingPc?.LaunchApp1Path ?? "",
                    LaunchApp2Path = existingPc?.LaunchApp2Path ?? "",
                    LaunchApp3Path = existingPc?.LaunchApp3Path ?? "",
                    LaunchApp4Path = existingPc?.LaunchApp4Path ?? ""
                });
            }

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}
