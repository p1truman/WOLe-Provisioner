using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;

namespace WOLe.Provisioner.Services
{
    public class ArduinoCliInstaller
    {
        private const string Esp32Platform = "esp32:esp32";
        private const string Esp32Version = "2.0.17";

        private readonly string _cliRoot;
        private readonly string _cliExe;
        private readonly string _cliYaml;
        private readonly string _cliDataDir;
        private readonly string _cliPackagesEsp32;

        private readonly string _bundledEsp32;

        private readonly string[] _bundledLibraries = new string[]
        {
            "SinricPro",
            "Adafruit_NeoPixel",
            "ArduinoJson",
            "WebSockets"
        };

        public string RootPath => _cliRoot;

        public ArduinoCliInstaller()
        {
            _cliRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WOL-e"
            );

            _cliExe = Path.Combine(_cliRoot, "arduino-cli.exe");
            _cliYaml = Path.Combine(_cliRoot, "arduino-cli.yaml");
            _cliDataDir = Path.Combine(_cliRoot, "data");
            _cliPackagesEsp32 = Path.Combine(_cliDataDir, "packages", "esp32");

            var appRoot = AppContext.BaseDirectory ?? string.Empty;
            _bundledEsp32 = Path.Combine(appRoot, "Tools", "esp32-core", "packages", "esp32");
        }

        public async Task EnsureEnvironmentAsync(IProgress<string>? progress = null)
        {
            progress?.Report("Preparing Arduino CLI environment...");

            EnsureCliFiles(progress);

            progress?.Report("Running self-repair...");
            await SelfRepairAsync(progress);

            if (Directory.Exists(_bundledEsp32) && BundledVersionMatches())
            {
                progress?.Report("Using bundled ESP32 core (2.0.17)...");
                await InstallBundledCoreAsync(progress);
            }
            else
            {
                if (!Directory.Exists(_bundledEsp32))
                    progress?.Report("Bundled ESP32 core missing.");

                if (!BundledVersionMatches())
                    progress?.Report("Bundled ESP32 core version mismatch. Expected 2.0.17.");

                progress?.Report("Falling back to online install of 2.0.17...");
                await InstallOnlineCoreAsync(progress);
            }

            progress?.Report("Installing bundled libraries...");
            await InstallBundledLibrariesAsync(progress);

            progress?.Report("Adding Defender exclusions...");
            AddDefenderExclusions(progress);

            progress?.Report("Arduino environment ready.");
        }

        private void EnsureCliFiles(IProgress<string>? progress)
        {
            Directory.CreateDirectory(_cliRoot);

            string toolsRoot = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "Tools", "arduino-cli");
            string srcExe = Path.Combine(toolsRoot, "arduino-cli.exe");
            string srcYaml = Path.Combine(toolsRoot, "arduino-cli.yaml");

            if (!File.Exists(srcExe))
                throw new FileNotFoundException("Bundled arduino-cli.exe missing.", srcExe);

            if (!File.Exists(srcYaml))
                throw new FileNotFoundException("Bundled arduino-cli.yaml missing.", srcYaml);

            File.Copy(srcExe, _cliExe, true);
            File.Copy(srcYaml, _cliYaml, true);

            // ⭐ Bulletproof YAML rewrite (Option 1: quoted paths)
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                .Replace("\\", "/");

            string yaml = File.ReadAllText(_cliYaml)
                .Replace("\r\n", "\n")
                .Replace("\\", "/");

            string dataPath = $"{appData}/WOL-e/data";
            string userPath = $"{appData}/WOL-e/libraries";

            // Replace entire YAML lines safely
            yaml = Regex.Replace(yaml, @"data:\s*.*", $"data: \"{dataPath}\"");
            yaml = Regex.Replace(yaml, @"user:\s*.*", $"user: \"{userPath}\"");

            File.WriteAllText(_cliYaml, yaml, new UTF8Encoding(false));

            progress?.Report("Arduino CLI installed and YAML rewritten for current user.");

