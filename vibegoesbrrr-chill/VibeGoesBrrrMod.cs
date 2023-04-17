using Buttplug.Client;
using MelonLoader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Buttplug.Client.Connectors.WebsocketConnector;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using static MelonLoader.MelonLogger;
using static VibeGoesBrrr.Util;
using System.Threading;
using static Buttplug.Core.Messages.ScalarCmd;
using System.Timers;

namespace VibeGoesBrrr
{
    public class VibeGoesBrrrMod : MelonMod
    {
        private bool Active = true;
        private bool ClosingApp = false;
        private bool TouchEnabled = true;
        private bool EnableBLERateLimit = false;
        private int BLERateLmit = 50;
        private bool ThrustEnabled = true;
        private bool TouchFeedbackEnabled = true;
        private bool AudioEnabled = false;
        private bool mIdleEnabled = false;
        private bool ExpressionParametersEnabled = true;
        private float mIdleIntensity = 5f;
        private float MinIntensity = 0f;
        private float MaxIntensity = 100f;
        private float IntensityCurveExponent = 1f;
        private string ServerURI = "ws://localhost:12345";
        private float UpdateFreq = 10f;
        private float ScanDuration = 15f;
        private float ScanWaitDuration = 5f;
        private bool SetupMode = false;
        private bool XSOverlayNotifications = true;
        private System.Timers.Timer ConnectionRetryTimer;
        private System.Timers.Timer BackgroundProcessingTimer;
        private AssetBundle Bundle;
        private Shader Shader;
        private GameObject OrthographicFrustum;
        private ButtplugClient Buttplug = null;
        private Task ScanTask = Task.CompletedTask;
        private int mMaxSeenDevices = 0;
        private Dictionary<uint, float[]> DeviceIntensities = new Dictionary<uint, float[]>();
        private HashSet<Sensor> TouchAndThrustSensors = new HashSet<Sensor>();
        private HashSet<Sensor> FeedbackSensors = new HashSet<Sensor>();
        private HashSet<Sensor> ExpressionSensors = new HashSet<Sensor>();
        private TouchZoneProvider SensorManager;
        private ThrustVectorProvider ThrustVectorManager;
        ButtplugWebsocketConnector buttplugWebsocket;// = new ButtplugWebsocketConnector(connectionTarget);
        // private AudioProvider mAudioProvider;
        private DeviceSensorBinder Binder;
        // private ExpressionParam<float> mGlobalParam;
        private XSNotify XSNotify;
        public override void OnApplicationStart()
        {
            XSNotify = XSNotify.Create();

            MelonPreferences.CreateCategory(BuildInfo.Name, "Vibe Goes Brrr~");
            MelonPreferences.CreateEntry(BuildInfo.Name, "Active", Active, "Active");
            MelonPreferences.CreateEntry(BuildInfo.Name, "EnableBLERateLimit", EnableBLERateLimit, "Enable BLE Rate Limit");
            MelonPreferences.CreateEntry(BuildInfo.Name, "BLERateLmit", BLERateLmit, "delay in milliseconds");
            MelonPreferences.CreateEntry(BuildInfo.Name, "TouchEnabled", TouchEnabled, "Touch Vibrations");
            MelonPreferences.CreateEntry(BuildInfo.Name, "ThrustEnabled", ThrustEnabled, "Thrust Vibrations");
            MelonPreferences.CreateEntry(BuildInfo.Name, "JustUseMyToys", DeviceSensorBinder.JustUseMyDevice, "JustUseMyToys");
            MelonPreferences.CreateEntry(BuildInfo.Name, "AudioEnabled", AudioEnabled, "Audio Vibrations");
            MelonPreferences.CreateEntry(BuildInfo.Name, "TouchFeedbackEnabled", TouchFeedbackEnabled, "Touch Feedback");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IdleEnabled", mIdleEnabled, "Idle Vibrations");
            MelonPreferences.SetEntryValue(BuildInfo.Name, "IdleEnabled", false);
            MelonPreferences.CreateEntry(BuildInfo.Name, "IdleIntensity", mIdleIntensity, "Idle Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "MinIntensity", MinIntensity, "Min Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "MaxIntensity", MaxIntensity, "Max Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "ExpressionParametersEnabled", ExpressionParametersEnabled, "Expression Parameters");
            MelonPreferences.CreateEntry(BuildInfo.Name, "SetupMode", SetupMode, "Setup Mode");

            MelonPreferences.SetEntryValue(BuildInfo.Name, "SetupMode", false);

            MelonPreferences.CreateEntry(BuildInfo.Name, "UpdateFreq", UpdateFreq, "Update Frequency");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponent2", IntensityCurveExponent, "Intensity Curve Exponent");
            MelonPreferences.CreateEntry(BuildInfo.Name, "XSOverlayNotifications", XSOverlayNotifications, "XSOverlay Notifications");
            // Hidden preferences
            MelonPreferences.CreateEntry(BuildInfo.Name, "ServerURI", ServerURI, "Server URI", null, is_hidden: false);
            MelonPreferences.CreateEntry(BuildInfo.Name, "ScanDuration2", ScanDuration, "Scan Duration", null, is_hidden: true);
            MelonPreferences.CreateEntry(BuildInfo.Name, "ScanWaitDuration2", ScanWaitDuration, "Scan Wait Duration", null, is_hidden: true);
            OnPreferencesSaved();

            // this.HarmonyInstance.PatchAll();
            CVRHooks.AddHooksIntoCVR(this.HarmonyInstance);
            // VRCHooks.AvatarIsReady += OnAvatarIsReady;

            SensorManager = new TouchZoneProvider();
            SensorManager.SensorDiscovered += OnSensorDiscovered;
            SensorManager.SensorLost += OnSensorLost;

            ThrustVectorManager = new ThrustVectorProvider();
            ThrustVectorManager.SensorDiscovered += OnSensorDiscovered;
            ThrustVectorManager.SensorLost += OnSensorLost;

            // mAudioProvider = new AudioProvider();
            // mAudioProvider.SensorDiscovered += OnSensorDiscovered;
            // mAudioProvider.SensorLost += OnSensorLost;

            Binder = new DeviceSensorBinder();
            Binder.BindingAdded += OnBindingAdded;
            Binder.BindingRemoved += OnBindingRemoved;
            Binder.AddSensorProvider(SensorManager);
            Binder.AddSensorProvider(ThrustVectorManager);
            // mBinder.AddSensorProvider(mAudioProvider);
            ConnectionRetryTimer = new System.Timers.Timer(20 * 1000);
            ConnectionRetryTimer.Elapsed += ConnectionRetryTimer_Elapsed;
            ConnectionRetryTimer.AutoReset = true;
            ConnectionRetryTimer.Enabled = true;
            ConnectionRetryTimer.Start();

            CreateBackgroundProcessingTimer();

            LoadAssets();
        }
        public void CreateBackgroundProcessingTimer()
        {
            if (BackgroundProcessingTimer != null)
            {
                // need to destroy the existing timer before creating a new one
                BackgroundProcessingTimer.Stop();
                BackgroundProcessingTimer.Enabled = false;
                BackgroundProcessingTimer.Dispose();
            }
            BackgroundProcessingTimer = new System.Timers.Timer(FrequencyToMiliseconds(UpdateFreq));
            BackgroundProcessingTimer.Elapsed += BackgroundProcessingTimer_Elapsed;
            BackgroundProcessingTimer.AutoReset = true;
            BackgroundProcessingTimer.Enabled = true;
            BackgroundProcessingTimer.Start();
        }
        private double FrequencyToMiliseconds(double updateFreq)
        {
            return (1 / updateFreq) * 1000;
        }

        private void BackgroundProcessingTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ProcessSensorsAndVibrateDevices();
        }

