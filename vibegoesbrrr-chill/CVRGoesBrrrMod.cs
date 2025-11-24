using MelonLoader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using static MelonLoader.MelonLogger;
using static CVRGoesBrrr.Util;
using System.Threading;
using System.Timers;
using AdultToyAPI;
using ABI_RC.Core.Player;
using CVRGoesBrrr.CVRIntegration;
using ABI_RC.Core.UI;
using ABI_RC.Core;

namespace CVRGoesBrrr
{
    public class CVRGoesBrrrMod : MelonMod
    {
        private bool Active = true;
        private bool TouchEnabled = true;
        private bool ThrustEnabled = true;
        private bool TouchFeedbackEnabled = true;
        private bool mIdleEnabled = false;
        private bool ExpressionParametersEnabled = true;
        private float mIdleIntensity = 5f;
        private float MinIntensity = 0f;
        private float MaxIntensity = 100f;
        //private float IntensityCurveExponent = 1f;
        private float UpdateFreq = 10f;
        private float IntensityCurveExponentOscillate = 1f;
        private float IntensityCurveExponentRotate = 1f;
        private float IntensityCurveExponentVibrate = 1f;
        private float IntensityCurveExponentConstrict = 1f;
        private float IntensityCurveExponentPosition = 1f;
        private float IntensityCurveExponentInflate = 1f;

        private bool SetupMode = false;
        private System.Timers.Timer BackgroundProcessingTimer;
        private AssetBundle Bundle;
        private Shader Shader;
        private GameObject OrthographicFrustum;
        private Task ScanTask = Task.CompletedTask;
        private int mMaxSeenDevices = 0;
        private Dictionary<int, float[]> DeviceIntensities = new Dictionary<int, float[]>();
        private ConcurrentQueue<Tuple<string, float>> AdvancedAvatarParameters = new ConcurrentQueue<Tuple<string, float>>();
        private HashSet<Sensor> TouchAndThrustSensors = new HashSet<Sensor>();
        private HashSet<Sensor> FeedbackSensors = new HashSet<Sensor>();
        private HashSet<Sensor> ExpressionSensors = new HashSet<Sensor>();
        private TouchZoneProvider SensorManager;
        private ThrustVectorProvider ThrustVectorManager;
        // private AudioProvider mAudioProvider;
        private DeviceSensorBinder Binder;
        // private ExpressionParam<float> mGlobalParam;
        private IAdultToyAPI ToyAPI;

        private HashSet<Sensor> PreviousActiveSensors;
        private Dictionary<string, float> AvatarParameterValues = new Dictionary<string, float>();

