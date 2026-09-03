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

        private const int VolumeStepPercent = 15;

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

        private string ToolsRoot =>
            Path.Combine(AppContext.BaseDirectory, "Tools");

        private string ToolsServerExe =>
            Path.Combine(ToolsRoot, "ActionsServer.exe");

        // ── Install / Uninstall ────────────────────────────────────────────

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

        // ── Server binary ──────────────────────────────────────────────────

        private void StopRunningServer()
        {
            try
            {
                var procs = Process.GetProcessesByName("ActionsServer");
                if (procs.Length > 0)
                {
                    Append($"Stopping {procs.Length} running ActionsServer instance(s)...");
                    foreach (var proc in procs)
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
                        finally { proc.Dispose(); }
                    }
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
            StopRunningServer();

            File.Copy(ToolsServerExe, ServerExePath, true);
            Append($"Copied ActionsServer.exe to: {ServerExePath}");
        }

        // ── Local PC matching ──────────────────────────────────────────────

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

        // ── Server installation ────────────────────────────────────────────

        private void InstallUnifiedActionServer(ProvisioningConfig cfg, WizardMode mode)
        {
            string configPath  = Path.Combine(ActionsServiceRoot, "config.json");
            PcConfig? localPc  = MatchLocalPc(cfg);
            bool shutdownOnly  = mode == WizardMode.ShutdownOnly;

            int    port;
            string secret;

            if (shutdownOnly || localPc == null)
            {
                port   = cfg.ShutdownPcPort ?? 5050;
                secret = cfg.ShutdownSecret;
            }
            else
            {
                port   = localPc.Port > 0 ? localPc.Port : 5050;
                secret = cfg.ShutdownSecret;
            }

            // App binding paths always come from Pcs[0] — single source of truth.
            var pc1 = cfg.Pcs != null && cfg.Pcs.Count > 0 ? cfg.Pcs[0] : null;

            string app1 = pc1?.LaunchApp1Path ?? "";
            string app2 = pc1?.LaunchApp2Path ?? "";
            string app3 = pc1?.LaunchApp3Path ?? "";
            string app4 = pc1?.LaunchApp4Path ?? "";
            string app5 = pc1?.LaunchApp5Path ?? "";
            string app6 = pc1?.LaunchApp6Path ?? "";
            string app7 = pc1?.LaunchApp7Path ?? "";
            string app8 = pc1?.LaunchApp8Path ?? "";

            string esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

            File.WriteAllText(configPath,
$@"{{
  ""port"": {port},
  ""secret"": ""{esc(secret)}"",
  ""volumeStep"": {VolumeStepPercent},
  ""launchApp1"": ""{esc(app1)}"",
  ""launchApp2"": ""{esc(app2)}"",
  ""launchApp3"": ""{esc(app3)}"",
  ""launchApp4"": ""{esc(app4)}"",
  ""launchApp5"": ""{esc(app5)}"",
  ""launchApp6"": ""{esc(app6)}"",
  ""launchApp7"": ""{esc(app7)}"",
  ""launchApp8"": ""{esc(app8)}""
}}");

            Append($"Generated config.json: {configPath}");
            Append($"Volume step: {VolumeStepPercent}%");

            if (!string.IsNullOrWhiteSpace(app1)) Append("App 1 binding: configured");
            if (!string.IsNullOrWhiteSpace(app2)) Append("App 2 binding: configured");
            if (!string.IsNullOrWhiteSpace(app3)) Append("App 3 binding: configured");
            if (!string.IsNullOrWhiteSpace(app4)) Append("App 4 binding: configured");
            if (!string.IsNullOrWhiteSpace(app5)) Append("App 5 binding: configured");
            if (!string.IsNullOrWhiteSpace(app6)) Append("App 6 binding: configured");
            if (!string.IsNullOrWhiteSpace(app7)) Append("App 7 binding: configured");
            if (!string.IsNullOrWhiteSpace(app8)) Append("App 8 binding: configured");

            InstallScheduledTask();

            try
            {
                Append("Starting ActionsServer...");
                Process.Start(new ProcessStartInfo
                {
                    FileName        = ServerExePath,
                    UseShellExecute = false,
                    CreateNoWindow  = true
                });
                Append("ActionsServer started.");
            }
            catch (Exception ex)
            {
                Append("WARN: Could not auto-start ActionsServer after install: " + ex.Message);
            }
        }

        // ── Scheduled task ─────────────────────────────────────────────────

        private void InstallScheduledTask()
        {
            const string newTask = "WOL-e_Actions";

            Append("Removing ALL legacy tasks...");
            RemoveAllLegacyTasks();

            Append("Installing per-user hidden scheduled task...");

            var    user             = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var    registrationDate = DateTime.UtcNow.ToString("s") + "Z";
            string command          = ServerExePath;

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
            foreach (var name in new[] { "WOL-e_Shutdown", "WOL-e_Actions" })
            {
                Append($"Removing task: {name}");
                RunCommand("schtasks", $"/Delete /TN \"{name}\" /F");
            }
        }

        // ── Firewall ───────────────────────────────────────────────────────

        private void InstallFirewallRule(ProvisioningConfig cfg, WizardMode mode)
        {
            Append("Configuring Windows Firewall...");
            RemoveFirewallRule();

            RunCommand("netsh",
                $"advfirewall firewall add rule name=\"WOL-e Actions Server\" " +
                $"dir=in action=allow program=\"{ServerExePath}\" enable=yes");

            Append("Program-based firewall rule added.");

            if (cfg.Pcs != null)
            {
                foreach (var pc in cfg.Pcs)
                {
                    if (!string.IsNullOrWhiteSpace(pc.IpAddress) && pc.Port > 0)
                    {
                        RunCommand("netsh",
                            $"advfirewall firewall add rule name=\"WOL-e Actions Server Port {pc.Port}\" " +
                            $"dir=in action=allow protocol=TCP localport={pc.Port} profile=private enable=yes");

                        Append($"Firewall port rule added for PC port {pc.Port}");

                        try { AddUrlAcl(pc.Port); Append($"URL ACL added for port {pc.Port}"); }
                        catch (Exception ex)
                        { Append($"WARN: URL ACL add failed for port {pc.Port}: {ex.Message} (requires admin)"); }
                    }
                }
            }

            if (mode == WizardMode.ShutdownOnly && cfg.ShutdownPcPort != null)
            {
                int shutdownPort = cfg.ShutdownPcPort.Value;

                RunCommand("netsh",
                    $"advfirewall firewall add rule name=\"WOL-e Shutdown Server Port {shutdownPort}\" " +
                    $"dir=in action=allow protocol=TCP localport={shutdownPort} profile=private enable=yes");

                Append($"Firewall port rule added for shutdown-only port {shutdownPort}");

                try { AddUrlAcl(shutdownPort); Append($"URL ACL added for shutdown port {shutdownPort}"); }
                catch (Exception ex)
                { Append($"WARN: URL ACL add failed for shutdown port {shutdownPort}: {ex.Message} (requires admin)"); }
            }

            Append("Firewall configuration complete.");
        }

        private void RemoveFirewallRule()
        {
            Append("Removing firewall rules (if exist)...");

            RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Actions Server\" program=\"{ServerExePath}\"");
            RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Shutdown Server\" program=\"{ServerExePath}\"");

            try
            {
                string configPath = Path.Combine(ActionsServiceRoot, "config.json");

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    using var doc  = JsonDocument.Parse(json);
                    var       root = doc.RootElement;

                    if (root.TryGetProperty("port", out var portProp) &&
                        portProp.ValueKind == JsonValueKind.Number)
                    {
                        int port = portProp.GetInt32();
                        RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Actions Server Port {port}\"");
                        RunCommand("netsh", $"advfirewall firewall delete rule name=\"WOL-e Shutdown Server Port {port}\"");
                        RunCommand("netsh", $"advfirewall firewall delete rule protocol=TCP localport={port}");
                        Append($"Removed firewall rules for port {port}");

                        try { RemoveUrlAcl(port); Append($"Removed URL ACL for port {port}"); }
                        catch (Exception ex)
                        { Append($"WARN: RemoveUrlAcl failed for port {port}: {ex.Message} (requires admin)"); }
                    }
                }
                else
                {
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

        // ── URL ACL helpers ────────────────────────────────────────────────

        private void AddUrlAcl(int port)
        {
            string user = $"{Environment.UserDomainName}\\{Environment.UserName}";
            RunCommand("netsh", $"http add urlacl url=http://+:{port}/ user=\"{user}\"");
        }

        private void RemoveUrlAcl(int port)
        {
            RunCommand("netsh", $"http delete urlacl url=http://+:{port}/");
        }

        // ── Utilities ──────────────────────────────────────────────────────

        private void RunCommand(string fileName, string arguments)
        {
            Append($"Running: {fileName} {arguments}");

            var psi = new ProcessStartInfo
            {
                FileName               = fileName,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var p = Process.Start(psi);
            if (p == null) { Append("ERR: Failed to start process."); return; }

            string output = p.StandardOutput.ReadToEnd();
            string error  = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (!string.IsNullOrWhiteSpace(output)) Append(output.Trim());
            if (!string.IsNullOrWhiteSpace(error))  Append("ERR: " + error.Trim());

            Append($"Exit code: {p.ExitCode}");
        }
    }
}