        private void ConnectionRetryTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            StartConnectionTask();
        }

        private void OnBindingRemoved(object sender, (ButtplugClientDevice Device, Sensor Sensor, int? Feature) e)
        {
            if (e.Sensor.OwnerType == SensorOwnerType.LocalPlayer)
            {
                Msg($"Device \"{e.Device.Name}\" unbound from sensor \"{e.Sensor.Name}\"");
            }
            else
            {
                DebugLog($"Device \"{e.Device.Name}\" unbound from {e.Sensor.OwnerType} sensor \"{e.Sensor.Name}\"");
            }
            _ = e.Device?.Stop();
            //mDeviceBattery.TryRemove(e.Device.Index, out double _); not needed?
        }

        private void OnBindingAdded(object sender, (ButtplugClientDevice Device, Sensor Sensor, int? Feature) e)
        {
            if (e.Sensor.OwnerType == SensorOwnerType.LocalPlayer)
            {
                // Send a lil' doot-doot 🎺 when successfully bound!
                DootDoot(e.Device);
                Msg($"Device \"{e.Device.Name}\" bound to sensor \"{e.Sensor.Name}\"");
            }
            else
            {
                Util.DebugLog($"Device \"{e.Device.Name}\" bound to {e.Sensor.OwnerType} sensor \"{e.Sensor.Name}\"");
            }
        }

