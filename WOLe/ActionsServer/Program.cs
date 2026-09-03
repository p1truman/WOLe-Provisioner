using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace WOLe.ActionsServer
{
    public sealed class ServerConfig
    {
        public int    Port        { get; set; }
        public string Secret      { get; set; } = "";
        public int    VolumeStep  { get; set; } = 15;
        public string LaunchApp1  { get; set; } = "";
        public string LaunchApp2  { get; set; } = "";
        public string LaunchApp3  { get; set; } = "";
        public string LaunchApp4  { get; set; } = "";
        public string LaunchApp5  { get; set; } = "";
        public string LaunchApp6  { get; set; } = "";
        public string LaunchApp7  { get; set; } = "";
        public string LaunchApp8  { get; set; } = "";
    }

    public static class Program
    {
        private static ServerConfig _config     = new();
        private static string       _configPath = "";
        private static string       _logPath    = "";

        public static int Main(string[] args)
        {
            try
            {
                InitializePaths();
                LoadConfig();
                Log($"WOL-e Actions Server starting on port {_config.Port}. Volume step: {_config.VolumeStep}%");

                using var listener = new HttpListener();
                listener.Prefixes.Add($"http://+:{_config.Port}/");
                listener.Start();

                Log("Listener started. Waiting for requests...");

                while (true)
                {
                    var context = listener.GetContext();
                    HandleRequest(context);
                }
            }
            catch (Exception ex)
            {
                Log("FATAL: " + ex);
                return 1;
            }
        }

        private static void InitializePaths()
        {
            var appData = Environment.GetEnvironmentVariable("APPDATA")
                ?? throw new InvalidOperationException("APPDATA environment variable is not set.");

            var basePath = Path.Combine(appData, "WOL-e", "actions-service");
            Directory.CreateDirectory(basePath);

            _configPath = Path.Combine(basePath, "config.json");
            _logPath    = Path.Combine(basePath, "actions_log.txt");
        }

        private static void LoadConfig()
        {
            if (!File.Exists(_configPath))
                throw new FileNotFoundException("config.json not found", _configPath);

            var json = File.ReadAllText(_configPath, Encoding.UTF8);
            var cfg  = new ServerConfig();

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                cfg.Port       = GetInt   (root, "Port",       "port",       5050);
                cfg.VolumeStep = GetInt   (root, "VolumeStep", "volumeStep", 15);
                cfg.Secret     = GetString(root, "Secret",     "secret");
                cfg.LaunchApp1 = GetString(root, "LaunchApp1", "launchApp1");
                cfg.LaunchApp2 = GetString(root, "LaunchApp2", "launchApp2");
                cfg.LaunchApp3 = GetString(root, "LaunchApp3", "launchApp3");
                cfg.LaunchApp4 = GetString(root, "LaunchApp4", "launchApp4");
                cfg.LaunchApp5 = GetString(root, "LaunchApp5", "launchApp5");
                cfg.LaunchApp6 = GetString(root, "LaunchApp6", "launchApp6");
                cfg.LaunchApp7 = GetString(root, "LaunchApp7", "launchApp7");
                cfg.LaunchApp8 = GetString(root, "LaunchApp8", "launchApp8");
            }

            if (string.IsNullOrWhiteSpace(cfg.Secret))
                throw new InvalidOperationException("Secret is missing in config.json.");

            if (cfg.VolumeStep < 1)  cfg.VolumeStep = 1;
            if (cfg.VolumeStep > 50) cfg.VolumeStep = 50;

            _config = cfg;
        }

        private static string GetString(JsonElement root, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (root.TryGetProperty(key, out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? "";
            }
            return "";
        }

        private static int GetInt(JsonElement root, string key1, string key2, int defaultValue)
        {
            if (root.TryGetProperty(key1, out var p1) && p1.TryGetInt32(out var v1)) return v1;
            if (root.TryGetProperty(key2, out var p2) && p2.TryGetInt32(out var v2)) return v2;
            return defaultValue;
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request  = context.Request;
                var response = context.Response;

                var path  = request.Url?.AbsolutePath ?? "/";
                var query = request.Url?.Query ?? "";
                var key   = GetQueryParam(query, "key");

                // ── Legacy ESP32 / Python server compatibility ─────────────
                var action = GetQueryParam(query, "action");
                if (!string.IsNullOrEmpty(action))
                {
                    Log($"Legacy action detected: {action}");
                    path = action.ToLowerInvariant() switch
                    {
                        "shutdown"   => "/shutdown",
                        "restart"    => "/restart",
                        "sleep"      => "/sleep",
                        "hibernate"  => "/hibernate",
                        "lock"       => "/lock",
                        "screenoff"  => "/screenoff",
                        "mute"       => "/mute",
                        "unmute"     => "/unmute",
                        "volumeup"   => "/volumeup",
                        "volumedown" => "/volumedown",
                        "launchapp1" => "/launchapp1",
                        "launchapp2" => "/launchapp2",
                        "launchapp3" => "/launchapp3",
                        "launchapp4" => "/launchapp4",
                        "launchapp5" => "/launchapp5",
                        "launchapp6" => "/launchapp6",
                        "launchapp7" => "/launchapp7",
                        "launchapp8" => "/launchapp8",
                        _            => path
                    };
                    key = _config.Secret;
                }

                // ── Legacy bare paths (no key) ─────────────────────────────
                if (string.IsNullOrEmpty(key))
                {
                    switch (path.ToLowerInvariant())
                    {
                        case "/shutdown":
                        case "/restart":
                        case "/sleep":
                        case "/hibernate":
                        case "/lock":
                        case "/screenoff":
                        case "/mute":
                        case "/unmute":
                        case "/volumeup":
                        case "/volumedown":
                        case "/launchapp1":
                        case "/launchapp2":
                        case "/launchapp3":
                        case "/launchapp4":
                        case "/launchapp5":
                        case "/launchapp6":
                        case "/launchapp7":
                        case "/launchapp8":
                            Log("Legacy path detected without key — injecting secret.");
                            key = _config.Secret;
                            break;
                    }
                }

                // ── Secret validation ──────────────────────────────────────
                if (!string.Equals(key, _config.Secret, StringComparison.Ordinal))
                {
                    WriteResponse(response, 403, "FORBIDDEN");
                    return;
                }

                Log($"Request: {path}");

                switch (path.ToLowerInvariant())
                {
                    case "/shutdown":
                        RunCommand("shutdown", "/s /t 0 /f");
                        WriteResponse(response, 200, "SHUTDOWN");
                        return;

                    case "/restart":
                        RunCommand("shutdown", "/r /t 0 /f");
                        WriteResponse(response, 200, "RESTART");
                        return;

                    case "/sleep":
                        RunCommand("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                        WriteResponse(response, 200, "SLEEP");
                        return;

                    case "/hibernate":
                        RunCommand("shutdown", "/h /f");
                        WriteResponse(response, 200, "HIBERNATE");
                        return;

                    case "/lock":
                        RunCommand("rundll32.exe", "user32.dll,LockWorkStation");
                        WriteResponse(response, 200, "LOCK");
                        return;

                    case "/screenoff":
                        RunCommand("powershell.exe",
                            "(Add-Type -MemberDefinition '[DllImport(\"user32.dll\")]public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);' " +
                            "-Name Win32 -Namespace Native -PassThru)::SendMessage(-1, 0x0112, 0xF170, 2)");
                        WriteResponse(response, 200, "SCREENOFF");
                        return;

                    // ── Mute / Unmute ──────────────────────────────────────
                    case "/mute":
                        RunCommand("powershell.exe", BuildMuteScript(true));
                        WriteResponse(response, 200, "MUTE");
                        return;

                    case "/unmute":
                        RunCommand("powershell.exe", BuildMuteScript(false));
                        WriteResponse(response, 200, "UNMUTE");
                        return;

                    // ── Volume Up / Down ───────────────────────────────────
                    case "/volumeup":
                        RunCommand("powershell.exe", BuildVolumeScript(+_config.VolumeStep));
                        WriteResponse(response, 200, "VOLUME_UP");
                        return;

                    case "/volumedown":
                        RunCommand("powershell.exe", BuildVolumeScript(-_config.VolumeStep));
                        WriteResponse(response, 200, "VOLUME_DOWN");
                        return;

                    // ── Launch Apps ────────────────────────────────────────
                    case "/launchapp1":
                        HandleLaunchApp(response, _config.LaunchApp1, "LAUNCH_APP_1"); return;
                    case "/launchapp2":
                        HandleLaunchApp(response, _config.LaunchApp2, "LAUNCH_APP_2"); return;
                    case "/launchapp3":
                        HandleLaunchApp(response, _config.LaunchApp3, "LAUNCH_APP_3"); return;
                    case "/launchapp4":
                        HandleLaunchApp(response, _config.LaunchApp4, "LAUNCH_APP_4"); return;
                    case "/launchapp5":
                        HandleLaunchApp(response, _config.LaunchApp5, "LAUNCH_APP_5"); return;
                    case "/launchapp6":
                        HandleLaunchApp(response, _config.LaunchApp6, "LAUNCH_APP_6"); return;
                    case "/launchapp7":
                        HandleLaunchApp(response, _config.LaunchApp7, "LAUNCH_APP_7"); return;
                    case "/launchapp8":
                        HandleLaunchApp(response, _config.LaunchApp8, "LAUNCH_APP_8"); return;

                    case "/ping":
                        WriteResponse(response, 200, "PONG"); return;
                    case "/status":
                        WriteResponse(response, 200, "OK"); return;
                    case "/version":
                        WriteResponse(response, 200, "WOL-e Actions Server 1.1"); return;

                    default:
                        WriteResponse(response, 404, "NOT_FOUND"); return;
                }
            }
            catch (Exception ex)
            {
                Log("ERR: " + ex);
                try { WriteResponse(context.Response, 500, "ERROR"); } catch { }
            }
        }

        // ── Audio script builders ──────────────────────────────────────────

        private static string AudioTypeDefinition =>
            "using System.Runtime.InteropServices; " +
            "[Guid(\\\"5CDF2C82-841E-4546-9722-0CF74078229A\\\"), " +
            "InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] " +
            "public interface IAudioEndpointVolume { " +
            "void r1(); void r2(); void r3(); void r4(); " +
            "[PreserveSig] int SetMasterVolumeLevelScalar(float f, System.Guid g); " +
            "[PreserveSig] int GetMasterVolumeLevelScalar(out float f); " +
            "void r7(); void r8(); " +
            "[PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool b, System.Guid g); }";

        private static string GetAudioDevice =>
            "$t = [Type]::GetTypeFromCLSID([Guid]'BCDE0395-E52F-467C-8E3D-C4579291692E'); " +
            "$dev = [Activator]::CreateInstance($t);";

        private static string BuildMuteScript(bool mute) =>
            $"-NoProfile -Command \"Add-Type -TypeDefinition '{AudioTypeDefinition}'; " +
            $"{GetAudioDevice} " +
            $"$dev.SetMute(${(mute ? "true" : "false")}, [System.Guid]::Empty)\"";

        private static string BuildVolumeScript(int stepPercent)
        {
            double stepScalar = stepPercent / 100.0;
            string clamp = stepPercent > 0
                ? $"[Math]::Min(1.0, $v + {stepScalar})"
                : $"[Math]::Max(0.0, $v + {stepScalar})";

            return $"-NoProfile -Command \"Add-Type -TypeDefinition '{AudioTypeDefinition}'; " +
                   $"{GetAudioDevice} " +
                   $"[float]$v = 0; $dev.GetMasterVolumeLevelScalar([ref]$v); " +
                   $"$new = {clamp}; " +
                   $"$dev.SetMasterVolumeLevelScalar([float]$new, [System.Guid]::Empty)\"";
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static void HandleLaunchApp(HttpListenerResponse response, string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                WriteResponse(response, 400, $"{label}_NOT_CONFIGURED");
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                WriteResponse(response, 200, label);
            }
            catch (Exception ex)
            {
                Log($"ERR launching {label}: {ex}");
                WriteResponse(response, 500, $"{label}_FAILED");
            }
        }

        private static string GetQueryParam(string query, string name)
        {
            if (string.IsNullOrEmpty(query)) return "";
            if (query.StartsWith("?")) query = query.Substring(1);

            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 &&
                    string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return "";
        }

        private static void WriteResponse(HttpListenerResponse response, int statusCode, string body)
        {
            response.StatusCode      = statusCode;
            var bytes                = Encoding.UTF8.GetBytes(body);
            response.ContentType     = "text/plain; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using var output         = response.OutputStream;
            output.Write(bytes, 0, bytes.Length);
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                Log($"Running: {fileName} {arguments}");
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName        = fileName,
                    Arguments       = arguments,
                    UseShellExecute = false,
                    CreateNoWindow  = true
                });
                p?.WaitForExit();
                Log($"Exit code: {p?.ExitCode}");
            }
            catch (Exception ex) { Log("ERR RunCommand: " + ex); }
        }

        private static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                Console.WriteLine(line);
                if (!string.IsNullOrEmpty(_logPath))
                    File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }
}