using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace WOLe.Provisioner.Controls
{
    public sealed partial class UnifiedProvisioningControl : UserControl
    {
        private class NetAdapterInfo
        {
            public string Name { get; set; } = string.Empty;
            public string InterfaceDescription { get; set; } = string.Empty;
            public string InterfaceAlias { get; set; } = string.Empty;
            public string DeviceID { get; set; } = string.Empty;

            public string DisplayName => $"{Name} ({InterfaceDescription})";
        }

        private readonly string _baseTempFolder =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Temp", "WOL-e", "Network");

        private string TempScriptPath =>
            Path.Combine(_baseTempFolder, "temp.ps1");

        private string LogPath =>
            Path.Combine(_baseTempFolder, "pslog.txt");

        private List<NetAdapterInfo> _adapters = new();

        private int _octet1 = 0;
        private int _octet2 = 0;
        private int _octet3 = 0;
        private int _octet4 = 0;
        private int _prefixLength = 24;
        private string _gatewayIp = "";

        public UnifiedProvisioningControl()
        {
            InitializeComponent();
            Loaded += UnifiedProvisioningControl_Loaded;
            Directory.CreateDirectory(_baseTempFolder);
        }

        private async void UnifiedProvisioningControl_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadAdaptersAsync();
        }

        private Brush? TryGetBrush(string resourceKey)
        {
            if (Resources.TryGetValue(resourceKey, out object localResource) && localResource is Brush localBrush)
                return localBrush;

            if (Application.Current?.Resources.TryGetValue(resourceKey, out object appResource) == true && appResource is Brush appBrush)
                return appBrush;

            return null;
        }

        private void SetStatusNeutral(string message)
        {
            StatusText.Text = message;
            var brush = TryGetBrush("StatusNeutralBrush");
            if (brush != null)
                StatusText.Foreground = brush;
        }

        private void SetStatusSuccess(string message)
        {
            StatusText.Text = message;
            var brush = TryGetBrush("StatusSuccessBrush");
            if (brush != null)
                StatusText.Foreground = brush;
        }

        private void SetStatusError(string message)
        {
            StatusText.Text = message;
            var brush = TryGetBrush("StatusErrorBrush");
            if (brush != null)
                StatusText.Foreground = brush;
        }

        private async Task LoadAdaptersAsync()
        {
            try
            {
                SetStatusNeutral("Loading network adapters…");

                string json = await Task.Run(() =>
                    RunPowerShellCapture("Get-NetAdapter | Select-Object Name, InterfaceDescription, InterfaceAlias, DeviceID | ConvertTo-Json -Depth 3"));

                if (string.IsNullOrWhiteSpace(json))
                {
                    SetStatusError("Unable to enumerate network adapters.");
                    return;
                }

                if (json.TrimStart().StartsWith("["))
                {
                    _adapters = JsonSerializer.Deserialize<List<NetAdapterInfo>>(json) ?? new List<NetAdapterInfo>();
                }
                else
                {
                    var single = JsonSerializer.Deserialize<NetAdapterInfo>(json);
                    _adapters = single != null ? new List<NetAdapterInfo> { single } : new List<NetAdapterInfo>();
                }

                _adapters = _adapters
                    .Where(a =>
                        !string.IsNullOrWhiteSpace(a.InterfaceDescription) &&
                        !a.InterfaceDescription.Contains("Tunnel", StringComparison.OrdinalIgnoreCase) &&
                        !a.InterfaceDescription.Contains("VPN", StringComparison.OrdinalIgnoreCase) &&
                        !a.InterfaceDescription.Contains("Virtual", StringComparison.OrdinalIgnoreCase) &&
                        !a.InterfaceDescription.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase) &&
                        !a.InterfaceDescription.Contains("WireGuard", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                AdapterSelector.ItemsSource = _adapters;

                if (_adapters.Any())
                {
                    AdapterSelector.SelectedIndex = 0;
                    SetStatusNeutral(string.Empty);
                }
                else
                {
                    SetStatusError("No physical Ethernet/Wi‑Fi adapters found.");
                }
            }
            catch (Exception ex)
            {
                SetStatusError($"Error loading adapters: {ex.Message}");
            }
        }

        private void AdapterSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshAdapterInfo();
        }

        private void RefreshAdapterInfo()
        {
            IpAddressText.Text = "IPv4: —";
            MacAddressText.Text = "MAC: —";
            GatewayText.Text = "Gateway: —";

            _octet1 = _octet2 = _octet3 = _octet4 = 0;
            _prefixLength = 24;
            _gatewayIp = "";

            IpPrefixText.Text = "0 . 0 . 0 .";
            IpOctet4Box.Text = "0";

            if (AdapterSelector.SelectedItem is not NetAdapterInfo selected)
                return;

            try
            {
                var nic = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(n =>
                        string.Equals(n.Description, selected.InterfaceDescription, StringComparison.OrdinalIgnoreCase));

                if (nic == null)
                {
                    SetStatusError("Unable to read adapter details from system.");
                    return;
                }

                var props = nic.GetIPProperties();

                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                var gateway = props.GatewayAddresses
                    .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);

                var mac = nic.GetPhysicalAddress()?.ToString();

                IpAddressText.Text = $"IPv4: {ipv4?.Address}";
                MacAddressText.Text = $"MAC: {FormatMac(mac)}";
                GatewayText.Text = $"Gateway: {gateway?.Address}";
                SetStatusNeutral(string.Empty);

                if (ipv4 != null)
                {
                    var parts = ipv4.Address.ToString().Split('.');
                    if (parts.Length == 4 &&
                        int.TryParse(parts[0], out _octet1) &&
                        int.TryParse(parts[1], out _octet2) &&
                        int.TryParse(parts[2], out _octet3) &&
                        int.TryParse(parts[3], out _octet4))
                    {
                        _prefixLength = ipv4.PrefixLength;

                        _gatewayIp = DetectGateway(selected.InterfaceDescription, _octet1, _octet2, _octet3);

                        IpPrefixText.Text = $"{_octet1} . {_octet2} . {_octet3} .";
                        IpOctet4Box.Text = _octet4.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                SetStatusError($"Error reading adapter info: {ex.Message}");
            }
        }

        private void CopyAdapterDetails_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ipv4 = IpAddressText.Text;
                string mac = MacAddressText.Text;
                string gateway = GatewayText.Text;

                if (string.IsNullOrWhiteSpace(ipv4) || ipv4 == "IPv4: —")
                {
                    SetStatusError("No adapter details available to copy.");
                    return;
                }

                string clipboardText =
                    $"{ipv4}{Environment.NewLine}" +
                    $"{mac}{Environment.NewLine}" +
                    $"{gateway}";

                var package = new DataPackage();
                package.SetText(clipboardText);
                Clipboard.SetContent(package);

                SetStatusSuccess("Adapter details copied to clipboard.");
            }
            catch (Exception ex)
            {
                SetStatusError($"Unable to copy adapter details: {ex.Message}");
            }
        }

        private string DetectGateway(string desc, int o1, int o2, int o3)
        {
            string escapedDesc = desc.Replace("'", "''");

            string gw = RunPowerShellCapture(
                $"(Get-NetRoute -DestinationPrefix \"0.0.0.0/0\" | Where-Object {{$_.InterfaceDescription -eq '{escapedDesc}'}} | Select-Object -ExpandProperty NextHop -ErrorAction SilentlyContinue)"
            ).Trim();

            if (string.IsNullOrWhiteSpace(gw) || gw.Contains("MSFT"))
                return $"{o1}.{o2}.{o3}.1";

            return gw;
        }

        private static string FormatMac(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Length != 12)
                return raw ?? "—";

            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => raw.Substring(i * 2, 2)));
        }

        private void IpOctet4Box_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IpOctet4Box == null)
                return;

            string text = IpOctet4Box.Text.Trim();

            if (string.IsNullOrEmpty(text))
            {
                SetStatusError("Please enter a number between 2 and 254.");
                return;
            }

            if (!int.TryParse(text, out int last))
            {
                SetStatusError("Please enter a number between 2 and 254.");
                return;
            }

            if (last < 2 || last > 254)
            {
                SetStatusError("Please enter a number between 2 and 254.");
                return;
            }

            if (StatusText.Text == "Please enter a number between 2 and 254.")
            {
                SetStatusNeutral(string.Empty);
            }

            _octet4 = last;
        }

        private void ApplyManualStaticIp_Click(object sender, RoutedEventArgs e)
        {
            if (AdapterSelector.SelectedItem is not NetAdapterInfo selected)
            {
                SetStatusError("Please select a network adapter first.");
                return;
            }

            if (_octet1 == 0 && _octet2 == 0 && _octet3 == 0)
            {
                SetStatusError("Unable to determine base IP.");
                return;
            }

            if (!int.TryParse(IpOctet4Box.Text.Trim(), out int lastOctet) || lastOctet < 2 || lastOctet > 254)
            {
                SetStatusError("Please enter a valid final IP octet between 2 and 254.");
                return;
            }

            _octet4 = lastOctet;

            string newIp = $"{_octet1}.{_octet2}.{_octet3}.{_octet4}";
            string desc = selected.InterfaceDescription.Replace("'", "''");
            string name = selected.Name.Replace("'", "''");

            string gatewayIp = string.IsNullOrWhiteSpace(_gatewayIp)
                ? $"{_octet1}.{_octet2}.{_octet3}.1"
                : _gatewayIp;

            int prefix = _prefixLength <= 0 ? 24 : _prefixLength;

            SetStatusNeutral($"Applying static IP: {newIp}");

            string script =
@"$ErrorActionPreference = 'Stop'

$desc = '" + desc + @"'
$name = '" + name + @"'

$adapter = Get-NetAdapter | Where-Object { $_.InterfaceDescription -eq $desc -or $_.Name -eq $name } | Select-Object -First 1
if (-not $adapter) {
    Write-Output 'Adapter not found.'
    exit
}

$ifName = $adapter.Name
$index  = $adapter.ifIndex

Get-NetIPAddress -InterfaceIndex $index -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue

Get-NetRoute -InterfaceIndex $index -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
    Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 400

Set-NetIPInterface -InterfaceIndex $index -Dhcp Disabled -ErrorAction SilentlyContinue

Start-Sleep -Milliseconds 400

New-NetIPAddress -InterfaceIndex $index -IPAddress '" + newIp + @"' -PrefixLength " + prefix + @" -DefaultGateway '" + gatewayIp + @"' -ErrorAction Stop

Set-DnsClientServerAddress -InterfaceIndex $index -ServerAddresses '8.8.8.8','1.1.1.1' -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2

Disable-NetAdapter -Name $ifName -Confirm:$false -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2
Enable-NetAdapter -Name $ifName -Confirm:$false -ErrorAction SilentlyContinue
";

            string output = RunPowerShellWithOutput(script);
            File.AppendAllText(LogPath, $"[{DateTime.Now}] APPLY STATIC IP\n{output}\n\n");

            IpAddressText.Text = $"IPv4: {newIp}";
            GatewayText.Text = $"Gateway: {gatewayIp}";
            SetStatusSuccess($"Static IP applied: {newIp}");
        }

        private void EnableWol_Click(object sender, RoutedEventArgs e)
        {
            if (AdapterSelector.SelectedItem is not NetAdapterInfo selected)
            {
                SetStatusError("Please select a network adapter first.");
                return;
            }

            try
            {
                SetStatusNeutral("Configuring Wake-on-LAN…");

                string desc = selected.InterfaceDescription.Replace("'", "''");
                string name = selected.Name.Replace("'", "''");

                string script =
@"$ErrorActionPreference = 'Continue'

$desc = '" + desc + @"'
$name = '" + name + @"'

Write-Output '=== WOL CONFIGURATION START ==='
Write-Output ""Requested adapter description: $desc""
Write-Output ""Requested adapter name:        $name""

$adapter = Get-NetAdapter -ErrorAction SilentlyContinue | Where-Object {
    $_.InterfaceDescription -eq $desc -or $_.Name -eq $name
} | Select-Object -First 1

if (-not $adapter) {
    Write-Output 'Adapter not found.'
    exit
}

$ifName = $adapter.Name
$ifDesc = $adapter.InterfaceDescription
$ifIndex = $adapter.ifIndex

Write-Output ""Resolved adapter name:        $ifName""
Write-Output ""Resolved adapter description: $ifDesc""
Write-Output ""Resolved adapter ifIndex:     $ifIndex""

function Get-PowerCfgList {
    param([string]$QueryType)

    try {
        $raw = & powercfg -devicequery $QueryType 2>&1 | Out-String

        if ([string]::IsNullOrWhiteSpace($raw)) {
            return @()
        }

        $lines = $raw -split ""`r?`n"" |
            ForEach-Object { $_.Trim() } |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace($_) -and
                $_ -notmatch '^PowerCfg' -and
                $_ -notmatch '^The following devices' -and
                $_ -notmatch '^Currently there are no wake'
            }

        return @($lines)
    }
    catch {
        return @()
    }
}

function Write-PowerCfgListToLog {
    param([string]$QueryType)

    $items = @(Get-PowerCfgList $QueryType)

    Write-Output (""POWERCFG LIST {0}:"" -f $QueryType)
    if ($items.Count -eq 0) {
        Write-Output '  <none>'
    }
    else {
        foreach ($item in $items) {
            Write-Output (""  {0}"" -f $item)
        }
    }
}

function Resolve-PowerCfgDeviceName {
    param(
        [string]$AdapterName,
        [string]$AdapterDescription
    )

    $allCandidates = @()

    foreach ($queryType in @('wake_programmable', 'wake_from_any', 'wake_armed')) {
        $items = @(Get-PowerCfgList $queryType)
        foreach ($item in $items) {
            $allCandidates += $item
        }
    }

    $uniqueCandidates = @($allCandidates | Select-Object -Unique)

    foreach ($candidate in $uniqueCandidates) {
        if ($candidate -eq $AdapterDescription) { return [string]$candidate }
    }
    foreach ($candidate in $uniqueCandidates) {
        if ($candidate -eq $AdapterName) { return [string]$candidate }
    }
    foreach ($candidate in $uniqueCandidates) {
        if ($candidate -like ""*$AdapterDescription*"") { return [string]$candidate }
    }
    foreach ($candidate in $uniqueCandidates) {
        if ($candidate -like ""*$AdapterName*"") { return [string]$candidate }
    }

    return $null
}

function Test-PowerCfgContainsExact {
    param(
        [string]$QueryType,
        [string]$Needle
    )

    if ([string]::IsNullOrWhiteSpace($Needle)) {
        return $false
    }

    $items = @(Get-PowerCfgList $QueryType)

    foreach ($item in $items) {
        if ($item -eq $Needle) {
            return $true
        }
    }

    return $false
}

function Set-AdvPropIfFound {
    param(
        [string]$AdapterName,
        [string[]]$NamePatterns,
        [string[]]$EnableValues,
        [string[]]$DisableValues,
        [bool]$Enable
    )

    $props = @(Get-NetAdapterAdvancedProperty -Name $AdapterName -ErrorAction SilentlyContinue)
    if ($props.Count -eq 0) {
        Write-Output ""Advanced properties unavailable for $AdapterName.""
        return $false
    }

    $matched = @(
        $props | Where-Object {
            $dn = $_.DisplayName
            if ([string]::IsNullOrWhiteSpace($dn)) { return $false }

            foreach ($pattern in $NamePatterns) {
                if ($dn -like $pattern) { return $true }
            }

            return $false
        }
    )

    if ($matched.Count -eq 0) {
        return $false
    }

    $overallSuccess = $false

    foreach ($prop in $matched) {
        $displayName = [string]$prop.DisplayName
        $displayValue = [string]$prop.DisplayValue

        Write-Output ""Found advanced property: '$displayName' = '$displayValue'""

        $candidateValues = if ($Enable) { $EnableValues } else { $DisableValues }

        foreach ($value in $candidateValues) {
            try {
                Set-NetAdapterAdvancedProperty -Name $AdapterName -DisplayName $displayName -DisplayValue $value -NoRestart -ErrorAction Stop | Out-Null
                Write-Output ""Set '$displayName' -> '$value'""
                $overallSuccess = $true
                break
            }
            catch {
                Write-Output ""Attempt failed: '$displayName' -> '$value' : $($_.Exception.Message)""
            }
        }
    }

    return ([bool]$overallSuccess)
}

$summary = [ordered]@{
    PowerCfgResolvedDeviceName = ''
    WakeProgrammableBefore = $false
    WakeFromAnyBefore = $false
    WakeArmedBefore = $false
    WakeEnableIssued = $false
    MagicPacketPropertySet = $false
    PatternWakeDisabled = $false
    LinkWakeDisabled = $false
    ShutdownWakeConfigured = $false
    PowerManagementMagicPacketSet = $false
    PowerManagementPatternDisabled = $false
    RegistryFallbackUsed = $false
    WakeProgrammableAfter = $false
    WakeArmedAfter = $false
}

Write-PowerCfgListToLog 'wake_programmable'
Write-PowerCfgListToLog 'wake_from_any'
Write-PowerCfgListToLog 'wake_armed'

$resolvedPowerCfgName = Resolve-PowerCfgDeviceName -AdapterName $ifName -AdapterDescription $ifDesc

if ($resolvedPowerCfgName) {
    $summary.PowerCfgResolvedDeviceName = [string]$resolvedPowerCfgName
    Write-Output ""Resolved powercfg device name: $resolvedPowerCfgName""
}
else {
    Write-Output 'No matching powercfg device name was found.'
}

$summary.WakeProgrammableBefore = [bool](Test-PowerCfgContainsExact -QueryType 'wake_programmable' -Needle $resolvedPowerCfgName)
$summary.WakeFromAnyBefore      = [bool](Test-PowerCfgContainsExact -QueryType 'wake_from_any' -Needle $resolvedPowerCfgName)
$summary.WakeArmedBefore        = [bool](Test-PowerCfgContainsExact -QueryType 'wake_armed' -Needle $resolvedPowerCfgName)

Write-Output ""Wake programmable before: $($summary.WakeProgrammableBefore)""
Write-Output ""Wake from any before:     $($summary.WakeFromAnyBefore)""
Write-Output ""Wake armed before:        $($summary.WakeArmedBefore)""

Write-Output '--- Querying advanced properties ---'
try {
    $allProps = Get-NetAdapterAdvancedProperty -Name $ifName -ErrorAction SilentlyContinue
    if ($allProps) {
        foreach ($p in $allProps) {
            Write-Output ""ADV PROP: $($p.DisplayName) = $($p.DisplayValue)""
        }
    }
    else {
        Write-Output 'No advanced properties returned.'
    }
}
catch {
    Write-Output ""Unable to query advanced properties: $($_.Exception.Message)""
}

$summary.MagicPacketPropertySet = [bool](Set-AdvPropIfFound `
    -AdapterName $ifName `
    -NamePatterns @('*Magic Packet*', '*Wake on Magic Packet*') `
    -EnableValues @('Enabled', 'Enable', 'On', '1', 'Magic Packet') `
    -DisableValues @() `
    -Enable $true)

$summary.PatternWakeDisabled = [bool](Set-AdvPropIfFound `
    -AdapterName $ifName `
    -NamePatterns @('*Pattern*', '*Wake on pattern match*') `
    -EnableValues @() `
    -DisableValues @('Disabled', 'Disable', 'Off', '0') `
    -Enable $false)

$summary.LinkWakeDisabled = [bool](Set-AdvPropIfFound `
    -AdapterName $ifName `
    -NamePatterns @('*Wake on link*', '*Wake on Link*', '*Link Wake*', '*Wake on link settings*') `
    -EnableValues @() `
    -DisableValues @('Disabled', 'Disable', 'Off', '0', 'Not Speed Down') `
    -Enable $false)

$summary.ShutdownWakeConfigured = [bool](Set-AdvPropIfFound `
    -AdapterName $ifName `
    -NamePatterns @('*Shutdown Wake-On-Lan*', '*S5 Wake on LAN*', '*Wake From Shutdown*') `
    -EnableValues @('Enabled', 'Enable', 'On', '1', '10 Mbps First') `
    -DisableValues @() `
    -Enable $true)

Write-Output '--- Applying adapter power management settings ---'
try {
    Set-NetAdapterPowerManagement -Name $ifName -WakeOnMagicPacket Enabled -ErrorAction Stop | Out-Null
    $summary.PowerManagementMagicPacketSet = $true
    Write-Output 'Set-NetAdapterPowerManagement WakeOnMagicPacket = Enabled'
}
catch {
    Write-Output ""Failed to set WakeOnMagicPacket: $($_.Exception.Message)""
}

try {
    Set-NetAdapterPowerManagement -Name $ifName -WakeOnPattern Disabled -ErrorAction Stop | Out-Null
    $summary.PowerManagementPatternDisabled = $true
    Write-Output 'Set-NetAdapterPowerManagement WakeOnPattern = Disabled'
}
catch {
    Write-Output ""Failed to set WakeOnPattern: $($_.Exception.Message)""
}

Write-Output '--- Querying adapter power management ---'
try {
    $pm = Get-NetAdapterPowerManagement -Name $ifName -ErrorAction SilentlyContinue
    if ($pm) {
        $pm | Format-List * | Out-String | Write-Output
    }
    else {
        Write-Output 'Get-NetAdapterPowerManagement returned no data.'
    }
}
catch {
    Write-Output ""Get-NetAdapterPowerManagement unavailable or failed: $($_.Exception.Message)""
}

Write-Output '--- Enabling OS wake permission ---'
if (-not [string]::IsNullOrWhiteSpace($resolvedPowerCfgName)) {
    try {
        $powerCfgOutput = & powercfg -deviceenablewake ""$resolvedPowerCfgName"" 2>&1 | Out-String
        if (-not [string]::IsNullOrWhiteSpace($powerCfgOutput)) {
            Write-Output $powerCfgOutput
        }
        $summary.WakeEnableIssued = $true
        Write-Output ""powercfg -deviceenablewake issued for: $resolvedPowerCfgName""
    }
    catch {
        Write-Output ""powercfg failed for resolved device '$resolvedPowerCfgName': $($_.Exception.Message)""
    }
}
else {
    Write-Output 'Skipping powercfg -deviceenablewake because no matching powercfg device name was found.'
}

Start-Sleep -Milliseconds 800

if (-not $summary.MagicPacketPropertySet) {
    Write-Output '--- Using limited registry fallback ---'
    try {
        $base = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}'
        $keys = Get-ChildItem -Path $base -ErrorAction SilentlyContinue | Where-Object {
            $_.PSChildName -match '^\d{4}$'
        }

        $regPath = $null

        foreach ($k in $keys) {
            try {
                $p = Get-ItemProperty -Path $k.PSPath -ErrorAction Stop
                if ($p.DriverDesc -eq $ifDesc) {
                    $regPath = $k.PSPath
                    break
                }
            }
            catch {}
        }

        if ($regPath) {
            Write-Output ""Registry fallback path: $regPath""

            $fallbackKeys = @{
                'WakeOnMagicPacket' = 1
                'WakeOnMagicPacketEnabled' = 1
                'WakeOnPattern' = 0
                '*WakeOnPattern' = 0
            }

            foreach ($key in $fallbackKeys.Keys) {
                try {
                    Set-ItemProperty -Path $regPath -Name $key -Value $fallbackKeys[$key] -Force -ErrorAction SilentlyContinue
                    Write-Output ""Fallback registry set: $key = $($fallbackKeys[$key])""
                    $summary.RegistryFallbackUsed = $true
                }
                catch {
                    Write-Output ""Fallback registry write failed for: $key""
                }
            }
        }
        else {
            Write-Output 'No registry path found for fallback.'
        }
    }
    catch {
        Write-Output ""Registry fallback failed: $($_.Exception.Message)""
    }
}