            if (!Directory.Exists(_cliDataDir))
                Directory.CreateDirectory(_cliDataDir);
        }

        private async Task SelfRepairAsync(IProgress<string>? progress)
        {
            KillProcessByName("arduino-cli");

            var staging = Path.Combine(_cliDataDir, "staging");
            TryDeleteDirectory(staging);

            Directory.CreateDirectory(_cliDataDir);

            await Task.CompletedTask;
        }

        private bool BundledVersionMatches()
        {
            var hw = Path.Combine(_bundledEsp32, "hardware", "esp32");
            if (!Directory.Exists(hw))
                return false;

            return Directory.GetDirectories(hw)
                .Select(Path.GetFileName)
                .Any(v => string.Equals(v, Esp32Version, StringComparison.OrdinalIgnoreCase));
        }

        private async Task InstallBundledCoreAsync(IProgress<string>? progress)
        {
            var target = _cliPackagesEsp32;

            if (!Directory.Exists(target))
            {
                CopyDirectory(_bundledEsp32, target);
                progress?.Report("Bundled ESP32 core installed.");
            }
            else
            {
                progress?.Report("ESP32 core already installed, skipping reinstall.");
            }

            await RunCliAsync("core list", progress);
        }

        private async Task InstallOnlineCoreAsync(IProgress<string>? progress)
        {
            await RunCliAsync("core update-index", progress);
            await RunCliAsync($"core install {Esp32Platform}@{Esp32Version}", progress);
        }

        private async Task InstallBundledLibrariesAsync(IProgress<string>? progress)
        {
            string libTarget = Path.Combine(_cliRoot, "libraries");
            Directory.CreateDirectory(libTarget);

            string toolsFolder = Path.Combine(AppContext.BaseDirectory ?? string.Empty, "Tools");

            foreach (string libName in _bundledLibraries)
            {
                string source = Path.Combine(toolsFolder, libName);
                string dest = Path.Combine(libTarget, libName);

                if (!Directory.Exists(source))
                {
                    progress?.Report($"ERROR: Missing bundled library: {libName}");
                    continue;
                }

                if (Directory.Exists(dest))
                {
                    progress?.Report($"Library already installed: {libName}");
                    continue;
                }

                CopyDirectory(source, dest);
                progress?.Report($"Installed library: {libName}");
            }

            await Task.CompletedTask;
        }

        private async Task RunCliAsync(string args, IProgress<string>? progress)
        {
            var psi = new ProcessStartInfo
            {
                FileName = _cliExe,
                Arguments = $"--config-file \"{_cliYaml}\" {args}",
                WorkingDirectory = _cliRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };

            string stderr = "";
            string stdout = "";

            proc.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    stdout += e.Data + "\n";
                    progress?.Report(e.Data);
                }
            };

            proc.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    stderr += e.Data + "\n";
                    progress?.Report("[ERR] " + e.Data);
                }
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0 && !IsHarmlessExit(args, proc.ExitCode, stderr))
            {
                throw new InvalidOperationException(
                    $"arduino-cli exited with code {proc.ExitCode}\nArgs: {args}\n{stderr}"
                );
            }

            progress?.Report($"✔ Completed: {args}");
        }

        private void AddDefenderExclusions(IProgress<string>? progress)
        {
            var paths = new[]
            {
                _cliRoot,
                _cliDataDir
            };

            foreach (var path in paths)
            {
                if (!Directory.Exists(path) && !File.Exists(path))
                    continue;

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Add-MpPreference -ExclusionPath '{path}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit();
                    progress?.Report($"Added Defender exclusion: {path}");
                }
                catch (Exception ex)
                {
                    progress?.Report($"Failed to add Defender exclusion for {path}: {ex.Message}");
                }
            }
        }

        private static void KillProcessByName(string name)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(true); } catch { }
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            var source = new DirectoryInfo(sourceDir);
            if (!source.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            foreach (var file in source.GetFiles("*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file.FullName);
                var targetPath = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                file.CopyTo(targetPath, true);
            }
        }

        private bool IsHarmlessExit(string args, int exitCode, string stderr)
        {
            if (exitCode == 1)
            {
                if (stderr.Contains("already installed", StringComparison.OrdinalIgnoreCase)) return true;
                if (stderr.Contains("up to date", StringComparison.OrdinalIgnoreCase)) return true;
                if (stderr.Contains("no updates available", StringComparison.OrdinalIgnoreCase)) return true;
                if (stderr.Contains("Error updating index", StringComparison.OrdinalIgnoreCase)) return true;

                if (args.StartsWith("core install") &&
                    stderr.Contains("installed", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
