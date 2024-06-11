using ABI_RC.Core.UI;
using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using Buttplug.Core;
using Buttplug.Core.Messages;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using static Buttplug.Core.Messages.ScalarCmd;

namespace AdultToyAPI
{
    public class AdultToyAPI : MelonMod, IAdultToyAPI
    {
        // Settings variables
        string IntifaceServerURI = "ws://localhost";
        int IntifaceServerPort = 12345;
        bool UseEmbeddedCLI = false;
        int SecondsBetweenConnectionAttempts = 15;
        int DeviceCommandTimeInterval = 50;
        bool Debug = false;
        string CLIVersion = "";
        bool UseLovenseConnect = true;
        bool UseBluetoothLE = true;
        bool RestartIntiface = false;
        bool UseSerial = false;
        bool UseHID = false;
        bool UseLovenseDongle = true;
        bool UseXinput = false;
        bool UseDeviceWebsocketServer = false;
        int DeviceWebsocketServerPort = 10000;

        // non-configurable settings
        string ButtplugCLIPath = "Executables/AdultToyAPI-intiface-engine.exe";

        // Internal Variables
        System.Timers.Timer ConnectToIntifaceTimer;
        System.Timers.Timer SendDeviceCommandsTimer;
        System.Timers.Timer ScanForDevicesTimer;
        System.Timers.Timer BatteryCheckTime;
        private ButtplugClient Buttplug = null;
        Process IntifaceProcess = null;
        bool ClosingApp = false;
        private ConcurrentQueue<ScalarSubcommand> DeviceCommandQueue = new ConcurrentQueue<ScalarSubcommand>();
        private ConcurrentDictionary<uint,AdultToy> KnownDevices = new ConcurrentDictionary<uint, AdultToy>();
        Task DeviceScanningTask;
        object DownloadLock = new object();
        object RunIntifaceCLILock = new object();
        
        

        //Public variables
        public event EventHandler<ErrorEventArgs> ErrorReceived;
        public event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;
        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;
        public event EventHandler<ServerDisconnectEventArgs> ServerDisconnect;
        public event EventHandler<ServerConnectedEventArgs> ServerConnected;

        public override void OnLateInitializeMelon()
        {
            try
            {
                InitSettings();
                LoadSettings();
                InitTimers();
                ExportIntifaceCLI();
            }
            catch(Exception e)
            {
                LoggerInstance.Error("Error During Initialization", e);
            }
        }
        private void ExportIntifaceCLI()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(AdultToyAPI));
            foreach (var name in assembly.GetManifestResourceNames())
            {
                DebugLog("embeded resource: " + name);
            }
            if(!Directory.Exists("Executables"))
            {
                Directory.CreateDirectory("Executables");
            }
            using (var stream = assembly.GetManifestResourceStream("AdultToyAPI.intiface-engine.exe"))
            {
                var file = new FileStream(ButtplugCLIPath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(file);
            }
        }

        private void InitTimers()
        {
            ConnectToIntifaceTimer = new System.Timers.Timer(SecondsBetweenConnectionAttempts * 1000);
            ConnectToIntifaceTimer.Elapsed += AttemptToConnect;
            ConnectToIntifaceTimer.AutoReset = true;
            ConnectToIntifaceTimer.Enabled = true;
            ConnectToIntifaceTimer.Start();

            SendDeviceCommandsTimer = new System.Timers.Timer(DeviceCommandTimeInterval);
            SendDeviceCommandsTimer.Elapsed += SendDeviceCommands;
            SendDeviceCommandsTimer.AutoReset = true;
            SendDeviceCommandsTimer.Enabled = true;
            SendDeviceCommandsTimer.Start();


            ScanForDevicesTimer = new System.Timers.Timer(15 * 1000);
            ScanForDevicesTimer.Elapsed += RunScanningTask;
            ScanForDevicesTimer.AutoReset = true;
            ScanForDevicesTimer.Enabled = true;
            ScanForDevicesTimer.Start();

            BatteryCheckTime = new System.Timers.Timer(60 * 1000);
            ScanForDevicesTimer.Elapsed += BatteryCheckTask;
            ScanForDevicesTimer.AutoReset = true;
            ScanForDevicesTimer.Enabled = true;
            ScanForDevicesTimer.Start();
        }

