using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using WOLe.Provisioner.Models;

namespace WOLe.Provisioner.Services
{
    public class ActionsServerInstaller
    {
        public event Action<string>? Log;

        private readonly DispatcherQueue _ui;

        public ActionsServerInstaller()
        {
            _ui = DispatcherQueue.GetForCurrentThread();
        }

        private void Append(string msg)
        {
            if (_ui != null)
                _ui.TryEnqueue(() => Log?.Invoke(msg));
            else
                Log?.Invoke(msg);
        }

        private string BasePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WOL-e");

        private string ActionsServiceRoot =>
            Path.Combine(BasePath, "actions-service");

        private string ServerExePath =>
            Path.Combine(ActionsServiceRoot, "ActionsServer.exe");

        private string NirCmdPath =>
            Path.Combine(ActionsServiceRoot, "nircmd.exe");

        private string ToolsRoot =>
            Path.Combine(AppContext.BaseDirectory, "Tools");

        private string ToolsServerExe =>
            Path.Combine(ToolsRoot, "ActionsServer.exe");

        private string ToolsNirCmdExe =>
            Path.Combine(ToolsRoot, "nircmd.exe");

        // ------------------------------------------------------------
        //  INSTALL / UNINSTALL
        // ------------------------------------------------------------

        public void InstallShutdownService(ProvisioningConfig cfg, WizardMode mode)
        {
            Append("=== WOL-e Enhanced Actions Installer (Native) ===");
            Append("App running from: " + AppContext.BaseDirectory);

            Directory.CreateDirectory(ActionsServiceRoot);

            EnsureServerBinary();

            InstallUnifiedActionServer(cfg, mode);

            InstallFirewallRule(cfg, mode);

            Append("=== Installation Complete ===");
        }

        public void UninstallShutdownService()
        {
            Append("=== WOL-e Enhanced Actions Uninstaller (Native) ===");

            StopRunningServer();

            RemoveAllLegacyTasks();
            RemoveFirewallRule();

            try
            {
                if (Directory.Exists(ActionsServiceRoot))
                {
                    Append($"Removing folder: {ActionsServiceRoot}");
                    Directory.Delete(ActionsServiceRoot, true);
                }
                else
                {
                    Append("actions-service folder not found — nothing to remove.");
                }
            }
            catch (Exception ex)
            {
                Append("ERR: Failed to remove actions-service folder: " + ex.Message);
            }

            Append("=== Uninstall Complete ===");
        }

        // ------------------------------------------------------------
        //  SERVER BINARY
        // ------------------------------------------------------------

        private void StopRunningServer()
        {
            try
            {
                var runningProcesses = Process.GetProcessesByName("ActionsServer");
                if (runningProcesses.Length > 0)
                {
                    Append($"Stopping {runningProcesses.Length} running ActionsServer instance(s)...");
                    foreach (var proc in runningProcesses)
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                            Append($"Stopped ActionsServer (PID {proc.Id}).");
                        }
                        catch (Exception ex)
                        {
                            Append($"WARN: Could not stop ActionsServer PID {proc.Id}: {ex.Message}");
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }

                    // Small delay to allow the OS to fully release the file handle
                    System.Threading.Thread.Sleep(500);
                }
                else
                {
                    Append("No running ActionsServer instance found.");
                }
            }
            catch (Exception ex)
            {
                Append("WARN: Failed to check for running ActionsServer: " + ex.Message);
            }
        }

        private void EnsureServerBinary()
        {
            Append("Checking ActionsServer.exe...");

            if (!File.Exists(ToolsServerExe))
            {
                Append("ERR: ActionsServer.exe not found in Tools folder.");
                return;
            }

            Directory.CreateDirectory(ActionsServiceRoot);

            // Stop any running instance before overwriting binaries
            StopRunningServer();

            File.Copy(ToolsServerExe, ServerExePath, true);
            Append($"Copied ActionsServer.exe to: {ServerExePath}");

            if (File.Exists(ToolsNirCmdExe))
            {
                File.Copy(ToolsNirCmdExe, NirCmdPath, true);
                Append($"Copied nircmd.exe to: {NirCmdPath}");
            }
            else
            {
                Append("WARN: nircmd.exe not found in Tools folder — volume/mute will not work.");
            }
        }

