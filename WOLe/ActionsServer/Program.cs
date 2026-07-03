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
        public int Port { get; set; }
        public string Secret { get; set; } = "";
        public string LaunchApp1 { get; set; } = "";
        public string LaunchApp2 { get; set; } = "";
        public string LaunchApp3 { get; set; } = "";
        public string LaunchApp4 { get; set; } = "";
    }

    public static class Program
    {
        private static ServerConfig _config = new();
        private static string _configPath = "";
        private static string _logPath = "";

        public static int Main(string[] args)
        {
            try
            {
                InitializePaths();
                LoadConfig();
                Log($"WOL-e Actions Server starting on port {_config.Port}.");

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
            _logPath = Path.Combine(basePath, "actions_log.txt");
        }

        private static void LoadConfig()
        {
            if (!File.Exists(_configPath))
                throw new FileNotFoundException("config.json not found", _configPath);

            var json = File.ReadAllText(_configPath, Encoding.UTF8);

            var cfg = new ServerConfig();

            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                cfg.Port = GetInt(root, "Port", "port", 5050);
                cfg.Secret = GetString(root, "Secret", "secret");
                cfg.LaunchApp1 = GetString(root, "LaunchApp1", "launchApp1");
                cfg.LaunchApp2 = GetString(root, "LaunchApp2", "launchApp2");
                cfg.LaunchApp3 = GetString(root, "LaunchApp3", "launchApp3");
                cfg.LaunchApp4 = GetString(root, "LaunchApp4", "launchApp4");
            }

            if (string.IsNullOrWhiteSpace(cfg.Secret))
                throw new InvalidOperationException("Secret is missing in config.json.");

            _config = cfg;
        }

        private static string GetString(JsonElement root, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (root.TryGetProperty(key, out var prop) &&
                    prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString() ?? "";
                }
            }
            return "";
        }

        private static int GetInt(JsonElement root, string key1, string key2, int defaultValue)
        {
            if (root.TryGetProperty(key1, out var p1) && p1.TryGetInt32(out var v1))
                return v1;

            if (root.TryGetProperty(key2, out var p2) && p2.TryGetInt32(out var v2))
                return v2;

            return defaultValue;
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                var path = request.Url?.AbsolutePath ?? "/";
                var query = request.Url?.Query ?? "";

                var key = GetQueryParam(query, "key");

                // ------------------------------------------------------------
                //  LEGACY ESP32 / PYTHON SERVER COMPATIBILITY LAYER
                // ------------------------------------------------------------

                var action = GetQueryParam(query, "action");
                if (!string.IsNullOrEmpty(action))
                {
                    Log($"Legacy action detected: {action}");

                    switch (action.ToLowerInvariant())
                    {
                        case "shutdown": path = "/shutdown"; break;
                        case "restart": path = "/restart"; break;
                        case "sleep": path = "/sleep"; break;
                        case "hibernate": path = "/hibernate"; break;
                        case "lock": path = "/lock"; break;
                        case "screenoff": path = "/screenoff"; break;
                        case "launchapp1": path = "/launchapp1"; break;
                        case "launchapp2": path = "/launchapp2"; break;
                        case "launchapp3": path = "/launchapp3"; break;
                        case "launchapp4": path = "/launchapp4"; break;
                    }

                    // Old ESP32 never sent the key → inject it
                    key = _config.Secret;
                }

                // Legacy bare paths (no key)
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
                        case "/launchapp1":
                        case "/launchapp2":
                        case "/launchapp3":
                        case "/launchapp4":
                            Log("Legacy path detected without key — injecting secret.");
                            key = _config.Secret;
                            break;
                    }
                }

                // ------------------------------------------------------------
                //  SECRET VALIDATION (after legacy injection)
                // ------------------------------------------------------------
                if (!string.Equals(key, _config.Secret, StringComparison.Ordinal))
                {
                    WriteResponse(response, 403, "FORBIDDEN");
                    return;
                }

                Log($"Request: {path}");

                switch (path.ToLowerInvariant())
                {
                    case "/shutdown":
                        RunCommand("shutdown", "/s /t 0");
                        WriteResponse(response, 200, "SHUTDOWN");
                        return;

                    case "/restart":
                        RunCommand("shutdown", "/r /t 0");
                        WriteResponse(response, 200, "RESTART");
                        return;

                    case "/sleep":
                        RunCommand("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                        WriteResponse(response, 200, "SLEEP");
                        return;

                    case "/hibernate":
                        RunCommand("shutdown", "/h /t 0");
                        WriteResponse(response, 200, "HIBERNATE");
                        return;

                    case "/lock":
                        RunCommand("rundll32.exe", "user32.dll,LockWorkStation");
                        WriteResponse(response, 200, "LOCK");
                        return;

                    case "/screenoff":
                        RunCommand("powershell.exe",
                            "(Add-Type -MemberDefinition '[DllImport(\"user32.dll\")]public static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);' -Name Win32 -Namespace Native -PassThru)::SendMessage(-1, 0x0112, 0xF170, 2)");
                        WriteResponse(response, 200, "SCREENOFF");
                        return;

                    case "/launchapp1":
                        HandleLaunchApp(response, _config.LaunchApp1, "LAUNCH_APP_1");
                        return;

                    case "/launchapp2":
                        HandleLaunchApp(response, _config.LaunchApp2, "LAUNCH_APP_2");
                        return;

                    case "/launchapp3":
                        HandleLaunchApp(response, _config.LaunchApp3, "LAUNCH_APP_3");
                        return;

                    case "/launchapp4":
                        HandleLaunchApp(response, _config.LaunchApp4, "LAUNCH_APP_4");
                        return;

                    case "/ping":
                        WriteResponse(response, 200, "PONG");
                        return;

                    case "/status":
                        WriteResponse(response, 200, "OK");
                        return;

                    case "/version":
                        WriteResponse(response, 200, "WOL-e Actions Server 1.0");
                        return;

                    default:
                        WriteResponse(response, 404, "NOT_FOUND");
                        return;
                }
            }
            catch (Exception ex)
            {
                Log("ERR: " + ex);
                try { WriteResponse(context.Response, 500, "ERROR"); }
                catch { }
            }
        }

        private static void HandleLaunchApp(HttpListenerResponse response, string path, string label)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                WriteResponse(response, 400, $"{label}_NOT_CONFIGURED");
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                Process.Start(psi);
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
            if (string.IsNullOrEmpty(query))
                return "";

            if (query.StartsWith("?"))
                query = query.Substring(1);

            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }

            return "";
        }

        private static void WriteResponse(HttpListenerResponse response, int statusCode, string body)
        {
            response.StatusCode = statusCode;
            var bytes = Encoding.UTF8.GetBytes(body);
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using var output = response.OutputStream;
            output.Write(bytes, 0, bytes.Length);
        }

        private static void RunCommand(string fileName, string arguments)
        {
            try
            {
                Log($"Running: {fileName} {arguments}");

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null)
                {
                    Log("ERR: Failed to start process.");
                    return;
                }

                p.WaitForExit();
                Log($"Exit code: {p.ExitCode}");
            }
            catch (Exception ex)
            {
                Log("ERR RunCommand: " + ex);
            }
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