        private void BatteryCheckTask(object sender, ElapsedEventArgs e)
        {
            try
            {
                foreach (var device in KnownDevices)
                {
                    try
                    {
                        if (device.Value.HasBattery())
                        {
                            double battery = device.Value.GetBatteryLevelSync();
                            WarnLowBattery(battery, device.Value);
                        }
                    }catch(Exception ex)
                    {
                        this.ErrorLog("Unable to read battery of device - " + device.Value.GetName());
                        this.ErrorLog(ex.ToString());
                        AdultToy altDevice = null;
                        KnownDevices.TryRemove(device.Key, out altDevice);
                        return;
                    }
                }
            }catch(Exception error)
            {
                this.ErrorLog(error.ToString());
            }
        }

        private void WarnLowBattery(double battery, AdultToy device)
        {
            double batteryPercent = battery * 100;
            string batteryStr = $"{batteryPercent:00.0} percent";
            DebugLog(device.GetName() + " - " + batteryStr);
            if (battery<.10)
            {
                if(device.LastWarnedBattery!=battery)
                {
                    CohtmlHud.Instance.ViewDropTextImmediate("Low Battery", batteryStr, device.GetName());
                    device.LastWarnedBattery = battery;
                }
            }
        }

        private void RunScanningTask(object sender, ElapsedEventArgs e)
        {
            if (Buttplug == null)
                return;
            if (!Buttplug.Connected)
                return;
            if (DeviceScanningTask == null || DeviceScanningTask.IsCompleted)
            {
                DeviceScanningTask = Buttplug.StartScanningAsync();
            }
        }