        public override void OnApplicationQuit()
        {
            Util.DebugLog("On Quit Application");
            ClosingApp = true;
            ConnectionRetryTimer.Stop();
            ConnectionRetryTimer.Dispose();
            if (Buttplug.Connected)
            {
                Application.CancelQuit();
                DisconnectButtPlugClient();
            }
            Util.DebugLog("Ready To Quit Application");
        }
        private void LogTaskStatus(Task t, string message)
        {
            if (!t.IsCompleted || !t.IsFaulted)
                return;
            Util.Error("Task - " + message + " has faulted");
            Error(t.Exception);
        }
        public void InitializeButtplugClient()
        {
            try
            {
                DebugLog("Initializing Buttplug client");// will spam the log
                if (Buttplug != null)
                {
                    //Buttplug.Dispose();
                }

                Buttplug = new ButtplugClient(BuildInfo.Name);
                Buttplug.ServerDisconnect += OnButtplugServerDisconnect;
                Buttplug.DeviceAdded += OnButtplugDeviceAdded;
                Buttplug.DeviceRemoved += OnButtplugDeviceRemoved;
                Buttplug.ErrorReceived += OnButtplugErrorReceived;


                Binder.SetButtplugClient(Buttplug);

                ConnectButtplugClient();
            }
            catch (Exception e)
            {
                Error("error connecting to intiface central", e);
            }
        }

        private void OnButtplugErrorReceived(object sender, Buttplug.Core.ButtplugExceptionEventArgs e)
        {
            Warning($"Device error: {e.Exception.Message}");
        }

        private void OnButtplugDeviceRemoved(object sender, DeviceRemovedEventArgs e)
        {
            try
            {
                DeviceIntensities.Remove(e.Device.Index);
                Msg($"Device \"{e.Device.Name}\" disconnected");
            }
            catch (Exception error)
            {
                Error("OnButtplugDeviceRemoved", error);
            }
        }

