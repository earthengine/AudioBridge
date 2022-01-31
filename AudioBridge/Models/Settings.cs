using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace AudioBridge.Models
{
    public struct DeviceSettings
    {
        public string Name;
        public bool AutoConnect;
        public int Volumn;
        public int Balance;
        public DeviceSettings(DeviceInformationModel di)
        {
            Name = di.Name;
            AutoConnect = false;
            Volumn = 100;
            Balance = 50;
        }
        public static DeviceSettings Parse(string s)
        {
            var values = s.Split(',');
            return new DeviceSettings
            {
                AutoConnect = values[1].ToLower() == "true",
                Balance = int.Parse(values[3]),
                Name = values[0],
                Volumn = int.Parse(values[2])
            };
        }
        public string ToString()
        {
            return $"{Name},{AutoConnect},{Volumn},{Balance}";
        }
    }

    public static class Settings
    {
        private static ApplicationDataContainer localSettings;
        private static Dictionary<string, DeviceSettings> devices;
        static Settings()
        {
            localSettings = ApplicationData.Current.LocalSettings;

            if (localSettings.Values.ContainsKey("devices"))
            {
                devices = DeserializeObject();
            }
            else
            {
                devices = new Dictionary<string, DeviceSettings>();
                SerializeObject(devices);
            }
        }
        private static void SerializeObject(Dictionary<string, DeviceSettings> devices)
        {
            var sb = new StringBuilder();
            foreach((var id, var dev) in devices)
            {
                sb.Append($"~{id}@{dev.ToString()}");
            }
            localSettings.Values["devices"] = sb.ToString();
        }
        private static Dictionary<string, DeviceSettings> DeserializeObject()
        {
            var result = new Dictionary<string, DeviceSettings>();
            var devices = localSettings.Values["devices"].ToString().Split('~');
            foreach (var device in devices)
            {
                if (!device.Any()) continue;
                var dev_value = device.Split('@');
                if (result.ContainsKey(dev_value[0]))
                {
                    result[dev_value[0]] = DeviceSettings.Parse(dev_value[1]);
                } 
                else
                {
                    result.Add(dev_value[0], DeviceSettings.Parse(dev_value[1]));
                }
            }            
            return result;
        }
        public static bool IsDeviceSetForAutoConnect(DeviceInformationModel di)
        {
            if (devices.ContainsKey(di.Id)) { return devices[di.Id].AutoConnect; }

            devices.Add(di.Id, new DeviceSettings(di));
            SerializeObject(devices);
            return false;
        }
        public static void SetDeviceForAutoConnect(DeviceInformationModel di, bool setValue)
        {
            var value = new DeviceSettings(di) { AutoConnect = setValue };
            if (devices.ContainsKey(di.Id)) { devices[di.Id] = value; }
            else { devices.Add(di.Id, value); }

            SerializeObject(devices);
        }
    }
}
