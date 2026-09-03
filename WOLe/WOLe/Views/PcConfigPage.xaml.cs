using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using WOLe.Provisioner.Models;
using WOLe.Provisioner.Services;

namespace WOLe.Provisioner.Views
{
    public sealed partial class PcConfigPage : Page, IWizardPage
    {
        private readonly ObservableCollection<PcRow> _rows = new();
        private bool _initializing = true;

        private const int MaxEnhancedDevices = 4;

        // Keeps track of each row's enhanced-device panel so we can
        // rebuild it whenever the collection changes.
        private readonly Dictionary<PcRow, StackPanel> _enhancedPanels = new();

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
            _enhancedPanels.Clear();

            int count = pcs.Count;
            if (count < 1) count = 1;
            if (count > 5) count = 5;

            PcCountComboBox.SelectedIndex = count - 1;
            PopulateRows(count, pcs);
            ToggleScroll(count);
        }

        private void PopulateRows(int count, List<PcConfig> pcs)
        {
            // Unhook old rows
            foreach (var row in _rows)
                row.EnhancedDevices.CollectionChanged -= EnhancedDevices_CollectionChanged;

            _rows.Clear();
            _enhancedPanels.Clear();

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

                if (pc?.EnhancedDevices != null)
                {
                    var ids = pc.EnhancedDevices
                        .Select(d => d?.DeviceId?.Trim() ?? "")
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Take(MaxEnhancedDevices)
                        .ToList();

                    for (int j = 0; j < ids.Count; j++)
                        row.EnhancedDevices.Add(new EnhancedDeviceRow
                        {
                            Header = GetEnhancedHeader(j),
                            DeviceId = ids[j]
                        });
                }

                row.EnhancedDevices.CollectionChanged += EnhancedDevices_CollectionChanged;
                _rows.Add(row);
            }
        }

        // Called by the DataTemplate via the Tag-based button clicks AND
        // also when the collection changes — we find the panel and rebuild.
        private void EnhancedDevices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Find which row owns this collection
            var row = _rows.FirstOrDefault(r => r.EnhancedDevices == sender);
            if (row == null) return;

            if (_enhancedPanels.TryGetValue(row, out var panel))
                RebuildEnhancedPanel(panel, row);
        }

        // ----------------------------------------------------------------
        //  Builds or rebuilds the enhanced device boxes inside 'panel'
        //  according to the count-aware layout rules:
        //
        //    count == 1  →  device 1 full width
        //    count == 2  →  device 1 full width
        //                   device 2 full width
        //    count == 3  →  [device 1 | device 2]  (50/50 row)
        //                   device 3 full width
        //    count == 4  →  [device 1 | device 2]  (50/50 row)
        //                   [device 3 | device 4]  (50/50 row)
        // ----------------------------------------------------------------
        private static void RebuildEnhancedPanel(StackPanel panel, PcRow row)
        {
            panel.Children.Clear();

            var devices = row.EnhancedDevices;
            int count = devices.Count;

            if (count == 0)
                return;

            // Decide which devices get a paired row vs a full-width row.
            // Pair rule: pair[0,1] when count >= 3; pair[2,3] when count == 4.
            bool pairFirstTwo  = count >= 3;
            bool pairLastTwo   = count == 4;

            int i = 0;
            while (i < count)
            {
                bool isPairedRow = (i == 0 && pairFirstTwo) || (i == 2 && pairLastTwo);

                if (isPairedRow && i + 1 < count)
                {
                    // Two boxes side by side, 50/50
                    var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(0, 0, 0, 8) };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var left  = MakeEnhancedTextBox(devices[i]);
                    var right = MakeEnhancedTextBox(devices[i + 1]);

                    Grid.SetColumn(left,  0);
                    Grid.SetColumn(right, 1);
                    grid.Children.Add(left);
                    grid.Children.Add(right);

                    panel.Children.Add(grid);
                    i += 2;
                }
                else
                {
                    // Full-width single box
                    var box = MakeEnhancedTextBox(devices[i]);
                    box.Margin = new Thickness(0, 0, 0, 8);
                    panel.Children.Add(box);
                    i++;
                }
            }
        }

        private static TextBox MakeEnhancedTextBox(EnhancedDeviceRow device)
        {
            var box = new TextBox { Header = device.Header };

            // Two-way bind Text to DeviceId
            var binding = new Binding
            {
                Source = device,
                Path = new PropertyPath(nameof(EnhancedDeviceRow.DeviceId)),
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };
            box.SetBinding(TextBox.TextProperty, binding);

            return box;
        }

        // Called from XAML DataTemplate — registers the panel for this row
        // and does the initial build.
        private void EnhancedPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not StackPanel panel)
                return;

            // Walk up to find the PcRow via the DataContext of the parent
            // ContentPresenter. The panel's DataContext IS the PcRow because
            // it's inside the DataTemplate.
            if (panel.DataContext is not PcRow row)
                return;

            _enhancedPanels[row] = panel;
            RebuildEnhancedPanel(panel, row);
        }

        private void PcCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing) return;

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
            // CollectionChanged fires → EnhancedDevices_CollectionChanged → RebuildEnhancedPanel
        }

        private void RemoveEnhancedDevice_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (sender is not Button button || button.Tag is not PcRow row)
                return;

            if (row.EnhancedDevices.Count > 0)
                row.EnhancedDevices.RemoveAt(row.EnhancedDevices.Count - 1);
            // CollectionChanged fires → RebuildEnhancedPanel
        }

        private static string GetEnhancedHeader(int index) => $"Enhanced Device {index + 1}";

        private void ToggleScroll(int count)
        {
            NonScrollContainer.Visibility = count == 1 ? Visibility.Visible : Visibility.Collapsed;
            ScrollContainer.Visibility    = count == 1 ? Visibility.Collapsed : Visibility.Visible;
        }

        public bool ValidateAndSave()
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var cfg = ProvisioningState.Current;
            var existingPcs = new List<PcConfig>(cfg.Pcs ?? new List<PcConfig>());
            var existingByPowerDeviceId      = new Dictionary<string, PcConfig>(System.StringComparer.OrdinalIgnoreCase);
            var existingByEnhancedDeviceId   = new Dictionary<string, (PcConfig Pc, EnhancedDeviceConfig Enhanced)>(System.StringComparer.OrdinalIgnoreCase);

            foreach (var existingPc in existingPcs)
            {
                if (!string.IsNullOrWhiteSpace(existingPc.PowerDeviceId))
                    existingByPowerDeviceId[existingPc.PowerDeviceId] = existingPc;

                existingPc.EnhancedDevices ??= new();

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

                if (string.IsNullOrWhiteSpace(trimmedPowerDeviceId) ||
                    string.IsNullOrWhiteSpace(row.MacAddress) ||
                    string.IsNullOrWhiteSpace(row.IpAddress) ||
                    string.IsNullOrWhiteSpace(row.Port))
                {
                    ErrorText.Text = "Power Device ID, MAC Address, IP Address, and Port are required.";
                    ErrorText.Visibility = Visibility.Visible;
                    return false;
                }

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
                        existingPc = existingEnhancedPc.Pc;
                }

                existingPc ??= rowIndex < existingPcs.Count ? existingPcs[rowIndex] : null;

                var enhancedDevices = new List<EnhancedDeviceConfig>();
                foreach (var enhancedRow in row.EnhancedDevices.Take(MaxEnhancedDevices))
                {
                    var trimmedId = (enhancedRow.DeviceId ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(trimmedId)) continue;

                    string actionOn  = "";
                    string actionOff = "";
                    if (existingByEnhancedDeviceId.TryGetValue(trimmedId, out var existingEnhanced))
                    {
                        actionOn  = existingEnhanced.Enhanced.ActionOn  ?? "";
                        actionOff = existingEnhanced.Enhanced.ActionOff ?? "";
                    }

                    enhancedDevices.Add(new EnhancedDeviceConfig
                    {
                        DeviceId  = trimmedId,
                        ActionOn  = actionOn,
                        ActionOff = actionOff
                    });
                }

                cfg.Pcs.Add(new PcConfig
                {
                    Name            = row.DisplayName,
                    PowerDeviceId   = trimmedPowerDeviceId,
                    ActionDeviceId  = enhancedDevices.Count > 0 ? enhancedDevices[0].DeviceId : existingPc?.ActionDeviceId ?? "",
                    EnhancedDevices = enhancedDevices,
                    MacAddress      = row.MacAddress.Trim(),
                    IpAddress       = row.IpAddress.Trim(),
                    Port            = port,
                    ActionOn        = enhancedDevices.Count > 0 ? enhancedDevices[0].ActionOn  : existingPc?.ActionOn  ?? "",
                    ActionOff       = enhancedDevices.Count > 0 ? enhancedDevices[0].ActionOff : existingPc?.ActionOff ?? "",
                    LaunchApp1Path  = existingPc?.LaunchApp1Path ?? "",
                    LaunchApp2Path  = existingPc?.LaunchApp2Path ?? "",
                    LaunchApp3Path  = existingPc?.LaunchApp3Path ?? "",
                    LaunchApp4Path  = existingPc?.LaunchApp4Path ?? ""
                });
            }

            ProvisioningState.Update(cfg);
            return true;
        }
    }
}