        public override void OnPreferencesSaved()
        {
            base.OnPreferencesSaved();
            LoadSettings();
        }
        private void SendDeviceCommands(object sender, ElapsedEventArgs e)
        {
            try
            {
                
                if (DeviceCommandQueue.Count > 0)
                {
                    ScalarSubcommand command = null;
                    bool result = DeviceCommandQueue.TryDequeue(out command);
                    
                    if (result)
                    {
                        ButtplugClientDevice device = GetDeviceByIndex(command.Index);
                        SendDeviceCommand(command, device);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("Unable to send Device command", ex);
            }
        }

        private void CloseIntifaceCLI()
        {
            lock (RunIntifaceCLILock)
            {
                RemoveAllKnownDevices();
                if (IntifaceProcess != null)
                {
                    IntifaceProcess.Kill();
                }
            }
        }

        public override void OnApplicationQuit()
        {
            ClosingApp = true;
            base.OnApplicationQuit();
            Task t = Buttplug.DisconnectAsync();
            CloseIntifaceCLI();
            Buttplug.Dispose();
        }

        private ButtplugClientDevice GetDeviceByIndex(uint index)
        {
            foreach(var device in Buttplug.Devices)
            {
                if(device.Index==index)
                {
                    return device;
                }
            }
            throw new ApplicationException("Device Not Found");
        }

        private void SendDeviceCommand(ScalarSubcommand command, ButtplugClientDevice device)
        {
            device.ScalarAsync(command);
        }

        private void AttemptToConnect(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (!IsConnected())
                {
                    bool connected = TryToConnectToIntiface();
                    if (!connected && UseEmbeddedCLI)
                    {
                        LaunchIntifaceCLI();
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error("Unable To Connect To Intiface", ex);
            }
        }

        public bool TryToConnectToIntiface()
        {
            if (ClosingApp)
                return false;
            if (IsConnected())
            {
                DebugLog("Disconnecting...");
                Buttplug.DisconnectAsync();
            }
            try
            {
                /* removing known devices because Intiface has an odd issue.
                 * when using multiple devices and having connectivity problems, the Intiface may internally restart.
                 * If Intiface internally restarts & reconnects devices, their IDs may change.
                 * additionally, we wont get the device added messages.
                 * So I think it's best for now to pretend like we don't know what's connected anymore.
                */
                RemoveAllKnownDevices();
                
                Buttplug = new ButtplugClient(BuildInfo.Name);

                string ServerURI = IntifaceServerURI + ":" + IntifaceServerPort+"/";

                DebugLog($"Attempting to Connect to Intiface at {ServerURI}");
                Uri connectionTarget = new Uri(ServerURI);
                DebugLog("creating websocket...");
                ButtplugWebsocketConnector buttplugWebsocket = new ButtplugWebsocketConnector(connectionTarget);
            
                Task ConnectionTask = Buttplug.ConnectAsync(buttplugWebsocket);
                DebugLog("Connecting to "+ ServerURI);
                ConnectionTask.Wait();
                DebugLog("finished connecting");
                Buttplug.ServerDisconnect += OnButtplugServerDisconnect;
                Buttplug.DeviceAdded += OnButtplugDeviceAdded;
                Buttplug.DeviceRemoved += OnButtplugDeviceRemoved;
                Buttplug.ErrorReceived += OnButtplugErrorReceived;
                ServerConnected.Invoke(null, new ServerConnectedEventArgs());
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("unable to connect to intiface, is it running? aditional detail available if debug is enabled.");
                if(Debug)
                {
                    MelonLoader.MelonLogger.Error(e);
                }
                return false;
            }
            return true;
        }

        private void RemoveAllKnownDevices()
        {
            var listOfDevices = KnownDevices.ToList();
            foreach (var deviceToRemove in listOfDevices)
            {
                OnDeviceRemoved(this, deviceToRemove.Value);
            }
        }

        private void OnButtplugErrorReceived(object sender, ButtplugExceptionEventArgs e)
        {
            ErrorReceived.Invoke(sender, new ErrorEventArgs());
        }

        private void OnButtplugDeviceRemoved(object sender, Buttplug.Client.DeviceRemovedEventArgs e)
        {
            DebugLog("intiface lost device index: " + e.Device.Index);
            AdultToy device = new AdultToy(e.Device);
            OnDeviceRemoved(sender,device);
        }
        private void OnDeviceRemoved(object sender,AdultToy device)
        {
            uint index = (uint)device.GetIndex();
            if (KnownDevices.ContainsKey(index))
            {
                device = KnownDevices[index];
                KnownDevices.TryRemove(index, out device);
            }
            DebugLog("Toy Lost, "+device.GetName()+", sad");
            CohtmlHud.Instance.ViewDropTextImmediate("Toy Lost", device.GetName(), string.Empty);
            DeviceRemoved.Invoke(sender, new DeviceRemovedEventArgs(device));
        }

        private void OnButtplugDeviceAdded(object sender, Buttplug.Client.DeviceAddedEventArgs e)
        {
            if (Debug)
            {
                string json = JsonConvert.SerializeObject(e.Device);
                DebugLog(json);
            }
            AdultToy device = null;
            if (KnownDevices.ContainsKey(e.Device.Index))
            {
                device = KnownDevices[e.Device.Index];
            }
            else
            {
                device = new AdultToy(e.Device);
                KnownDevices[e.Device.Index] = device;
            }
            DebugLog("Toy Detected, " + device.GetName() + ", Nice!");
            CohtmlHud.Instance.ViewDropTextImmediate("Toy Detected", device.GetName(), "Nice!");
            DeviceAdded.Invoke(sender, new DeviceAddedEventArgs(device));
        }

        private void OnButtplugServerDisconnect(object sender, EventArgs e)
        {
            DebugLog("Server Disconnected");
            ServerDisconnect.Invoke(sender, new ServerDisconnectEventArgs());
        }

        public bool IsConnected()
        {
            if (Buttplug == null)
                return false;
            return Buttplug.Connected;
        }

        private void LaunchIntifaceCLI()
        {
            StartButtplugInstance();
        }

        private void LoadSettings()
        {
            DebugLog("Settings Updated");
            UseEmbeddedCLI = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseEmbeddedCLI");
            string newIntifaceServerURI = MelonPreferences.GetEntryValue<string>(BuildInfo.Name, "IntifaceServerURI");
            if(string.IsNullOrEmpty(newIntifaceServerURI) || string.Equals(newIntifaceServerURI, "null",StringComparison.InvariantCultureIgnoreCase))
            {
                newIntifaceServerURI = "ws:\\localhost"; // attempting to work around an issue where mellon preferences may not initialize correctly
            }
            if(!string.Equals(IntifaceServerURI,newIntifaceServerURI))
            {
                Task t = Buttplug.DisconnectAsync();
            }
            SecondsBetweenConnectionAttempts = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "SecondsBetweenConnectionAttempts");
            DeviceCommandTimeInterval = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "DeviceCommandTimeInterval");
            DeviceCommandTimeInterval = Clamp(DeviceCommandTimeInterval, 1, 100);
            Debug = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "Debug");
            CLIVersion = MelonPreferences.GetEntryValue<string>(BuildInfo.Name, "CLIVersion");
            IntifaceServerPort = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "IntifaceServerPort");
            UseLovenseConnect = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseLovenseConnect");
            UseBluetoothLE = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseBluetoothLE");
            UseSerial = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseSerial");
            UseHID = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseHID");
            UseLovenseDongle = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseLovenseDongle");
            UseXinput = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseXinput");
            UseDeviceWebsocketServer = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseDeviceWebsocketServer");
            DeviceWebsocketServerPort = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "DeviceWebsocketServerPort");
            RestartIntiface = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "RestartIntiface");
        }

        private void InitSettings()
        {
            MelonPreferences.CreateCategory(BuildInfo.Name, "Adult Toy API~");
            MelonPreferences.CreateEntry(BuildInfo.Name, "RestartIntiface", RestartIntiface, "Restart Intiface");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseEmbeddedCLI", UseEmbeddedCLI, "Use Embedded CLI");
            MelonPreferences.CreateEntry<string>(BuildInfo.Name, "IntifaceServerURI", IntifaceServerURI, "IntifaceServerURI");
            MelonPreferences.CreateEntry(BuildInfo.Name, "SecondsBetweenConnectionAttempts", SecondsBetweenConnectionAttempts, "Seconds Between Connection Attempts");
            MelonPreferences.CreateEntry(BuildInfo.Name, "DeviceCommandTimeInterval", DeviceCommandTimeInterval, "Device Command Time Interval");
            MelonPreferences.CreateEntry(BuildInfo.Name, "Debug", Debug, "Debug");
            MelonPreferences.CreateEntry<string>(BuildInfo.Name, "CLIVersion", CLIVersion, "CLI Version", description: "CLI EXE Version", is_hidden: true);
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntifaceServerPort", IntifaceServerPort, "Intiface Server Port");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseLovenseConnect", UseLovenseConnect, "Use Lovense Connect");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseBluetoothLE", UseBluetoothLE, "Use Bluetooth LE");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseSerial", UseSerial, "Use Serial");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseHID", UseHID, "Use HID");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseLovenseDongle", UseLovenseDongle, "Use Lovense Dongle");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseXinput", UseXinput, "Use Xinput");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseDeviceWebsocketServer", UseDeviceWebsocketServer, "Use Device Websocket Server");
            MelonPreferences.CreateEntry(BuildInfo.Name, "DeviceWebsocketServerPort", DeviceWebsocketServerPort, "Device Websocket Server Port");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            try
            {
                if (RestartIntiface)
                {
                    MelonPreferences.SetEntryValue(BuildInfo.Name, "RestartIntiface", false);
                    RestartIntiface = false;
                    CloseIntifaceCLI();
                    StartButtplugInstance();
                }
            }catch(Exception e)
            {
                MelonLoader.MelonLogger.Error("error trying to restart intiface",e);
            }
        }
        private void DebugLog(string message)
        {
            if (Debug)
            {
                MelonLoader.MelonLogger.Msg(System.ConsoleColor.Cyan, "[DEBUG] " + message);
            }
        }
        private void ErrorLog(string message)
        {
            if (Debug)
            {
                MelonLoader.MelonLogger.Msg(System.ConsoleColor.Red, "[ERROR] " + message);
            }
        }
        bool IsIntifaceCentralRunning()
        {
            foreach (var item in Process.GetProcesses())
            {
                try
                {
                    if (!item.HasExited)
                    {
                        if (item.ProcessName == "intiface_central.exe")
                        {
                            return true;
                        }
                    }
                }
                catch (Exception e)
                {
                    MelonLoader.MelonLogger.Error("Error while retrieving Processname of running application",e);
                }
            }
            return false;
        }
        private void ShutdownIntifaceCLI()
        {
            IntifaceProcess.Kill();
        }
        public void StartButtplugInstance()
        {
            try
            {
                lock (RunIntifaceCLILock)
                {
                    if(IsIntifaceCentralRunning())
                    {
                        MelonLoader.MelonLogger.Error("Intiface Central running. Using running instance");
                        return;
                    }
                    if(IntifaceProcess!=null && IntifaceProcess.HasExited==false)
                    {
                        return;
                    }
                    FileInfo target = new FileInfo(ButtplugCLIPath);
                    
                    string options = "";
                    if (UseLovenseConnect)
                    {
                        options += "--use-lovense-connect ";
                    }
                    if (UseBluetoothLE)
                    {
                        options += "--use-bluetooth-le ";
                    }
                    if(UseSerial)
                    {
                        options += "--use-serial ";
                    }
                    if (UseHID)
                    {
                        options += "--use-hid ";
                    }
                    if (UseLovenseDongle)
                    {
                        options += "--use-lovense-dongle-hid ";
                    }
                    if (UseXinput)
                    {
                        options += "--use-xinput ";
                    }
                    if (UseDeviceWebsocketServer)
                    {
                        options += "--use-device-websocket-server ";
                        options += "--device-websocket-server-port " + DeviceWebsocketServerPort;
                    }
                    
                    options += $" --websocket-port {IntifaceServerPort} --log error";
                    DebugLog("Intiface engine start parameters: " + options);
                    var startInfo = new ProcessStartInfo(target.FullName, options);
                    startInfo.UseShellExecute = false;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                    RemoveAllKnownDevices();
                    IntifaceProcess = Process.Start(startInfo);
                    IntifaceProcess.EnableRaisingEvents = true;
                    IntifaceProcess.OutputDataReceived += (sender, args) => DebugLog(args.Data);
                    IntifaceProcess.ErrorDataReceived += (sender, args) => MelonLoader.MelonLogger.Error(args.Data);

                    //IntifaceProcess.Exited += (_, _2) => StartButtplugInstance();
                }
            }
            catch (Exception ex)
            {
                //NotificationSystem.EnqueueNotification("Error", "Error starting intiface. Check log and try again", 5);
                MelonLoader.MelonLogger.Error("Error starting intiface engine. Check log and try again", ex);
            }
        }

        public List<IAdultToy> GetConnectedDevices()
        {
            List<IAdultToy> devicesToReturn = new List<IAdultToy>();
            if (IsConnected())
            {
                foreach(var unknownDevice in Buttplug.Devices)
                {
                    
                    if(!KnownDevices.ContainsKey(unknownDevice.Index))
                    {
                        KnownDevices[unknownDevice.Index] = new AdultToy(unknownDevice);
                    }
                }

                foreach (var device in KnownDevices)
                {
                    devicesToReturn.Add(device.Value);
                }
            }
            return devicesToReturn;
        }

        public void SetMotorSpeed(IAdultToy device, MotorType motor, float speed)
        {
            ActuatorType at = ConvertMotorType(motor);
            ScalarSubcommand subCommand = new ScalarSubcommand((uint)device.GetIndex(),Clamp(speed,0f,1f),at);
            if(DeviceCommandTimeInterval>0)
            {
                DeviceCommandQueue.Enqueue(subCommand);
            }
            else
            {
                ButtplugClientDevice bpdevice = GetDeviceByIndex((uint)device.GetIndex());
                SendDeviceCommand(subCommand, bpdevice);
            }
        }

        private ActuatorType ConvertMotorType(MotorType motor)
        {
            switch(motor)
            {
                case MotorType.Constrict:
                    return ActuatorType.Constrict;
                case MotorType.Inflate:
                    return ActuatorType.Inflate;
                case MotorType.Oscillate:
                    return ActuatorType.Oscillate;
                case MotorType.Position:
                    return ActuatorType.Position;
                case MotorType.Rotate:
                    return ActuatorType.Rotate;
                case MotorType.Vibrate:
                    return ActuatorType.Vibrate;
                default:
                    return ActuatorType.Vibrate;
            }
        }

        private double Clamp(float speed, float min, float max)
        {
            return Math.Max(Math.Min(speed, max), min);
        }
        private int Clamp(int speed, int min, int max)
        {
            return Math.Max(Math.Min(speed, max), min);
        }
    }
}
