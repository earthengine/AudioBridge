using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Media.Imaging;

namespace AudioBridge.Models
{
    public class DeviceInformationModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string Id => DeviceInformation.Id;
        public string Name => DeviceInformation.Name;
        public BitmapImage GlyphBitmapImage { get; private set; }
        public DeviceInformation DeviceInformation { get; private set; }
        public bool IsAutoSelect => Settings.IsDeviceSetForAutoConnect(this);

        private DeviceInformationModel(DeviceInformation device)
        {
            this.DeviceInformation = device;
        }

        public static async Task<DeviceInformationModel> Create(DeviceInformation device)
        {
            var result = new DeviceInformationModel(device);
            await result.SetGlyphImage();
            return result;
        }

        public async Task Update(DeviceInformationUpdate deviceUpdate)
        {
            DeviceInformation.Update(deviceUpdate);

            await SetGlyphImage();

            OnPropertyChanged(nameof(Id));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(GlyphBitmapImage));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task SetGlyphImage()
        {
            var thumbnail = await DeviceInformation.GetGlyphThumbnailAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);
            this.GlyphBitmapImage = bitmap;
        }
    }
}