        // ------------------------------------------------------------
        //  LOCAL PC MATCHING
        // ------------------------------------------------------------

        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            }
            return "127.0.0.1";
        }

        private PcConfig? MatchLocalPc(ProvisioningConfig cfg)
        {
            string localIp = GetLocalIPAddress();
            Append($"Local machine IP detected as: {localIp}");

            if (cfg.Pcs == null || cfg.Pcs.Count == 0)
            {
                Append("No PC list found — using ShutdownOnly mode.");
                return null;
            }

            var match = cfg.Pcs.FirstOrDefault(pc => pc.IpAddress == localIp);

            if (match == null)
            {
                Append("WARNING: No matching PC found for this machine. Defaulting to PC1.");
                return cfg.Pcs[0];
            }

            Append($"Matched this machine to PC entry: {match.IpAddress}:{match.Port}");
            return match;
        }

        // ------------------------------------------------------------
        //  SERVER INSTALLATION
        // ------------------------------------------------------------

        private void InstallUnifiedActionServer(ProvisioningConfig cfg, WizardMode mode)
        {
            string configPath = Path.Combine(ActionsServiceRoot, "config.json");

            PcConfig? localPc = MatchLocalPc(cfg);

            int port;
            string secret;

            bool shutdownOnly = mode == WizardMode.ShutdownOnly;

            if (shutdownOnly || localPc == null)
            {
                port = cfg.ShutdownPcPort ?? 5050;
                secret = cfg.ShutdownSecret;
            }
            else
            {
                port = localPc.Port > 0 ? localPc.Port : 5050;
                secret = cfg.ShutdownSecret;
            }

            // App binding paths always come from Pcs[0] regardless of mode.
            var pc1 = cfg.Pcs != null && cfg.Pcs.Count > 0 ? cfg.Pcs[0] : null;

            string launchApp1Path = pc1?.LaunchApp1Path ?? "";
            string launchApp2Path = pc1?.LaunchApp2Path ?? "";
            string launchApp3Path = pc1?.LaunchApp3Path ?? "";
            string launchApp4Path = pc1?.LaunchApp4Path ?? "";

            // Problem #1 fix: write keys Program.cs actually reads
            int volumeUpStep = ClampStep(cfg.VolumeUpStepPercent);
            int volumeDownStep = ClampStep(cfg.VolumeDownStepPercent);

            string escapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

            File.WriteAllText(configPath, $@"{{
  ""port"": {port},
  ""secret"": ""{escapeJson(secret)}"",
  ""VolumeUpStep"": {volumeUpStep},
  ""VolumeDownStep"": {volumeDownStep},
  ""launchApp1"": ""{escapeJson(launchApp1Path)}"",
  ""launchApp2"": ""{escapeJson(launchApp2Path)}"",
  ""launchApp3"": ""{escapeJson(launchApp3Path)}"",
  ""launchApp4"": ""{escapeJson(launchApp4Path)}""
}}");

            Append($"Generated config.json: {configPath}");
            Append($"Volume steps configured: up {volumeUpStep}%, down {volumeDownStep}%");

            if (!string.IsNullOrWhiteSpace(launchApp1Path)) Append("App 1 binding: configured");
            if (!string.IsNullOrWhiteSpace(launchApp2Path)) Append("App 2 binding: configured");
            if (!string.IsNullOrWhiteSpace(launchApp3Path)) Append("App 3 binding: configured");
            if (!string.IsNullOrWhiteSpace(launchApp4Path)) Append("App 4 binding: configured");

            InstallScheduledTask();

            // Start the server immediately so the user does not need to log out
            try
            {
                Append("Starting ActionsServer...");
                var psi = new ProcessStartInfo
                {
                    FileName = ServerExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Append("ActionsServer started.");
            }
            catch (Exception ex)
            {
                Append("WARN: Could not auto-start ActionsServer after install: " + ex.Message);
            }
        }

        private int ClampStep(int value)
        {
            if (value < 1) return 1;
            if (value > 50) return 50;
            return value;
        }

        // ------------------------------------------------------------
        //  SCHEDULED TASK (per-user, hidden)
        // ------------------------------------------------------------

        private void InstallScheduledTask()
        {
            const string newTask = "WOL-e_Actions";

            Append("Removing ALL legacy tasks...");
            RemoveAllLegacyTasks();

            Append("Installing per-user hidden scheduled task...");

            var user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var registrationDate = DateTime.UtcNow.ToString("s") + "Z";
            string command = ServerExePath;

            string taskXml =
$@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Date>{registrationDate}</Date>
    <Author>WOL-e</Author>
    <Description>WOL-e Actions Server (per-user, hidden)</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <Delay>PT5S</Delay>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <UserId>{user}</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <Hidden>true</Hidden>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{command}</Command>
      <Arguments></Arguments>
    </Exec>
  </Actions>
</Task>";

            string tmpXml = Path.Combine(Path.GetTempPath(), "WOL-e_Actions_Task.xml");
            File.WriteAllText(tmpXml, taskXml, Encoding.Unicode);

            RunCommand("schtasks", $"/Create /TN \"{newTask}\" /XML \"{tmpXml}\" /F");

            try { File.Delete(tmpXml); } catch { }

            Append("Per-user hidden scheduled task installed.");
        }

        private void RemoveAllLegacyTasks()
        {
            string[] legacyNames =
            {
                "WOL-e_Shutdown",
                "WOL-e_Actions"
            };

            foreach (var name in legacyNames)
            {
                Append($"Removing task: {name}");
                RunCommand("schtasks", $"/Delete /TN \"{name}\" /F");
            }
        }

        // ------------------------------------------------------------
        //  FIREWALL (config.json-based port cleanup, URL ACL handling)
        // ------------------------------------------------------------

        private void InstallFirewallRule(ProvisioningConfig cfg, WizardMode mode)
        {
            Append("Configuring Windows Firewall...");

            RemoveFirewallRule();

            // Program-based fallback rule
            RunCommand("netsh",
                $"advfirewall firewall add rule name=\"WOL-e Actions Server\" " +
                $"dir=in action=allow program=\"{ServerExePath}\" enable=yes");

            Append("Program-based firewall rule added.");

            // Multi-PC port rules
            if (cfg.Pcs != null)
            {
                foreach (var pc in cfg.Pcs)
                {
                    if (!string.IsNullOrWhiteSpace(pc.IpAddress) && pc.Port > 0)
                    {
                        RunCommand("netsh",
                            $"advfirewall firewall add rule name=\"WOL-e Actions Server Port {pc.Port}\" " +
                            $"dir=in action=allow protocol=TCP localport={pc.Port} profile=any enable=yes");

                        Append($"Firewall port rule added for PC port {pc.Port}");

                        try
                        {
                            AddUrlAcl(pc.Port);
                            Append($"URL ACL added for port {pc.Port}");
                        }
                        catch (Exception ex)
                        {
                            Append($"WARN: URL ACL add failed for port {pc.Port}: {ex.Message} (requires admin)");
                        }
                    }
                }
            }

            // Shutdown-only mode port rule
            if (mode == WizardMode.ShutdownOnly && cfg.ShutdownPcPort != null)
            {
                int shutdownPort = cfg.ShutdownPcPort.Value;

                RunCommand("netsh",
                    $"advfirewall firewall add rule name=\"WOL-e Shutdown Server Port {shutdownPort}\" " +
                    $"dir=in action=allow protocol=TCP localport={shutdownPort} profile=any enable=yes");

                Append($"Firewall port rule added for shutdown-only port {shutdownPort}");

                try
                {
                    AddUrlAcl(shutdownPort);
                    Append($"URL ACL added for shutdown port {shutdownPort}");
                }
                catch (Exception ex)
                {
                    Append($"WARN: URL ACL add failed for shutdown port {shutdownPort}: {ex.Message} (requires admin)");
                }
            }

            Append("Firewall configuration complete.");
        }

        private void RemoveFirewallRule()
        {
            Append("Removing firewall rules (if exist)...");

            // Remove program-based rules (by program path)
            RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Actions Server\" program=\"{ServerExePath}\"");
            RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Shutdown Server\" program=\"{ServerExePath}\"");

            // Read config.json to find the port and remove its rules and URL ACL
            try
            {
                string configPath = Path.Combine(ActionsServiceRoot, "config.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("port", out var portProp) &&
                        portProp.ValueKind == JsonValueKind.Number)
                    {
                        int port = portProp.GetInt32();

                        RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Actions Server Port {port}\"");
                        RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Shutdown Server Port {port}\"");
                        RunCommand("netsh", $"advfirewall firewall delete rule protocol=TCP localport={port}");
                        Append($"Removed firewall rules for port {port}");

                        try
                        {
                            RemoveUrlAcl(port);
                            Append($"Removed URL ACL for port {port}");
                        }
                        catch (Exception ex)
                        {
                            Append($"WARN: RemoveUrlAcl failed for port {port}: {ex.Message} (requires admin)");
                        }
                    }
                }
                else
                {
                    // config.json not found — best-effort fallback on default port
                    Append("config.json not found — attempting fallback removal on port 5050.");
                    RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Actions Server Port 5050\"");
                    RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Shutdown Server Port 5050\"");
                }
            }
            catch (Exception ex)
            {
                Append("ERR while removing firewall rules: " + ex.Message);
            }
        }

        // ------------------------------------------------------------
        //  URL ACL helpers (require admin elevation to succeed)
        // ------------------------------------------------------------

        private void AddUrlAcl(int port)
        {
            string user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            RunCommand("netsh", $"http add urlacl url=http://+:{port}/ user=\"{user}\"");
        }

        private void RemoveUrlAcl(int port)
        {
            RunCommand("netsh", $"http delete urlacl url=http://+:{port}/");
        }

        // ------------------------------------------------------------
        //  UTILITIES
        // ------------------------------------------------------------

        private void RunCommand(string fileName, string arguments)
        {
            Append($"Running: {fileName} {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null)
            {
                Append("ERR: Failed to start process.");
                return;
            }

            string output = p.StandardOutput.ReadToEnd();
            string error = p.StandardError.ReadToEnd();

            p.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output))
                Append(output.Trim());

            if (!string.IsNullOrWhiteSpace(error))
                Append("ERR: " + error.Trim());

            Append($"Exit code: {p.ExitCode}");
        }
    }
}