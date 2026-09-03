using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WOLe.Provisioner.Models
{
    public class EnhancedDeviceRow : INotifyPropertyChanged
    {
        private string _header = "";
        private string _deviceId = "";

        public string Header
        {
            get => _header;
            set { _header = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Header))); }
        }

        public string DeviceId
        {
            get => _deviceId;
            set { _deviceId = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DeviceId))); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class PcRow : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = "";
        public string PowerDeviceId { get; set; } = "";

        public ObservableCollection<EnhancedDeviceRow> EnhancedDevices { get; set; } = new();

        public string MacAddress { get; set; } = "";
        public string IpAddress { get; set; } = "";
        public string Port { get; set; } = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public void RaiseEnhancedDevicesLayoutChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EnhancedDevices)));
        }
    }
}