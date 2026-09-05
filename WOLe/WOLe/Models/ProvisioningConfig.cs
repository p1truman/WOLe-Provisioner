using System.Collections.Generic;

namespace WOLe.Provisioner.Models
{
    public class EnhancedDeviceConfig
    {
        public string DeviceId  { get; set; } = "";
        public string ActionOn  { get; set; } = "";
        public string ActionOff { get; set; } = "";
    }

    public class PcConfig
    {
        public string Name            { get; set; } = "";
        public string PowerDeviceId   { get; set; } = "";
        public string ActionDeviceId  { get; set; } = "";
        public List<EnhancedDeviceConfig> EnhancedDevices { get; set; } = new();
        public string MacAddress      { get; set; } = "";
        public string IpAddress       { get; set; } = "";
        public int    Port            { get; set; } = 5050;
        public string ActionOn        { get; set; } = "";
        public string ActionOff       { get; set; } = "";
        public string LaunchApp1Path  { get; set; } = "";
        public string LaunchApp2Path  { get; set; } = "";
        public string LaunchApp3Path  { get; set; } = "";
        public string LaunchApp4Path  { get; set; } = "";
        public string LaunchApp5Path  { get; set; } = "";
        public string LaunchApp6Path  { get; set; } = "";
        public string LaunchApp7Path  { get; set; } = "";
        public string LaunchApp8Path  { get; set; } = "";
    }

    public class ProvisioningConfig
    {
        public string WifiSsid                 { get; set; } = "";
        public string WifiPassword             { get; set; } = "";
        public string SinricAppKey             { get; set; } = "";
        public string SinricAppSecret          { get; set; } = "";
        public string ShutdownSecret           { get; set; } = "";
        public List<PcConfig> Pcs              { get; set; } = new();
        public string GlobalActionOn           { get; set; } = "";
        public string GlobalActionOff          { get; set; } = "";
        public string ShutdownPcIp             { get; set; } = "";
        public int?   ShutdownPcPort           { get; set; }

        // New independent volume steps (global provisioning-level settings)
        public int VolumeUpStepPercent         { get; set; } = 6;
        public int VolumeDownStepPercent       { get; set; } = 6;

        public bool   ShutdownServiceInstalled { get; set; }
        public bool   Step1Completed           { get; set; }
        public bool   Step2Completed           { get; set; }
        public bool   Step3Completed           { get; set; }
        public bool   Step4Completed           { get; set; }
    }
}