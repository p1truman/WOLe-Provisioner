using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

        private string ToolsRoot =>
            Path.Combine(AppContext.BaseDirectory, "Tools");

        private string ToolsServerExe =>
            Path.Combine(ToolsRoot, "ActionsServer.exe");

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

        private void EnsureServerBinary()
        {
            Append("Checking ActionsServer.exe...");

            if (!File.Exists(ToolsServerExe))
            {
                Append("ERR: ActionsServer.exe not found in Tools folder.");
                return;
            }

            Directory.CreateDirectory(ActionsServiceRoot);

            File.Copy(ToolsServerExe, ServerExePath, true);
            Append($"Copied ActionsServer.exe to: {ServerExePath}");
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
            string launchApp1Path = "";
            string launchApp2Path = "";
            string launchApp3Path = "";
            string launchApp4Path = "";

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

                launchApp1Path = localPc.LaunchApp1Path ?? "";
                launchApp2Path = localPc.LaunchApp2Path ?? "";
                launchApp3Path = localPc.LaunchApp3Path ?? "";
                launchApp4Path = localPc.LaunchApp4Path ?? "";
            }

            string escapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

            File.WriteAllText(configPath, $@"{{
  ""port"": {port},
  ""secret"": ""{escapeJson(secret)}"",
  ""launchApp1"": ""{escapeJson(launchApp1Path)}"",
  ""launchApp2"": ""{escapeJson(launchApp2Path)}"",
  ""launchApp3"": ""{escapeJson(launchApp3Path)}"",
  ""launchApp4"": ""{escapeJson(launchApp4Path)}""
}}");

            Append($"Generated config.json: {configPath}");

            InstallScheduledTask();
        }

        // ------------------------------------------------------------
        //  SCHEDULED TASK (UPDATED TO RUN HIDDEN)
        // ------------------------------------------------------------

        private void InstallScheduledTask()
        {
            const string newTask = "WOL-e_Actions";

            Append("Removing ALL legacy tasks...");
            RemoveAllLegacyTasks();

            Append("Installing silent elevated scheduled task...");

            string logPath = Path.Combine(ActionsServiceRoot, "actions_log.txt");

            string taskCommand =
                $"\"{ServerExePath}\" >> \"{logPath}\" 2>&1";

            string args =
                $"/Create /TN \"{newTask}\" " +
                $"/TR \"{taskCommand}\" " +
                "/SC ONLOGON /RU \"SYSTEM\" /RL HIGHEST /NP /F";

            RunCommand("schtasks", args);

            Append("Silent SYSTEM-level scheduled task installed.");
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
        //  FIREWALL (UPDATED CLEANUP)
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
                            $"dir=in action=allow protocol=TCP localport={pc.Port} profile=private enable=yes");

                        Append($"Firewall port rule added for PC port {pc.Port}");
                    }
                }
            }

            // Shutdown-only mode port rule
            if (mode == WizardMode.ShutdownOnly && cfg.ShutdownPcPort != null)
            {
                int shutdownPort = cfg.ShutdownPcPort.Value;

                RunCommand("netsh",
                    $"advfirewall firewall add rule name=\"WOL-e Shutdown Server Port {shutdownPort}\" " +
                    $"dir=in action=allow protocol=TCP localport={shutdownPort} profile=private enable=yes");

                Append($"Firewall port rule added for shutdown-only port {shutdownPort}");
            }

            Append("Firewall configuration complete.");
        }

        private void RemoveFirewallRule()
        {
            Append("Removing firewall rules (if exist)...");

            // Remove program-based rules
            RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Actions Server\"");
            RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Shutdown Server\"");

            // Remove dynamic port rules (prefix match)
            RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Actions Server Port\"");
            RunCommand("netsh", "advfirewall firewall delete rule name=\"WOL-e Shutdown Server Port\"");
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