Write-Output '--- Restarting adapter ---'
try {
    Disable-NetAdapter -Name $ifName -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Enable-NetAdapter -Name $ifName -Confirm:$false -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    Write-Output 'Adapter restarted.'
}
catch {
    Write-Output ""Adapter restart failed: $($_.Exception.Message)""
}

$summary.WakeProgrammableAfter = [bool](Test-PowerCfgContainsExact -QueryType 'wake_programmable' -Needle $resolvedPowerCfgName)
$summary.WakeArmedAfter        = [bool](Test-PowerCfgContainsExact -QueryType 'wake_armed' -Needle $resolvedPowerCfgName)

Write-Output ""Wake programmable after: $($summary.WakeProgrammableAfter)""
Write-Output ""Wake armed after:        $($summary.WakeArmedAfter)""

Write-Output '--- SUMMARY ---'
$summary.GetEnumerator() | ForEach-Object {
    Write-Output (""{0}: {1}"" -f $_.Key, $_.Value)
}

Write-Output '=== WOL CONFIGURATION END ==='
";

                string output = RunPowerShellWithOutput(script);
                File.AppendAllText(LogPath, $"[{DateTime.Now}] ENABLE WOL\n{output}\n\n");

                SetStatusSuccess("Wake-on-LAN configured successfully.");
            }
            catch (Exception ex)
            {
                SetStatusError($"Error enabling WOL: {ex.Message}");
            }
        }

        private string RunPowerShellWithOutput(string script)
        {
            try
            {
                Directory.CreateDirectory(_baseTempFolder);

                if (File.Exists(TempScriptPath))
                    File.Delete(TempScriptPath);

                File.WriteAllText(TempScriptPath, script);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{TempScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return "Failed to start PowerShell.";

                string output = proc.StandardOutput.ReadToEnd();
                string error = proc.StandardError.ReadToEnd();

                proc.WaitForExit();

                try
                {
                    if (File.Exists(TempScriptPath))
                        File.Delete(TempScriptPath);
                }
                catch
                {
                }

                return output + Environment.NewLine + error;
            }
            catch (Exception ex)
            {
                return $"Exception while running PowerShell: {ex}";
            }
        }

        private static string RunPowerShellCapture(string command)
        {
            var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return string.Empty;

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();
            return output;
        }
    }
}