using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WOLe.Provisioner.Services
{
    public class FlashService
    {
        private readonly string _arduinoCli;
        private readonly string _configFile;
        private readonly string _librariesPath;
        private readonly string _buildPath;

        public FlashService()
        {
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WOL-e"
            );

            _arduinoCli = Path.Combine(basePath, "arduino-cli.exe");
            _configFile = Path.Combine(basePath, "arduino-cli.yaml");

            _librariesPath = Path.Combine(basePath, "libraries")
                .Replace("\\", "/");

            _buildPath = Path.Combine(basePath, "build")
                .Replace("\\", "/");

            Directory.CreateDirectory(_buildPath);
        }

        public async Task FlashAsync(string sketchPath, string port, IProgress<string> progress)
        {
            if (!File.Exists(_arduinoCli))
                throw new FileNotFoundException("arduino-cli.exe not found", _arduinoCli);

            string sketchDir = Path.GetDirectoryName(sketchPath)
                ?? throw new Exception("Invalid sketch path");

            progress.Report("=== WOL-e Firmware Flashing ===");
            progress.Report($"Sketch: {sketchPath}");
            progress.Report($"Port: {port}");
            progress.Report("");

            //
            // 1. Compile firmware
            //
            progress.Report("Compiling firmware...");

            string compileArgs =
                $"compile " +
                $"--fqbn esp32:esp32:esp32s3 " +
                $"--libraries \"{_librariesPath}\" " +
                $"--build-path \"{_buildPath}\" " +        // ⭐ FORCE WOL-e BUILD FOLDER
                $"--build-property compiler.cpp.extra_flags=\"-std=gnu++17\" " +
                $"--config-file \"{_configFile}\" " +
                $"\"{sketchDir}\"";                        // ⭐ Compile folder, not file

            await RunProcessAsync(_arduinoCli, compileArgs, progress);

            //
            // 2. Upload firmware
            //
            progress.Report("");
            progress.Report("Uploading firmware...");

            string uploadArgs =
                $"upload " +
                $"--fqbn esp32:esp32:esp32s3 " +
                $"--port {port} " +
                $"--input-dir \"{_buildPath}\" " +         // ⭐ Upload from WOL-e build folder
                $"--config-file \"{_configFile}\" " +
                $"\"{sketchDir}\"";

            await RunProcessAsync(_arduinoCli, uploadArgs, progress);

            progress.Report("");
            progress.Report("Flash complete.");
        }

        private async Task RunProcessAsync(string exe, string args, IProgress<string> progress)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };

            proc.OutputDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    progress.Report(e.Data);
            };

            proc.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                    progress.Report("ERR: " + e.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            await proc.WaitForExitAsync();

            if (proc.ExitCode != 0)
                throw new Exception($"arduino-cli exited with code {proc.ExitCode}");
        }
    }
}
