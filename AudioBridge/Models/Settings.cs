using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
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
    }

    public static class Settings
    {
        private static ApplicationDataContainer localSettings;
        private static DataContractSerializer settingsSerializer;
        static Settings()
        {
            localSettings = ApplicationData.Current.LocalSettings;
            settingsSerializer = new DataContractSerializer(typeof(Dictionary<string, DeviceSettings>));

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
            using (var ms = new MemoryStream())
            {
                using (var rd = new StreamReader(ms))
                {
                    settingsSerializer.WriteObject(ms, devices);
                    ms.Seek(0, SeekOrigin.Begin);
                    localSettings.Values["devices"] = rd.ReadToEnd();
                }
            }
        }
        private static Dictionary<string, DeviceSettings> DeserializeObject()
        {
            using (var ms = new MemoryStream())
            {
                using (var wr = new StreamWriter(ms))
                {
                    wr.Write(localSettings.Values["devices"].ToString());
                    wr.Flush();
                    ms.Seek(0, SeekOrigin.Begin);
                    return settingsSerializer.ReadObject(ms) as Dictionary<string, DeviceSettings>;
                }
            }
        }

        private static Dictionary<string, DeviceSettings> devices;

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
