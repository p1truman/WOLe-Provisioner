using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using WOLe.Provisioner.Models;

namespace WOLe.Provisioner.Services
{
    public static class ProvisioningState
    {
        private static bool _isLoaded;

        private static readonly string ConfigPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WOL-e",
                "provisioning.json");

        public static ProvisioningConfig Current { get; private set; } = new();

        public static void Ensure()
        {
            if (_isLoaded)
                return;

            if (File.Exists(ConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(ConfigPath);
                    Current = JsonSerializer.Deserialize<ProvisioningConfig>(json) ?? new ProvisioningConfig();
                }
                catch
                {
                    Current = new ProvisioningConfig();
                }
            }

            Normalize(Current);
            Save();
            _isLoaded = true;
        }

        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

            var json = JsonSerializer.Serialize(
                Current,
                new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(ConfigPath, json);
        }

        public static void Update(ProvisioningConfig cfg)
        {
            Current = cfg ?? new ProvisioningConfig();
            Normalize(Current);
            Save();
        }

        private static void Normalize(ProvisioningConfig cfg)
        {
            cfg.Pcs ??= new();

            // Ensure at least one PC exists, but DO NOT prefill IP, port, or enhanced devices
            if (cfg.Pcs.Count == 0)
            {
                cfg.Pcs.Add(new PcConfig
                {
                    Name = "Local PC",
                    IpAddress = "",
                    Port = 5050,
                    EnhancedDevices = new()
                });
            }

            // Ensure EnhancedDevices list exists
            foreach (var pc in cfg.Pcs)
            {
                pc.EnhancedDevices ??= new();

                // Remove legacy auto-migration that created unwanted devices
                if (!string.IsNullOrWhiteSpace(pc.ActionDeviceId) &&
                    !pc.EnhancedDevices.Any(d => d.DeviceId == pc.ActionDeviceId))
                {
                    // Do NOT auto-create enhanced devices anymore
                }
            }
        }
    }
}