        public override void OnUpdate()
        {
            if (PlayerSetup.Instance?.AnimatorManager != null)
            {
                while (AdvancedAvatarParameters.Count > 0)
                {
                    Tuple<string, float> param = new Tuple<string, float>("", 0);
                    var success = AdvancedAvatarParameters.TryDequeue(out param);
                    if (success)
                    {
                        string parameterName = param.Item1;
                        float intensityValue = param.Item2;
                        float previousValue = -1;
                        if(AvatarParameterValues.ContainsKey(parameterName))
                        {
                            previousValue = AvatarParameterValues[parameterName];
                        }
                        if (previousValue != intensityValue)
                        {
                            AvatarParameterValues[parameterName] = intensityValue;
                            CVRHooks.SetAdvancedAvatarParameter(parameterName, intensityValue);
                        }
                    }
                }
            }
        }
        public override void OnLateInitializeMelon()
        {
            Util.Logger = LoggerInstance;

            var adultToyAPI = RegisteredMelons.FirstOrDefault(x => x.Info.Name.Equals("AdultToyAPI"));

            if (adultToyAPI == null)
            {
                Util.Error("AdultToyAPI was not detected! CVRGoesBrrr will not start up!");
                return;
            }

            ToyAPI = (IAdultToyAPI)adultToyAPI;
            ToyAPI.DeviceAdded += ToyAPI_DeviceAdded;
            ToyAPI.DeviceRemoved += ToyAPI_DeviceRemoved;
            ToyAPI.ErrorReceived += ToyAPI_ErrorReceived;
            ToyAPI.ServerDisconnect += ToyAPI_ServerDisconnect;
            ToyAPI.ServerConnected += ToyAPI_ServerConnected;

            Binder.SetButtplugClient(ToyAPI);

            Util.Info($"CVRGoesBrrr is starting up! AdultToyAPI {adultToyAPI.Info.Version} is detected!");

            MelonPreferences.CreateCategory(BuildInfo.Name, "CVR Goes Brrr~");
            MelonPreferences.CreateEntry(BuildInfo.Name, "Active", Active, "Active");
            MelonPreferences.CreateEntry(BuildInfo.Name, "TouchEnabled", TouchEnabled, "Touch Vibrations");
            MelonPreferences.CreateEntry(BuildInfo.Name, "ThrustEnabled", ThrustEnabled, "Thrust Vibrations");
            MelonPreferences.CreateEntry(BuildInfo.Name, "JustUseMyToys", DeviceSensorBinder.JustUseMyDevice, "JustUseMyToys");
            MelonPreferences.CreateEntry(BuildInfo.Name, "TouchFeedbackEnabled", TouchFeedbackEnabled, "Touch Feedback");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IdleEnabled", mIdleEnabled, "Idle Vibrations");
            MelonPreferences.SetEntryValue(BuildInfo.Name, "IdleEnabled", false);
            MelonPreferences.CreateEntry(BuildInfo.Name, "IdleIntensity", mIdleIntensity, "Idle Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "MinIntensity", MinIntensity, "Min Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "MaxIntensity", MaxIntensity, "Max Vibration Intensity %");
            MelonPreferences.CreateEntry(BuildInfo.Name, "ExpressionParametersEnabled", ExpressionParametersEnabled, "Expression Parameters");
            MelonPreferences.CreateEntry(BuildInfo.Name, "SetupMode", SetupMode, "Setup Mode");
            MelonPreferences.CreateEntry(BuildInfo.Name, "Debug", false, "Debug");
            MelonPreferences.CreateEntry(BuildInfo.Name, "DebugPerformance", false, "Debug Performance");
            MelonPreferences.SetEntryValue(BuildInfo.Name, "SetupMode", false);
            MelonPreferences.CreateEntry(BuildInfo.Name, "UpdateFreq", UpdateFreq, "Update Frequency");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentOscillate", IntensityCurveExponentOscillate, "Intensity Curve Exponent for Oscillate Motor");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentRotate", IntensityCurveExponentRotate, "Intensity Curve Exponent for Rotation Motor");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentVibrate", IntensityCurveExponentVibrate, "Intensity Curve Exponent for Vibration Motor");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentConstrict", IntensityCurveExponentOscillate, "Intensity Curve Exponent for Constriction Motor");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentPosition", IntensityCurveExponentPosition, "Intensity Curve Exponent for Position Motor");
            MelonPreferences.CreateEntry(BuildInfo.Name, "IntensityCurveExponentInflate", IntensityCurveExponentInflate, "Intensity Curve Exponent for Inflation Motor");
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
            Util.StartTimer("Computation Time");
            if (Active)
            {
                //This is background thread safe, no functions touch Unity objects
                ProcessSensorsAndVibrateDevices();
            }
            Util.StopTimer("Computation Time", 25);
        }

        private void ToyAPI_ServerConnected(object sender, ServerConnectedEventArgs e)
        {
            try
            {
                var deviceList = ToyAPI.GetConnectedDevices();
                foreach (var device in deviceList)
                {
                    var eventObj = new AdultToyAPI.DeviceAddedEventArgs(device);
                    this.ToyAPI_DeviceAdded(null, eventObj);
                }
            }
            catch (Exception ex)
            { 
                Error("Error Initializing devices that were already connected",ex); 
            }
        }
        private void ToyAPI_ServerDisconnect(object sender, ServerDisconnectEventArgs e)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Nothing needed here at the moment
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToyAPI_ErrorReceived(object sender, AdultToyAPI.ErrorEventArgs e)
        {
        }

        private void ToyAPI_DeviceRemoved(object sender, AdultToyAPI.DeviceRemovedEventArgs e)
        {
            try
            {
                DeviceIntensities.Remove(e.AdultToy.GetIndex());
                Util.Info($"Device \"{e.AdultToy.GetName()}\" disconnected");
            }
            catch (Exception error)
            {
                Error("OnButtplugDeviceRemoved", error);
            }
        }

        private void ToyAPI_DeviceAdded(object sender, AdultToyAPI.DeviceAddedEventArgs e)
        {
            try
            {

                mMaxSeenDevices = Math.Max(mMaxSeenDevices, e.AdultToy.GetIndex());
                var motorCount = e.AdultToy.MotorCount();
                if (motorCount > 0)
                {
                    DeviceIntensities[e.AdultToy.GetIndex()] = new float[motorCount];
                }

                string message;

                message = $"Device \"{e.AdultToy.GetName()}\" connected";

                var supporting = new List<string>();


                if (motorCount > 0)
                {
                    supporting.Add($"{motorCount} vibration motor{(motorCount > 1 ? "s" : "")}");
                    DeviceIntensities[e.AdultToy.GetIndex()] = new float[motorCount];
                }


                if (supporting.Count > 0)
                {
                    message += ", supporting " + String.Join(", ", supporting) + ".";
                }
                else
                {
                    message += " (unsupported)";
                }
                Util.Info(message);

                DebugLog($"{e.AdultToy.GetName()} supports the following messages:");
                foreach (var msgInfo in e.AdultToy.GetMotorTypes())
                {
                    DebugLog($"- {msgInfo.ToString()}");
                }
            }
            catch (Exception error)
            {
                Error("On Buttplug Device Added", error);
            }
        }


        private void OnBindingRemoved(object sender, (IAdultToy Device, Sensor Sensor, int? Feature) e)
        {
            if (e.Sensor.OwnerType == SensorOwnerType.LocalPlayer)
            {
                Util.Info($"Device \"{e.Device.GetName()}\" unbound from sensor \"{e.Sensor.Name}\"");
            }
            else
            {
                DebugLog($"Device \"{e.Device.GetName()}\" unbound from {e.Sensor.OwnerType} sensor \"{e.Sensor.Name}\"");
            }
        }

        private void OnBindingAdded(object sender, (IAdultToy Device, Sensor Sensor, int? Feature) e)
        {
            if (e.Sensor.OwnerType == SensorOwnerType.LocalPlayer)
            {
                // Send a lil' doot-doot 🎺 when successfully bound!
                DootDoot(e.Device);
                Util.Info($"Device \"{e.Device.GetName()}\" bound to sensor \"{e.Sensor.Name}\"");
            }
            else
            {
                Util.DebugLog($"Device \"{e.Device.GetName()}\" bound to {e.Sensor.OwnerType} sensor \"{e.Sensor.Name}\"");
            }
        }


        private void LogTaskStatus(Task t, string message)
        {
            if (!t.IsCompleted || !t.IsFaulted)
                return;
            Util.Error("Task - " + message + " has faulted");
            Error(t.Exception);
        }


        void LoadAssets()
        {
#if DEBUG
            Util.Info("Resources:");
            foreach (var res in Assembly.GetExecutingAssembly().GetManifestResourceNames())
            {
                Util.Info($"- {res}");
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
                Util.Info($"Asset: {name}");
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
                Util.Info($"Discovered sensor \"{sensor.Name}\"");
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

        private void ProcessSensorsAndVibrateDevices()
        {
            //Util.DebugLog("background thread");
            try
            {
                //Util.DebugLog("pre sensor manager");
                SensorManager.OnUpdate();
                //Util.DebugLog("pre Binder");
                Binder.OnUpdate();
                if (ToyAPI == null) return;

                //Util.DebugLog("ThrustVectorManager");
                ThrustVectorManager.OnUpdate(PreviousActiveSensors);
                //Util.DebugLog("beginning background calculations");

                var activeSensors = new HashSet<Sensor>();
                DriveDevices(activeSensors);
                CalculateHandTouchFeedback(activeSensors);
                Util.StartTimer("Disable Inactive Sensors");
                DisableInactiveSensors(activeSensors);
                Util.StopTimer("Disable Inactive Sensors", 3);
                PreviousActiveSensors = activeSensors;
            }
            catch (Exception e)
            {
                Error("Process Sensors And Vibrate Devices", e);
            }
        }
        private void DriveDevices(HashSet<Sensor> activeSensors)
        {
            var newDeviceIntensities = new Dictionary<int, float?[]>();
            List<IAdultToy> Devices = ToyAPI.GetConnectedDevices();
            foreach (var device in Devices)
            {
                int deviceMotorCount = device.MotorCount();
                if (deviceMotorCount > 0)
                {
                    int motorCount = deviceMotorCount;
                    newDeviceIntensities[device.GetIndex()] = new float?[motorCount];
                }
            }

            // Idle intensities
            if (mIdleEnabled)
            {
                foreach (var device in Devices)
                {
                    if (!newDeviceIntensities.ContainsKey(device.GetIndex())) continue;
                    var motorIntensities = newDeviceIntensities[device.GetIndex()];
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
                    if (!newDeviceIntensities.ContainsKey(device.GetIndex())) continue;

                    var motorIntensities = newDeviceIntensities[device.GetIndex()];
                    foreach (var (sensor, featureIndex) in bindings)
                    {
                        if(!sensor.Active)
                        {
                            sensor.RemoveAverageValues();
                            ResetAvatarParameter(sensor);
                        }
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
                        sensor.InitAverageValues((int)UpdateFreq);
                        //Util.DebugLog($"calculating between {device.Name} and {sensor.Name}");
                        // Only vibrate at the maximum intensity of all accumulated affected sensors
                        float intensityValue = 0;
                        if (featureIndex != null)
                        {
                            int motorIndex = (int)featureIndex;
                            MotorType motorType = device.GetMotorTypes()[motorIndex];
                            intensityValue = intensity(motorIndex, sensor, motorIntensities, motorType);
                            motorIntensities[motorIndex] = (float)Math.Max(motorIntensities[motorIndex] ?? 0, intensityValue);
                        }
                        else
                        {
                            // Vibrate all motors for unindexed sensors
                            for (int motorIndex = 0; motorIndex < motorIntensities.Length; motorIndex++)
                            {
                                MotorType motorType = device.GetMotorTypes()[motorIndex];
                                intensityValue = intensity(motorIndex, sensor, motorIntensities, motorType);
                                motorIntensities[motorIndex] = (float)Math.Max(motorIntensities[motorIndex] ?? 0, intensityValue);
                            }
                        }
                        SetAvatarParameter(sensor, intensityValue);
                        activeSensors.Add(sensor);
                    }
                }
            }
            List<Task> commands = new List<Task>();
            // Send device commands
            foreach (var device in Devices)
            {
                var motorTypes = device.GetMotorTypes();
                if (!newDeviceIntensities.ContainsKey(device.GetIndex())) continue;
                // Refrain from updating with the same values, since this seems to increase the chance of device hangs
                var motorIntensityValues = Array.ConvertAll(newDeviceIntensities[device.GetIndex()], i => i ?? 0);
                var motorCount = device.MotorCount();
                if (!DeviceIntensities.ContainsKey(device.GetIndex()))
                {
                    DeviceIntensities[device.GetIndex()] = new float[motorCount];
                }
                if (!motorIntensityValues.SequenceEqual(DeviceIntensities[device.GetIndex()]))
                {

                    for (int motorIndex = 0; motorIndex < motorIntensityValues.Length; motorIndex++)
                    {

                        ToyAPI.SetMotorSpeed(device, motorTypes[motorIndex], motorIntensityValues[motorIndex]);
                        // Util.DebugLog($"{device.GetName()}-{motorTypes[motorIndex].ToString()}: {motorIntensityValues[motorIndex]}");
                    }
                    DeviceIntensities[device.GetIndex()] = motorIntensityValues;

                }
            }
        }
        private void ResetAvatarParameter(Sensor sensor)
        {
            SetAvatarParameter(sensor, 0);
        }
        private void SetAvatarParameter(Sensor sensor, float intensityValue)
        {
            if (ExpressionParametersEnabled)
            {
                string parameterName = sensor.GetParameterName();
                string averageParameterName = parameterName + "Average";
                sensor.AddToAverage(intensityValue);
                float averageIntensity = sensor.GetAverage();
                AdvancedAvatarParameters.Enqueue(new Tuple<string, float>(parameterName, intensityValue));
                AdvancedAvatarParameters.Enqueue(new Tuple<string, float>(averageParameterName, averageIntensity));
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
            if (!TouchFeedbackEnabled || PlayerSetup.Instance?.AvatarObject == null) return;

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
                Animator playerLocalAvatarAnimator = PlayerSetup.Instance.Animator;
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
                            CVRHooks.VibratePlayerHands(0, .5f, 440, 5 + 20 * intensity, CVRHand.Left);
                        }

                        if (rightDistance <= minDistance)
                        {
                            CVRHooks.VibratePlayerHands(0, .5f, 440, 5 + 20 * intensity, CVRHand.Right);
                        }
                    }

                    activeSensors.Add(sensor);
                }
            }
        }
        private float intensity(int motorIndex, Sensor sensor, float?[] motorIntensities, MotorType motorType)
        {
            
            float clampedValue = Mathf.Clamp(sensor.Value, 0f, 1f);
            float exponent = GetExponent(motorType);
            float intensityCurve = 1f - (float)Math.Pow(1f - clampedValue, exponent);
            // Subtract potential idle intensity so the sensor alpha acts fully in the remaining range
            float result = (motorIntensities[motorIndex] ?? 0f) + intensityCurve * Math.Max(0f, Math.Min(MaxIntensity / 100f - (motorIntensities[motorIndex] ?? 0f), 1f));
            // Util.DebugLog($"Calculating intensity for {sensor.Name} {motorType} = {result}");
            return result;
        }

        private float GetExponent(MotorType motorType)
        {
            float exponent = 0;
            switch (motorType)
            {
                case MotorType.Oscillate:
                    exponent = IntensityCurveExponentOscillate;
                    break;
                case MotorType.Rotate:
                    exponent = IntensityCurveExponentRotate;
                    break;
                case MotorType.Vibrate:
                    exponent = IntensityCurveExponentVibrate;
                    break;
                case MotorType.Constrict:
                    exponent = IntensityCurveExponentConstrict;
                    break;
                case MotorType.Position:
                    exponent = IntensityCurveExponentPosition;
                    break;
                case MotorType.Inflate:
                    exponent = IntensityCurveExponentInflate;
                    break;
            }
            // Util.DebugLog($"Exponent for {motorType} = {exponent}");
            return exponent;
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
            mIdleEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "IdleEnabled");
            ExpressionParametersEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "ExpressionParametersEnabled");
            TouchFeedbackEnabled = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "TouchFeedbackEnabled");
            mIdleIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IdleIntensity");
            MinIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "MinIntensity");
            MaxIntensity = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "MaxIntensity");
            UpdateFreq = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "UpdateFreq");
            DeviceSensorBinder.JustUseMyDevice = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "JustUseMyToys");
            IntensityCurveExponentOscillate = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentOscillate");
            IntensityCurveExponentRotate = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentRotate");
            IntensityCurveExponentVibrate = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentVibrate");
            IntensityCurveExponentConstrict = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentConstrict");
            IntensityCurveExponentPosition = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentPosition");
            IntensityCurveExponentInflate = MelonPreferences.GetEntryValue<float>(BuildInfo.Name, "IntensityCurveExponentInflate");
            Active = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "Active");
            bool setupMode = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "SetupMode");
            Util.Debug = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "Debug");
            Util.DebugPerformance = MelonPreferences.GetEntryValue<bool>(BuildInfo.Name, "DebugPerformance");
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

        async void DootDoot(IAdultToy device)
        {
            try
            {
                ToyAPI.SetMotorSpeed(device, MotorType.Vibrate, 0.15f);
                await Task.Delay(150);
                ToyAPI.SetMotorSpeed(device, MotorType.Vibrate, 0.0f);
                await Task.Delay(150);
                ToyAPI.SetMotorSpeed(device, MotorType.Vibrate, 0.15f);
                await Task.Delay(150);
                ToyAPI.SetMotorSpeed(device, MotorType.Vibrate, 0.0f);
            }
            catch { }
        }

    }
}
