using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.Media.Capture;
using Windows.Media.Render;
using Windows.UI.Core;

namespace AudioBridge.Models
{
    public class AudioGraphManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event Action<DeviceInformationModel> NewCaptureDeviceToSelect;
        public event Action<DeviceInformationModel> NewRenderDeviceToSelect;
        public event Action<DeviceInformationModel, DeviceInformationModel> NewAudioGraphCreated;
        public event Action<DeviceInformationModel, DeviceInformationModel> AudioGraphRemoved;

        public ObservableCollection<DeviceInformationModel> AudioRenders =
            new ObservableCollection<DeviceInformationModel>();
        public ObservableCollection<DeviceInformationModel> AudioCaptures =
            new ObservableCollection<DeviceInformationModel>();

        private StringBuilder logMessages = new StringBuilder();
        public string LogMessages => logMessages.ToString();

        private List<AudioGraph> audioGraphs = new List<AudioGraph>();
        private CoreDispatcher foregroundDispatcher;
        private DeviceWatcher renderDeviceWatcher;
        private DeviceWatcher captureDeviceWatcher;
        private List<string> removedDevices = new List<string>();

        public AudioGraphManager(CoreDispatcher dispatcher)
        {
            this.foregroundDispatcher = dispatcher;
            renderDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioRender);
            captureDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.AudioCapture);

            renderDeviceWatcher.Added += RenderDeviceWatcher_Added;
            renderDeviceWatcher.Updated += RenderDeviceWatcher_Updated;
            renderDeviceWatcher.Removed += RenderDeviceWatcher_Removed;
            renderDeviceWatcher.Stopped += RenderDeviceWatcher_Stopped;

            captureDeviceWatcher.Added += CaptureDeviceWatcher_Added;
            captureDeviceWatcher.Updated += CaptureDeviceWatcher_Updated;
            captureDeviceWatcher.Removed += CaptureDeviceWatcher_Removed;
            captureDeviceWatcher.Stopped += CaptureDeviceWatcher_Stopped;
        }

        public void AddLogMessage(string msg)
        {
            logMessages.Insert(0, $"{msg}\r\n");
            OnPropertyUpdated(nameof(LogMessages));
        }

        private void OnPropertyUpdated(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private async Task RunEvent(Action foregroundHandler)
        {
            await foregroundDispatcher.RunAsync(CoreDispatcherPriority.Low, () => foregroundHandler());
        }

        private async void CaptureDeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            await RunEvent(() => AddLogMessage("Capture device watcher stopped"));
        }

        private async void RenderDeviceWatcher_Stopped(DeviceWatcher sender, object args)
        {
            await RunEvent(() => AddLogMessage("Render device watcher stopped"));
        }

        private async void CaptureDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunEvent(() => {
                var device = AudioCaptures.Where(d => d.Id == args.Id).First();
                removedDevices.Add(device.Id);
                AudioCaptures.Remove(device);
                OnPropertyUpdated(nameof(AudioCaptures));
                AddLogMessage($"Capture Device {device.Name} has been removed");
            });
        }

        private async void CaptureDeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunEvent(async () => {
                var device = AudioCaptures.Where(d => d.Id == args.Id).First();
                await device.Update(args);
                OnPropertyUpdated(nameof(AudioCaptures));
                AddLogMessage($"Capture Device  {device.Name} has been updated");
            });
        }

        private async void CaptureDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation device)
        {
            await RunEvent(async () => {
                var devModel = await DeviceInformationModel.Create(device);
                AudioCaptures.Add(devModel);
                OnPropertyUpdated(nameof(AudioCaptures));
                if (devModel.IsAutoSelect)
                {
                    RunIdleAsync(() =>
                    {
                        NewCaptureDeviceToSelect?.Invoke(devModel);
                        AddLogMessage($"Notify to select capture {devModel.Name}");
                    });
                }
                AddLogMessage($"Capture Device {device.Name} has been added");
            });
        }

        private async void RenderDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunEvent(() =>
            {
                var device = AudioRenders.Where(d => d.Id == args.Id).First();
                removedDevices.Add(device.Id);
                AudioRenders.Remove(device); 
                OnPropertyUpdated(nameof(AudioRenders)); 
                AddLogMessage($"Render Device {device.Name} has been removed"); 
            });
        }

        private async void RenderDeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await RunEvent(async () => {
                var device = AudioRenders.Where(d => d.Id == args.Id).First();
                await device.Update(args);
                OnPropertyUpdated(nameof(AudioRenders));
                AddLogMessage($"Render Device {device.Name} has been updated");
            });
        }

        private async void RenderDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation device)
        {
            await RunEvent(async () => {
                var devModel = await DeviceInformationModel.Create(device);
                AudioRenders.Add(devModel);
                OnPropertyUpdated(nameof(AudioRenders));
                if (devModel.IsAutoSelect)
                {
                    RunIdleAsync(() =>
                    {
                        NewRenderDeviceToSelect(devModel);
                        AddLogMessage($"Notify to select render {devModel.Name}");
                    });
                }
                AddLogMessage($"Render Device {device.Name} has been added");
            });
        }

        private async void RunIdleAsync(Action a)
        {
            await foregroundDispatcher.RunIdleAsync((IdleDispatchedHandlerArgs e) => a());
        }

        public void StartWatchers()
        {
            renderDeviceWatcher.Start();
            captureDeviceWatcher.Start();
        }

        private async Task<AudioGraph> CreateAudioGraphAsync(DeviceInformation input, DeviceInformation output)
        {
            var settings = new AudioGraphSettings(AudioRenderCategory.Other);
            settings.PrimaryRenderDevice = output;

            var newGraphResult = await AudioGraph.CreateAsync(settings);
            if (newGraphResult.Status != AudioGraphCreationStatus.Success)
            {
                AddLogMessage($"Audio graph creation failed: {newGraphResult.ExtendedError.Message}");
                return null;
            }
            var inputResult = await newGraphResult.Graph.CreateDeviceInputNodeAsync(MediaCategory.Other, null, input);
            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                AddLogMessage($"Audio graph creation failed: {inputResult.ExtendedError.Message}");
                return null;
            }
            var outputResult = await newGraphResult.Graph.CreateDeviceOutputNodeAsync();
            if (inputResult.Status != AudioDeviceNodeCreationStatus.Success)
            {
                AddLogMessage($"Audio graph creation failed: {inputResult.ExtendedError.Message}");
                return null;
            }
            inputResult.DeviceInputNode.AddOutgoingConnection(outputResult.DeviceOutputNode);
            return newGraphResult.Graph;
        }

        public async Task SelectInputDeviceAsync(DeviceInformationModel previous, DeviceInformationModel input, IEnumerable<DeviceInformationModel> currentSelectedOutputs)
        {
            var newGraphs = new List<AudioGraph>();
            var currentSelected = currentSelectedOutputs.ToList();

            await RunEvent(async () =>
            {
                foreach (var graph in audioGraphs)
                {
                    var outputDevice = graph.PrimaryRenderDevice;
                    graph.Stop();
                    await RunEvent(() =>
                    {
                        AddLogMessage($"Graph stopped: {previous.Name} -> {outputDevice.Name}");
                    });
                    await UnsetAutoConnectIfNotRemoved(previous);
                    AudioGraphRemoved?.Invoke(previous, await DeviceInformationModel.Create(outputDevice));
                }

                foreach (var outputDevice in currentSelected)
                {
                    var newGraph = await CreateAudioGraphAsync(input.DeviceInformation, outputDevice.DeviceInformation);

                    newGraph.Start();
                    newGraphs.Add(newGraph);
                    await RunEvent(() =>
                    {
                        AddLogMessage($"Graph created: {input.Name} -> {outputDevice.Name}");
                    });
                    Settings.SetDeviceForAutoConnect(input, true);
                    NewAudioGraphCreated?.Invoke(input, outputDevice);
                }
                audioGraphs = newGraphs;
            });
        }
        private async Task UnsetAutoConnectIfNotRemoved(DeviceInformationModel device)
        {
            await RunEvent(() =>
            {
                if (removedDevices.Contains(device.Id))
                {
                    removedDevices.Remove(device.Id);
                } else
                {
                    Settings.SetDeviceForAutoConnect(device, false);
                }
            });
        }

        public async Task SelectOutputDevicesAsync(DeviceInformationModel currentSelectedInput, IEnumerable<DeviceInformationModel> added, IEnumerable<DeviceInformationModel> removed)
        {
            var removedDevices = removed.ToList();
            var addedDevices = added.ToList();
            await RunEvent(async () =>
            {
                foreach (var device in removedDevices)
                {
                    var graph = audioGraphs.Where(g => g.PrimaryRenderDevice.Id == device.Id).FirstOrDefault();
                    if (graph == null) { continue; }
                    var outputDevice = graph.PrimaryRenderDevice;
                    graph.Stop();
                    audioGraphs.Remove(graph);
                    await RunEvent(async () =>
                    {
                        AddLogMessage($"Graph stopped: {currentSelectedInput.Name} -> {outputDevice.Name}");
                        AudioGraphRemoved?.Invoke(currentSelectedInput, await DeviceInformationModel.Create(outputDevice));
                    });
                    await UnsetAutoConnectIfNotRemoved(device);
                }
                foreach (var output in addedDevices)
                {
                    if (currentSelectedInput == null) { break; }
                    var graph = await CreateAudioGraphAsync(currentSelectedInput.DeviceInformation, output.DeviceInformation);
                    audioGraphs.Add(graph);
                    graph.Start();
                    await RunEvent(() =>
                    {
                        AddLogMessage($"Graph created: {currentSelectedInput.Name} -> {output.Name}");
                        NewAudioGraphCreated?.Invoke(currentSelectedInput, output);
                    });
                    Settings.SetDeviceForAutoConnect(output, true);
                }
            });
        }

        public async void Play()
        {
            await RunEvent(() =>
            {
                foreach (var ag in audioGraphs)
                {
                    ag.Start();
                }
            });
        }

        public async void Pause()
        {
            await RunEvent(() =>
            {
                foreach (var ag in audioGraphs)
                {
                    ag.Stop();
                }
            });
        }
    }
}
