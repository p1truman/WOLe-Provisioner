using System;
using System.Management;

namespace WOLe.Provisioner.Services
{
    public class SerialWatcher
    {
        private ManagementEventWatcher? _arrival;
        private ManagementEventWatcher? _removal;

        public event Action<string>? DeviceArrived;
        public event Action<string>? DeviceRemoved;

        public void Start()
        {
            var arrivalQuery = new WqlEventQuery(
                "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 2");

            var removalQuery = new WqlEventQuery(
                "SELECT * FROM Win32_DeviceChangeEvent WHERE EventType = 3");

            _arrival = new ManagementEventWatcher(arrivalQuery);
            _arrival.EventArrived += (s, e) =>
            {
                DeviceArrived?.Invoke("Device connected");
            };

            _removal = new ManagementEventWatcher(removalQuery);
            _removal.EventArrived += (s, e) =>
            {
                DeviceRemoved?.Invoke("Device removed");
            };

            _arrival.Start();
            _removal.Start();
        }

        public void Stop()
        {
            try { _arrival?.Stop(); } catch { }
            try { _removal?.Stop(); } catch { }
        }
    }
}
