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

        // non-configurable settings
        string ButtplugCLIPath = "Executables/intiface-engine.exe";

        // Internal Variables
        System.Timers.Timer ConnectToIntifaceTimer;
        System.Timers.Timer SendDeviceCommandsTimer;
        System.Timers.Timer ScanForDevicesTimer;
        private ButtplugClient Buttplug = null;
        Process IntifaceProcess = null;
        bool ClosingApp = false;
        private ConcurrentQueue<ScalarSubcommand> DeviceCommandQueue = new ConcurrentQueue<ScalarSubcommand>();
        Task DeviceScanningTask;
        object DownloadLock = new object();
        object RunIntifaceCLILock = new object();
        

        //Public variables
        public event EventHandler<ErrorEventArgs> ErrorReceived;
        public event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;
        public event EventHandler<DeviceAddedEventArgs> DeviceAdded;
        public event EventHandler<ServerDisconnectEventArgs> ServerDisconnect;

        public override void OnLateInitializeMelon()
        {
            base.OnLateInitializeMelon();
            InitSettings();
            LoadSettings();
            InitTimers();
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
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("unable to connect to intiface", e);
                return false;
            }
            return true;
        }

        private void OnButtplugErrorReceived(object sender, ButtplugExceptionEventArgs e)
        {
            ErrorReceived.Invoke(sender, new ErrorEventArgs());
        }

        private void OnButtplugDeviceRemoved(object sender, Buttplug.Client.DeviceRemovedEventArgs e)
        {
            DeviceRemoved.Invoke(sender, new DeviceRemovedEventArgs(new AdultToy(e.Device)));
        }

        private void OnButtplugDeviceAdded(object sender, Buttplug.Client.DeviceAddedEventArgs e)
        {
            DeviceAdded.Invoke(sender, new DeviceAddedEventArgs(new AdultToy(e.Device)));
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
            DownloadButtplugCLI();
            StartButtplugInstance();
        }

        private void LoadSettings()
        {
            UseEmbeddedCLI = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseEmbeddedCLI");
            IntifaceServerURI = MelonPreferences.GetEntryValue<string>(BuildInfo.Name, "IntifaceServerURI");
            SecondsBetweenConnectionAttempts = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "SecondsBetweenConnectionAttempts");
            DeviceCommandTimeInterval = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "DeviceCommandTimeInterval");
            Debug = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "Debug");
            CLIVersion = MelonPreferences.GetEntryValue<string>(BuildInfo.Name, "CLIVersion");
            IntifaceServerPort = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "IntifaceServerPort");
            UseLovenseConnect = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseLovenseConnect");
            UseBluetoothLE = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "UseBluetoothLE");
        }

        private void InitSettings()
        {
            MelonPreferences.CreateCategory(BuildInfo.Name, "Adult Toy API~");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseEmbeddedCLI", UseEmbeddedCLI, "Use Embedded CLI");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntifaceServerURI", IntifaceServerURI, "IntifaceServerURI");
            MelonPreferences.CreateEntry(BuildInfo.Name, "SecondsBetweenConnectionAttempts", SecondsBetweenConnectionAttempts, "Seconds Between Connection Attempts");
            MelonPreferences.CreateEntry(BuildInfo.Name, "DeviceCommandTimeInterval", DeviceCommandTimeInterval, "Device Command Time Interval");
            MelonPreferences.CreateEntry(BuildInfo.Name, "Debug", Debug, "Debug");
            MelonPreferences.CreateEntry<string>(BuildInfo.Name, "CLIVersion", CLIVersion, "CLI Version", description: "CLI EXE Version", is_hidden: true);
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntifaceServerPort", IntifaceServerPort, "Intiface Server Port");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseLovenseConnect", UseLovenseConnect, "Use Lovense Connect");
            MelonPreferences.CreateEntry(BuildInfo.Name, "UseBluetoothLE", UseBluetoothLE, "Use Bluetooth LE");
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
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
        private void DownloadButtplugCLI()
        {

            DebugLog("Checking if Intiface needs to be updated...");
            lock (DownloadLock)
            {
                var wc = new WebClient
                {
                    Headers = {
                    ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:87.0) Gecko/20100101 Firefox/87.0"
                }
                };

                if (CheckForIntifaceUpdate(wc) || !File.Exists("Executables/intiface-engine.exe"))
                {
                    DebugLog("New Intiface version detected! Downloading to Executables/intiface-engine.exe!");

                    try
                    {
                        byte[] bytes = wc.DownloadData("https://github.com/intiface/intiface-engine/releases/latest/download/intiface-engine-win-x64-Release.zip");
                        var stream = new MemoryStream(bytes);

                        var zipStream = new ZipArchive(stream).GetEntry("intiface-engine.exe").Open();
                        Directory.CreateDirectory("Executables");
                        var file = new FileStream(ButtplugCLIPath, FileMode.Create, FileAccess.Write);
                        zipStream.CopyTo(file);
                        stream.Dispose();
                        zipStream.Dispose();
                    }
                    catch (Exception e)
                    {
                        MelonLoader.MelonLogger.Error("Failed to download Buttplug Engine. If you start multiple instances of VRC this might occur", e);
                    }
                }
            }

        }
        private bool CheckForIntifaceUpdate(WebClient client)
        {
            try
            {
                var request = client.DownloadString("https://api.github.com/repos/intiface/intiface-engine/releases?per_page=1");
                var jsonArray = JsonConvert.DeserializeObject<JArray>(request);

                if (jsonArray != null)
                {
                    var entry = jsonArray.First;
                    string entryString = (string)entry["name"];

                    var result = !string.Equals(CLIVersion, entryString);

                    DebugLog($"Intiface version is {entryString} | Outdated: {result}");

                    MelonPreferences.SetEntryValue(BuildInfo.Name, "CLIVersion", entryString);
                    return result;
                }
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("An error occured while attempting to retrieve Intiface version info!", e);
            }

            return false;
        }
        bool IsIntifaceCentralRunning()
        {
            foreach (var item in Process.GetProcesses())
            {
                try
                {
                    if (item.ProcessName == "intiface_central.exe")
                    {
                        return true;
                    }
                }
                catch (Exception)
                {
                    MelonLoader.MelonLogger.Error("Error while retrieving Processname of running application");
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
                    options += $" --websocket-port {IntifaceServerPort}";
                    var startInfo = new ProcessStartInfo(target.FullName, options);
                    startInfo.UseShellExecute = true;
                    startInfo.WorkingDirectory = Environment.CurrentDirectory;
                    //startInfo.RedirectStandardError = true;
                    //startInfo.RedirectStandardOutput = true;

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
                foreach (var device in Buttplug.Devices)
                {
                    devicesToReturn.Add(new AdultToy(device));
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
    }
}
