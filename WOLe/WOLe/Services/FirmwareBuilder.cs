using WOLe.Provisioner.Models;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace WOLe.Provisioner.Services
{
    public class FirmwareBuilder
    {
        public string BuildFirmware(ProvisioningConfig config)
        {
            var exeDir = AppContext.BaseDirectory;
            var templatePath = Path.Combine(exeDir, "Firmware", "template.ino");

            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Firmware template not found", templatePath);

            var template = File.ReadAllText(templatePath, Encoding.UTF8);

            template = template.Replace("{{WIFI_SSID}}", Escape(config.WifiSsid));
            template = template.Replace("{{WIFI_PASSWORD}}", Escape(config.WifiPassword));
            template = template.Replace("{{APP_KEY}}", Escape(config.SinricAppKey));
            template = template.Replace("{{APP_SECRET}}", Escape(config.SinricAppSecret));
            template = template.Replace("{{SHUTDOWN_SECRET}}", Escape(config.ShutdownSecret));

            // Null-safe PC list
            var pcs = config.Pcs ?? new List<PcConfig>();

            int pcCount = Math.Min(5, pcs.Count);
            template = template.Replace("{{PC_COUNT}}", pcCount.ToString());

            string[] powerIds = new string[5];
            string[] macs = new string[5];
            string[] hosts = new string[5];
            int[] ports = new int[5];

            int maxEnhancedTotal = 5 * ProvisioningLimits.MaxEnhancedDevicesPerPc;
            var enhancedDeviceIds = new List<string>(maxEnhancedTotal);
            var enhancedDevicePcIndexes = new List<int>(maxEnhancedTotal);
            var enhancedActionOns = new List<string>(maxEnhancedTotal);
            var enhancedActionOffs = new List<string>(maxEnhancedTotal);

            for (int i = 0; i < 5; i++)
            {
                if (i < pcCount)
                {
                    var pc = pcs[i] ?? new PcConfig();

                    powerIds[i] = pc.PowerDeviceId ?? "";
                    macs[i] = pc.MacAddress ?? "";
                    hosts[i] = pc.IpAddress ?? "";
                    ports[i] = pc.Port;

                    var pcEnhanced = pc.EnhancedDevices ?? new List<EnhancedDeviceConfig>();

                    if (pcEnhanced.Count == 0 && !string.IsNullOrWhiteSpace(pc.ActionDeviceId))
                    {
                        pcEnhanced.Add(new EnhancedDeviceConfig
                        {
                            DeviceId = pc.ActionDeviceId,
                            ActionOn = pc.ActionOn,
                            ActionOff = pc.ActionOff
                        });
                    }

                    int addedForPc = 0;
                    foreach (var enhanced in pcEnhanced)
                    {
                        if (addedForPc >= ProvisioningLimits.MaxEnhancedDevicesPerPc ||
                            enhancedDeviceIds.Count >= maxEnhancedTotal)
                            break;

                        var id = enhanced?.DeviceId ?? "";
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        string actionOn =
                            !string.IsNullOrWhiteSpace(enhanced.ActionOn) ? enhanced.ActionOn :
                            !string.IsNullOrWhiteSpace(pc.ActionOn) ? pc.ActionOn :
                            !string.IsNullOrWhiteSpace(config.GlobalActionOn) ? config.GlobalActionOn :
                            "restart";

                        string actionOff =
                            !string.IsNullOrWhiteSpace(enhanced.ActionOff) ? enhanced.ActionOff :
                            !string.IsNullOrWhiteSpace(pc.ActionOff) ? pc.ActionOff :
                            !string.IsNullOrWhiteSpace(config.GlobalActionOff) ? config.GlobalActionOff :
                            "sleep";

                        enhancedDeviceIds.Add(id);
                        enhancedDevicePcIndexes.Add(i);
                        enhancedActionOns.Add(actionOn);
                        enhancedActionOffs.Add(actionOff);
                        addedForPc++;
                    }
                }
                else
                {
                    powerIds[i] = "";
                    macs[i] = "";
                    hosts[i] = "";
                    ports[i] = 0;
                }
            }

            template = template.Replace("{{PC1_POWER_DEVICE_ID}}", powerIds[0]);
            template = template.Replace("{{PC2_POWER_DEVICE_ID}}", powerIds[1]);
            template = template.Replace("{{PC3_POWER_DEVICE_ID}}", powerIds[2]);
            template = template.Replace("{{PC4_POWER_DEVICE_ID}}", powerIds[3]);
            template = template.Replace("{{PC5_POWER_DEVICE_ID}}", powerIds[4]);

            template = template.Replace("{{PC1_MAC}}", macs[0]);
            template = template.Replace("{{PC2_MAC}}", macs[1]);
            template = template.Replace("{{PC3_MAC}}", macs[2]);
            template = template.Replace("{{PC4_MAC}}", macs[3]);
            template = template.Replace("{{PC5_MAC}}", macs[4]);

            // ------------------------------------------------------------
            //  SHUTDOWN URLS (per PC)
            //  - Primary: per-PC IpAddress + Port
            //  - Fallback for Shutdown-only: ShutdownPcIp + ShutdownPcPort for PC1
            // ------------------------------------------------------------

            string[] shutdownUrls = new string[5];

            for (int i = 0; i < 5; i++)
            {
                if (i < pcCount &&
                    !string.IsNullOrWhiteSpace(hosts[i]) &&
                    ports[i] > 0)
                {
                    shutdownUrls[i] = $"http://{hosts[i]}:{ports[i]}";
                }
                else
                {
                    shutdownUrls[i] = "";
                }
            }

            // Fallback: Shutdown-only style config (no PC IpAddress, but ShutdownPcIp/Port set)
            if (pcCount > 0 &&
                string.IsNullOrWhiteSpace(shutdownUrls[0]) &&
                !string.IsNullOrWhiteSpace(config.ShutdownPcIp) &&
                (config.ShutdownPcPort ?? 0) > 0)
            {
                shutdownUrls[0] = $"http://{config.ShutdownPcIp}:{config.ShutdownPcPort.Value}";
            }

            template = template.Replace("{{PC1_SHUTDOWN_URL}}", shutdownUrls[0]);
            template = template.Replace("{{PC2_SHUTDOWN_URL}}", shutdownUrls[1]);
            template = template.Replace("{{PC3_SHUTDOWN_URL}}", shutdownUrls[2]);
            template = template.Replace("{{PC4_SHUTDOWN_URL}}", shutdownUrls[3]);
            template = template.Replace("{{PC5_SHUTDOWN_URL}}", shutdownUrls[4]);

            int enhancedCount = enhancedDeviceIds.Count;
            while (enhancedDeviceIds.Count < maxEnhancedTotal)
            {
                enhancedDeviceIds.Add("");
                enhancedDevicePcIndexes.Add(0);
                enhancedActionOns.Add("restart");
                enhancedActionOffs.Add("sleep");
            }

            template = template.Replace("{{ENHANCED_DEVICE_COUNT}}", enhancedCount.ToString());
            template = template.Replace("{{ENHANCED_DEVICE_IDS}}", BuildQuotedArray(enhancedDeviceIds));
            template = template.Replace("{{ENHANCED_DEVICE_PC_INDEXES}}", string.Join(", ", enhancedDevicePcIndexes));
            template = template.Replace("{{ENHANCED_ACTION_ON}}", BuildQuotedArray(enhancedActionOns));
            template = template.Replace("{{ENHANCED_ACTION_OFF}}", BuildQuotedArray(enhancedActionOffs));

            var outDir = Path.Combine(Path.GetTempPath(), "WOL-e", "WOL-e_Firmware");
            Directory.CreateDirectory(outDir);

            var outPath = Path.Combine(outDir, "WOL-e_Firmware.ino");
            File.WriteAllText(outPath, template, Encoding.UTF8);

            return outPath;
        }

        private static string Escape(string value)
        {
            if (value == null)
                return "";

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        private static string BuildQuotedArray(List<string> values)
        {
            var escaped = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                escaped[i] = $"\"{Escape(values[i] ?? "")}\"";
            return string.Join(", ", escaped);
        }
    }
}
