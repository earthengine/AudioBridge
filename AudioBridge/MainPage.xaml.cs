using AudioBridge.Models;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Linq;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AudioBridge
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            var manager = App.CurrentApp.Manager;
            manager.NewCaptureDeviceToSelect += Manager_NewCaptureDeviceToSelect;
            manager.NewRenderDeviceToSelect += Manager_NewRenderDeviceToSelect;
            manager.NewAudioGraphCreated += Manager_NewAudioGraphCreated;
            manager.AudioGraphRemoved += Manager_AudioGraphRemoved;

            App.CurrentApp.Start();

            audioCaptureView.ItemsSource = manager.AudioCaptures;
            audioRenderView.ItemsSource = manager.AudioRenders;
            DataContext = manager;
        }

        private void Manager_NewAudioGraphCreated(DeviceInformationModel input, DeviceInformationModel output)
        {
            new ToastContentBuilder()
                .AddText("New Audio Graph Created")
                .AddText($"Input device: {input.Name}")
                .AddText($"Output device: {output.Name}")
                .Show();
        }

        private void Manager_AudioGraphRemoved(DeviceInformationModel input, DeviceInformationModel output)
        {
            new ToastContentBuilder()
                .AddText("Audio Graph Removed")
                .AddText($"Input device: {input.Name}")
                .AddText($"Output device: {output.Name}")
                .Show();
        }

        private void Manager_NewRenderDeviceToSelect(DeviceInformationModel obj)
        {
            var items = audioRenderView.Items.Cast<DeviceInformationModel>().Select((d, i) => (d, i + 1)).ToList();
            int idx = items.Where((d) => d.Item1.Id == obj.Id).FirstOrDefault().Item2;
            if (idx > 0)
            {
                App.CurrentApp.Manager.AddLogMessage($"Render devices index {idx} ({obj.Name}) selected");
                audioRenderView.SelectRange(new ItemIndexRange(idx - 1, 1));
            }
        }

        private void Manager_NewCaptureDeviceToSelect(DeviceInformationModel obj)
        {
            var items = audioCaptureView.Items.Cast<DeviceInformationModel>().Select((d, i) => (d, i + 1)).ToList();
            int idx = items.Where((d) => d.Item1.Id == obj.Id).FirstOrDefault().Item2;
            if (idx > 0)
            {
                App.CurrentApp.Manager.AddLogMessage($"Capture devices index {idx} ({obj.Name}) selected");
                audioCaptureView.SelectedIndex = idx - 1;
            }
        }

        private async void audioCaptureView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var previous = e.RemovedItems.FirstOrDefault() as DeviceInformationModel;
            await App.CurrentApp.Manager.SelectInputDeviceAsync(previous, audioCaptureView.SelectedItem as DeviceInformationModel, audioRenderView.SelectedItems.Cast<DeviceInformationModel>());
        }

        private async void audioRenderView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await App.CurrentApp.Manager.SelectOutputDevicesAsync(audioCaptureView.SelectedItem as DeviceInformationModel,
                                                   e.AddedItems.Cast<DeviceInformationModel>(),
                                                   e.RemovedItems.Cast<DeviceInformationModel>());
        }
    }
}
