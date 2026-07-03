namespace WOLe.Provisioner.Models
{
    public class EnhancedDeviceRow
    {
        public string Header { get; set; } = "";
        public string DeviceId { get; set; } = "";
    }

    public class PcRow
    {
        public string DisplayName { get; set; } = "";

        // Switch A (Power)
        public string PowerDeviceId { get; set; } = "";

        public System.Collections.ObjectModel.ObservableCollection<EnhancedDeviceRow> EnhancedDevices { get; set; } = new();

        public string MacAddress { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Port { get; set; } = "";
    }
}