        private void OnButtplugDeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            try
            {
                mMaxSeenDevices = Math.Max(mMaxSeenDevices, Buttplug.Devices.Length);
                var motorCount = e.Device.MessageAttributes.ScalarCmd.Length;
                if (e.Device.MessageAttributes.ScalarCmd.Length > 0)
                {
                    DeviceIntensities[e.Device.Index] = new float[motorCount];
                }


                double? battery = null;
                if (e.Device.HasBattery)
                {
                    //Task<double> batteryTask = e.Device.BatteryAsync();
                    //batteryTask.Wait();
                    //if (batteryTask.IsCompleted && !batteryTask.IsFaulted)
                    //{
                    //    battery = batteryTask.Result;
                    //}
                }

                string message;
                if (battery != null)
                {
                    message = $"Device \"{e.Device.Name}\" connected ({Math.Round((double)battery * 100)}%)";
                }
                else
                {
                    message = $"Device \"{e.Device.Name}\" connected";
                }
                var supporting = new List<string>();


                if (motorCount > 0)
                {
                    supporting.Add($"{motorCount} vibration motor{(motorCount > 1 ? "s" : "")}");
                    DeviceIntensities[e.Device.Index] = new float[motorCount];
                }


                if (supporting.Count > 0)
                {
                    message += ", supporting " + String.Join(", ", supporting) + ".";
                }
                else
                {
                    message += " (unsupported)";
                }
                Msg(message);

                DebugLog($"{e.Device.Name} supports the following messages:");
                foreach (var msgInfo in e.Device.MessageAttributes.ScalarCmd)
                {
                    DebugLog($"- {msgInfo.ActuatorType.ToString()} {msgInfo.FeatureDescriptor}");
                }


                if (battery != null)
                {
                    Notify($"<b>{e.Device.Name}</b> connected ({Math.Round((double)battery * 100)}%)");
                }
                else
                {
                    Notify($"<b>{e.Device.Name}</b> connected");
                }
            }
            catch (Exception error)
            {
                Error("On Buttplug Device Added", error);
            }
        }

        private void OnButtplugServerDisconnect(object sender, EventArgs e)
        {
            try
            {
                Warning($"Lost connection to Buttplug server!");
                Buttplug.DisconnectAsync();
            }
            catch (Exception error)
            {
                Error("On Server Disconnect", error);
            }
        }

        private void ConnectButtplugClient()
        {
            if (Buttplug == null || ClosingApp)
                return;
            if (Buttplug.Connected)
            {
                DebugLog("Disconnecting...");
                Buttplug.DisconnectAsync();
            }
            Msg($"Attempting to Connect to Intiface at {ServerURI}");
            Uri connectionTarget = new Uri(ServerURI);
            DebugLog("creating websocket...");//will spam the console

            //ButtplugWebsocketConnector conn = null;
            buttplugWebsocket = new ButtplugWebsocketConnector(connectionTarget);
            try
            {
                DebugLog("Connecting...");//will spam the console
                Task t = Buttplug.ConnectAsync(buttplugWebsocket);

                DebugLog("finished connecting");
            }
            catch (Exception e)
            {
                Error("unable to connect to intiface central", e);
                //await Task.Delay(5000);
            }
        }

        void LoadAssets()
        {
#if DEBUG
            Msg("Resources:");
            foreach (var res in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Msg($"- {res}");
            }
#endif

            var assetStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("assetbundle");
            if (assetStream == null)
            {
                Util.Error("Failed to load asset stream");
            }
            var tempStream = new MemoryStream((int)assetStream.Length);
            if (tempStream == null)
            {
                Util.Error("Failed to load temp stream");
            }
            assetStream.CopyTo(tempStream);
            Bundle = AssetBundle.LoadFromMemory(tempStream.ToArray(), 0);
            if (Bundle == null)
            {
                Util.Error("Failed to load asset bundle");
            }
            Bundle.hideFlags |= HideFlags.DontUnloadUnusedAsset;

#if DEBUG
            foreach (var name in Bundle.GetAllAssetNames())
            {
                Msg($"Asset: {name}");
            }
#endif
            Shader = Bundle.LoadAsset<Shader>("Assets/VibeGoesBrrr Internal/OrthographicDepth.shader");
            if (!Shader)
            {
                Util.Error("Failed to load shader");
            }
            Shader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            TouchSensor.Shader = Shader;
            var orthographicFrustumMat = Bundle.LoadAsset<Material>("Assets/VibeGoesBrrr Internal/OrthographicFrustum.mat");
            if (!orthographicFrustumMat)
            {
                Util.Error("Failed to load OrthographicFrustum material");
            }
            orthographicFrustumMat.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            OrthographicFrustum = Bundle.LoadAsset<GameObject>("Assets/VibeGoesBrrr Internal/OrthographicFrustum.prefab");
            if (!OrthographicFrustum)
            {
                Util.Error("Failed to load OrthographicFrustum prefab");
            }
            OrthographicFrustum.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            Bundle.LoadAllAssets();
        }


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            SensorManager.OnSceneWasInitialized();
            ThrustVectorManager.OnSceneWasInitialized();
        }

        void OnSensorDiscovered(object _, Sensor sensor)
        {
            // Sensors
            if (sensor.OwnerType == SensorOwnerType.LocalPlayer)
            {
                TouchAndThrustSensors.Add(sensor);
                ExpressionSensors.Add(sensor);
                Msg($"Discovered sensor \"{sensor.Name}\"");
            }
            if (sensor.OwnerType == SensorOwnerType.RemotePlayer)
            {
                FeedbackSensors.Add(sensor);
                // mExpressionSensors.Add(sensor);
            }
            if (sensor.OwnerType == SensorOwnerType.World)
            {
                TouchAndThrustSensors.Add(sensor);
                FeedbackSensors.Add(sensor);
                ExpressionSensors.Add(sensor);
            }

            // Frustums
            if (SetupMode)
            {
                if (sensor.OwnerType == SensorOwnerType.LocalPlayer)
                {
                    AddFrustumIfMissing(sensor);
                }
            }

            DebugLog($"Discovered sensor \"{sensor.Name}\" ({sensor.ToString()}) in {sensor.OwnerType.ToString()}");
        }

        void OnSensorLost(object _, Sensor sensor)
        {
            TouchAndThrustSensors.Remove(sensor);
            ExpressionSensors.Remove(sensor);
            FeedbackSensors.Remove(sensor);
            DebugLog($"Lost sensor {sensor.Name} in {sensor.OwnerType.ToString()}");
        }

        /// <summary>
        /// unknown why this is starting and stopping the scanning.
        /// </summary>
        /// <returns></returns>
        private async Task Scan()
        {
            await Task.Delay((int)(ScanWaitDuration * 1000));
            // DebugLog("Starting scan...");
            await Buttplug.StartScanningAsync();
            await Task.Delay((int)(ScanDuration * 1000));
            // DebugLog("Stopping scan.");
            await Buttplug.StopScanningAsync();

        }
        private void StartConnectionTask()
        {
            Util.DebugLog("background maintain buttplug connection");
            if (Active)
            {
                if (Buttplug == null || !Buttplug.Connected)
                {
                    Util.DebugLog("buttplug not connected, connecting....");
                    InitializeButtplugClient();
                }
            }
            else
            {
                if (Buttplug != null && Buttplug.Connected)
                {
                    DisconnectButtPlugClient();
                }
            }

        }

        private void DisconnectButtPlugClient()
        {
            Msg($"Disconnecting from Intiface at {ServerURI}");
            foreach (var device in Buttplug.Devices)
            {
                device.Stop();
            }
            buttplugWebsocket.DisconnectAsync();
            Buttplug.DisconnectAsync();
            Buttplug.Dispose();
        }

        private void ProcessSensorsAndVibrateDevices()
        {
            //Util.DebugLog("background thread");
            try
            {
                //Util.DebugLog("pre sensor manager");
                SensorManager.OnUpdate();
                //Util.DebugLog("pre Binder");
                Binder.OnUpdate();

                if (Buttplug == null || ClosingApp) return;
                if (!Buttplug.Connected)
                {
                    return;
                }
                //Util.DebugLog("background maintain scan task");
                // Scan forever!!!
                if (ScanTask == null || ScanTask.IsCompleted)
                {
                    ScanTask = Scan();
                }

                //Util.DebugLog("ThrustVectorManager");
                ThrustVectorManager.OnUpdate();
                //Util.DebugLog("beginning background calculations");

                var activeSensors = new HashSet<Sensor>();
                DriveDevices(activeSensors);
                CalculateHandTouchFeedback(activeSensors);
                DisableInactiveSensors(activeSensors);
            }
            catch (Exception e)
            {
                Error("Process Sensors And Vibrate Devices", e);
            }
        }
        private void DriveDevices(HashSet<Sensor> activeSensors)
        {
            var deviceIntensities = new Dictionary<uint, float?[]>();
            foreach (var device in Buttplug.Devices)
            {
                if (device.MessageAttributes.ScalarCmd.Length > 0)
                {
                    uint motorCount = (uint)device.MessageAttributes.ScalarCmd.Length;
                    deviceIntensities[device.Index] = new float?[motorCount];
                }
            }

            // Idle intensities
            if (mIdleEnabled)
            {
                foreach (var device in Buttplug.Devices)
                {
                    if (!deviceIntensities.ContainsKey(device.Index)) continue;
                    var motorIntensities = deviceIntensities[device.Index];
                    for (int motorIndex = 0; motorIndex < motorIntensities.Length; motorIndex++)
                    {
                        motorIntensities[motorIndex] = Mathf.Clamp(mIdleIntensity / 100, 0, 1);
                    }
                }
            }

            // Calculate sensor intensities / actuation
            if (TouchEnabled || ThrustEnabled)
            {
                foreach (var kv in Binder.Bindings)
                {
                    var device = kv.Key;
                    var bindings = kv.Value;
                    if (!deviceIntensities.ContainsKey(device.Index)) continue;

                    var motorIntensities = deviceIntensities[device.Index];
                    foreach (var (sensor, featureIndex) in bindings)
                    {
                        if (!TouchAndThrustSensors.Contains(sensor)) continue;
                        //Util.DebugLog($"{sensor.Name} is touch or thrust sensor");
                        if (!SetupMode && sensor.OwnerType == SensorOwnerType.World) continue;
                        //util.DebugLog($"{sensor.Name} is not world or we are in setup mode");
                        if (!sensor.Active) continue;
                        //Util.DebugLog($"{sensor.Name} is active");
                        if (!TouchEnabled && sensor is TouchSensor) continue;
                        //Util.DebugLog($"{sensor.Name} touch enabled or not touch sensor");
                        if (!ThrustEnabled && (sensor is Giver || sensor is Taker)) continue;
                        //Util.DebugLog($"{sensor.Name} thrust enabled or not giver/taker");
                        sensor.Enabled = true;
                        //Util.DebugLog($"calculating between {device.Name} and {sensor.Name}");
                        // Only vibrate at the maximum intensity of all accumulated affected sensors
                        if (featureIndex != null)
                        {
                            int motorIndex = (int)featureIndex;
                            motorIntensities[motorIndex] = (float)Math.Max(motorIntensities[motorIndex] ?? 0, intensity(motorIndex, sensor, motorIntensities));
                        }
                        else
                        {
                            // Vibrate all motors for unindexed sensors
                            for (int motorIndex = 0; motorIndex < motorIntensities.Length; motorIndex++)
                            {
                                float intensityValue = intensity(motorIndex, sensor, motorIntensities);
                                motorIntensities[motorIndex] = (float)Math.Max(motorIntensities[motorIndex] ?? 0, intensityValue);
                            }
                        }

                        activeSensors.Add(sensor);
                    }
                }
            }
            List<Task> commands = new List<Task>();
            // Send device commands
            foreach (var device in Buttplug.Devices)
            {
                if (!deviceIntensities.ContainsKey(device.Index)) continue;

                // Refrain from updating with the same values, since this seems to increase the chance of device hangs
                var motorIntensityValues = Array.ConvertAll(deviceIntensities[device.Index], i => i ?? 0);
                if (!motorIntensityValues.SequenceEqual(DeviceIntensities[device.Index]))
                {
                    List<ScalarSubcommand> subCommands = new List<ScalarSubcommand>();
                    // VIBRATE!!!
                    for (int motorIndex = 0; motorIndex < motorIntensityValues.Length; motorIndex++)
                    {
                        ScalarSubcommand subCommand = new ScalarSubcommand(
                            device.MessageAttributes.ScalarCmd[motorIndex].Index,
                            motorIntensityValues[motorIndex],
                            device.MessageAttributes.ScalarCmd[motorIndex].ActuatorType);
                        //subCommands.Add();
                        device.ScalarAsync(subCommand);
                        Util.DebugLog($"{device.Name}-{device.MessageAttributes.ScalarCmd[motorIndex].ActuatorType}: {motorIntensityValues[0]}");
                        if (EnableBLERateLimit)
                        {
                            Thread.Sleep(Math.Min(Math.Max(BLERateLmit, 0), 200));
                        }
                    }
                    //commands.Add(device.ScalarAsync(subCommands));
                    DeviceIntensities[device.Index] = motorIntensityValues;

                }
            }
            foreach (var t in commands)
            {
                // t.Wait();
            }
        }
        private void DisableInactiveSensors(HashSet<Sensor> activeSensors)
        {
            foreach (var sensor in SensorManager.Sensors)
            {
                if (!activeSensors.Contains(sensor))
                {
                    sensor.Enabled = false;
                }
            }
        }
        private void CalculateHandTouchFeedback(HashSet<Sensor> activeSensors)
        {
            // Calculate and send touch feedback
            if (TouchFeedbackEnabled && CVRHooks.LocalAvatar!=null)
            {
                foreach (var sensor in FeedbackSensors)
                {
                    if (!sensor.Active) continue;

                    float minDistance = 0f;

                    if (sensor is TouchSensor)
                    {
                        var touchSensor = sensor as TouchSensor;
                        minDistance = touchSensor.Camera.orthographicSize * 4;
                    }
                    else if (sensor is Giver)
                    {
                        var giver = sensor as Giver;
                        minDistance = giver.Length;
                    }
                    else
                    {
                        continue;
                    }

                    float leftDistance = float.MaxValue;
                    float rightDistance = float.MaxValue;
                    Animator playerLocalAvatarAnimator = CVRHooks.LocalAvatar.GetComponentInChildren<Animator>();
                    if (playerLocalAvatarAnimator != null)
                    {
                        var leftHand = playerLocalAvatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                        if (leftHand != null)
                        {
                            leftDistance = Vector3.Distance(sensor.GameObject.transform.position, leftHand.position);
                        }
                        else
                        {
                            Util.Error("Unable to find player left hand to calculate touch feedback");
                        }
                        var rightHand = playerLocalAvatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                        if (rightHand != null)
                        {
                            rightDistance = Vector3.Distance(sensor.GameObject.transform.position, rightHand.position);
                        }
                        else
                        {
                            Util.Error("Unable to find player right hand to calculate touch feedback");
                        }
                    }
                    else
                    {
                        Util.Error("Unable to find player Avatar to calculate touch feedback");
                    }

                    if (leftDistance <= minDistance || rightDistance <= minDistance)
                    {
                        if (sensor is TouchSensor)
                        {
                            var touchSensor = sensor as TouchSensor;
                            int playerNetworkMask = CVRLayersUtil.LayerToCullingMask(CVRLayers.PlayerNetwork);
                            if (SetupMode)
                            {
                                touchSensor.Camera.cullingMask |= playerNetworkMask;
                            }
                            else
                            {
                                touchSensor.Camera.cullingMask = playerNetworkMask;
                            }
                        }

                        sensor.Enabled = true;

                        float intensity = sensor.Value;
                        if (intensity > 0f)
                        {
                            if (leftDistance <= minDistance)
                            {
                                CVRHooks.VibratePlayerHands(0, .5f, 440, 5 + 20 * intensity, true);
                            }

                            if (rightDistance <= minDistance)
                            {
                                CVRHooks.VibratePlayerHands(0, .5f, 440, 5 + 20 * intensity, false);
                            }
                        }

                        activeSensors.Add(sensor);
                    }
                }
            }
        }
        private float intensity(int motorIndex, Sensor sensor, float?[] motorIntensities)
        {
            float clampedValue = Math.Max(0f, Math.Min(sensor.Value, 1f));
            float intensityCurve = 1f - (float)Math.Pow(1f - clampedValue, IntensityCurveExponent);
            // Subtract potential idle intensity so the sensor alpha acts fully in the remaining range
            return (motorIntensities[motorIndex] ?? 0f) + intensityCurve * Math.Max(0f, Math.Min(MaxIntensity / 100f - (motorIntensities[motorIndex] ?? 0f), 1f));
        }
        public override void OnUpdate()
        {
            try
            {

                //Util.DebugLog("pre reset one shot preferences");
                ResetOneShotPreferences();
            }
            catch (Exception e)
            {
                Error("OnUpdate", e);
            }
        }

        private OrthographicFrustum AddFrustumIfMissing(Sensor sensor)
        {
            if (sensor is TouchSensor)
            {
                var frustum = sensor.GameObject.GetComponentInChildren<OrthographicFrustum>();
                if (frustum == null)
                {
                    var obj = GameObject.Instantiate(OrthographicFrustum, sensor.GameObject.transform);
                    obj.AddComponent<OrthographicFrustum>();
                }
                return frustum;
            }

            return null;
        }

        public override void OnPreferencesSaved()
        {
            TouchEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "TouchEnabled");
            ThrustEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "ThrustEnabled");
            AudioEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "AudioEnabled");
            mIdleEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "IdleEnabled");
            ExpressionParametersEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "ExpressionParametersEnabled");
            TouchFeedbackEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "TouchFeedbackEnabled");
            mIdleIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IdleIntensity");
            MinIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "MinIntensity");
            MaxIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "MaxIntensity");
            ServerURI = MelonPreferences.GetEntryValue<string>(BuildInfo.Name, "ServerURI");
            UpdateFreq = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "UpdateFreq");
            DeviceSensorBinder.JustUseMyDevice = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "JustUseMyToys");
            ScanDuration = Math.Max(2, MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "ScanDuration2"));
            ScanWaitDuration = Math.Max(2, MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "ScanWaitDuration2"));
            IntensityCurveExponent = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponent2");
            XSOverlayNotifications = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "XSOverlayNotifications");
            Active = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "Active");
            EnableBLERateLimit = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "EnableBLERateLimit");
            BLERateLmit = MelonPreferences.GetEntryValue<int>(BuildInfo.Name, "BLERateLmit");
            bool setupMode = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "SetupMode");
            CreateBackgroundProcessingTimer();
            if (!SetupMode && setupMode)
            {
                if (SensorManager != null)
                {
                    foreach (var sensor in SensorManager.Sensors)
                    {
                        if (sensor != null && (sensor.OwnerType == SensorOwnerType.LocalPlayer))
                        {
                            AddFrustumIfMissing(sensor);
                        }
                    }
                }
            }
            else if (SetupMode && !setupMode)
            {
                foreach (var frustumObject in Resources.FindObjectsOfTypeAll(typeof(OrthographicFrustum)))
                {
                    var frustum = frustumObject as OrthographicFrustum;
                    if (frustum.gameObject != OrthographicFrustum)
                    {
                        GameObject.Destroy(frustum.gameObject);
                    }
                }
            }
            SetupMode = setupMode;
        }
        /// <summary>
        /// Reset one-shot preferences since we can't do this in OnPreferencesSaved without risking an infinite loop when a pref is pinned
        /// </summary>
        public void ResetOneShotPreferences()
        {
            if (MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "RestartIntiface"))
            {
                MelonPreferences.SetEntryValue(BuildInfo.Name, "RestartIntiface", false);
                Buttplug.DisconnectAsync();
            }
        }

        async void DootDoot(ButtplugClientDevice device)
        {
            try
            {
                await device.VibrateAsync(0.15);
                await Task.Delay(150);
                await device.VibrateAsync(0);
                await Task.Delay(150);
                await device.VibrateAsync(0.15);
                await Task.Delay(150);
                await device.VibrateAsync(0);
            }
            catch { }
        }

        void Notify(string message)
        {
            if (XSNotify != null && XSOverlayNotifications)
            {
                XSNotify.Notify(message);
            }
        }
    }
}
