using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using ABI.CCK.Components;
using AdultToyAPI;
using UnityEngine;
using UnityEngine.Animations;
using static MelonLoader.MelonLogger;

namespace VibeGoesBrrr
{
    class DeviceSensorBinder
    {
        public Dictionary<IAdultToy, List<(Sensor Sensor, int? Feature)>> Bindings = new Dictionary<IAdultToy, List<(Sensor Sensor, int? Feature)>>();

        public event EventHandler<(IAdultToy Device, Sensor Sensor, int? Feature)> BindingAdded;
        public event EventHandler<(IAdultToy Device, Sensor Sensor, int? Feature)> BindingRemoved;

        static readonly DeviceDB mDB = new DeviceDB();
        IAdultToyAPI ToyAPI;
        List<ISensorProvider> mSensorProviders = new List<ISensorProvider>();

        ConcurrentQueue<IAdultToy> DevicesAdded = new ConcurrentQueue<IAdultToy>();
        ConcurrentQueue<IAdultToy> DevicesRemoved = new ConcurrentQueue<IAdultToy>();
        public static bool JustUseMyDevice = false;
        public void SetButtplugClient(IAdultToyAPI buttplug)
        {            
            if (ToyAPI != null)
            {
                buttplug.DeviceRemoved -= OnDeviceRemoved;
                buttplug.DeviceAdded -= OnDeviceAdded;
                buttplug.ServerDisconnect -= OnButtplugDisconnect;
                OnButtplugDisconnect(buttplug, new EventArgs());
            }

            ToyAPI = buttplug;
            buttplug.DeviceAdded += OnDeviceAdded;
            buttplug.DeviceRemoved += OnDeviceRemoved;
            buttplug.ServerDisconnect += OnButtplugDisconnect;
        }

        public void AddSensorProvider(ISensorProvider sensorProvider)
        {
            mSensorProviders.Add(sensorProvider);
            sensorProvider.SensorDiscovered += OnSensorDiscovered;
            sensorProvider.SensorLost += OnSensorLost;
            foreach (var sensor in sensorProvider.Sensors)
            {
                OnSensorDiscovered(sensorProvider, sensor);
            }
        }

        public void OnUpdate()
        {
            // notes from Jayce
            // Since Buttplug events come from a different thread we must make sure to execute
            // all our logic here in the main thread to prevent weird race conditions and crashes.

            // OnDeviceRemoved
            IAdultToy device = null;
            DevicesRemoved.TryDequeue(out device);
            do
            {
                if (device != null)
                {
                    var removedBindings = Bindings.ContainsKey(device) ? Bindings[device] : new List<(Sensor Sensor, int? Feature)>();
                    Bindings.Remove(device);
                    foreach (var binding in removedBindings)
                    {
                        BindingRemoved?.Invoke(this, (device, binding.Sensor, binding.Feature));
                    }
                    DevicesRemoved.TryDequeue(out device);
                }
            } while (device != null);
            
            
            // OnDeviceAdded
            if (mSensorProviders != null)
            {
                DevicesAdded.TryDequeue(out device);
                do
                {
                    if (device != null)
                    {
                        foreach (var sensorProvider in mSensorProviders)
                        {
                            MatchSensorAndDevice(sensorProvider.Sensors.ToList(), device);
                        }
                    }
                    DevicesAdded.TryDequeue(out device);
                } while (device != null);
            }
        }

        void OnDeviceAdded(object buttplug, DeviceAddedEventArgs e)
        {
            try
            {
                DevicesAdded.Enqueue(e.AdultToy);
            }catch(Exception error)
            {
                Error(error);
            }
        }

        void OnDeviceRemoved(object buttplug, DeviceRemovedEventArgs e)
        {
            try
            {
                DevicesRemoved.Enqueue(e.AdultToy);
            }
            catch (Exception error)
            {
                Error(error);
            }
        }

        void OnButtplugDisconnect(object buttplug, EventArgs e)
        {
            try
            {
                foreach (var device in Bindings.Keys.ToList())
                {
                    OnDeviceRemoved(ToyAPI, new DeviceRemovedEventArgs(device));
                }
            }
            catch (Exception error)
            {
                Error(error);
            }
        }

        void OnSensorDiscovered(object sensorProviderObj, Sensor sensor)
        {
            if (ToyAPI != null)
            {
                foreach (var device in ToyAPI.GetConnectedDevices())
                {
                    MatchSensorAndDevice(new List<Sensor> { sensor }, device);
                }
            }
        }
        private void MatchSensorAndDevice(List<Sensor> sensors, IAdultToy device)
        {
            var matches = Match(sensors, device);
            if (matches.Count > 0)
            {
                if (!Bindings.ContainsKey(device))
                {
                    Bindings[device] = matches;
                }
                else
                {
                    Bindings[device].AddRange(matches);
                }
                foreach (var binding in matches)
                {
                    BindingAdded?.Invoke(this, (device, binding.Sensor, binding.Feature));
                }
            }
        }

        void OnSensorLost(object sensorManager, Sensor sensor)
        {
            foreach (var kv in Bindings)
            {
                var removedBindings = kv.Value.FindAll(binding => binding.Sensor == sensor);
                kv.Value.RemoveAll(binding => binding.Sensor == sensor);
                foreach (var binding in removedBindings)
                {
                    BindingRemoved?.Invoke(this, (kv.Key, binding.Sensor, binding.Feature));
                }
            }
        }

        static List<(Sensor Sensor, int? Feature)> Match(List<Sensor> sensors, IAdultToy device)
        {
            var matching = new List<(Sensor, int?)>();
            foreach (var sensor in sensors)
            {
                if (sensor is Giver || sensor is Taker)
                {
                    // Auto-assign thrust vector sensors without tags to certain toys, to make them work "out of the box"
                    if (string.IsNullOrWhiteSpace(sensor.Tag))
                    {
                        // Don't do bone type checks for world sensors
                        if (sensor.OwnerType == SensorOwnerType.World)
                        {
                            matching.Add((sensor, null));
                            continue;
                        }

                        try
                        {
                            // Only auto attach to specific bone types (so we don't end up with vibrating mouth givers etc)
                            HumanBodyBones[] validBoneParents = { HumanBodyBones.Spine, HumanBodyBones.Hips, HumanBodyBones.Chest, HumanBodyBones.UpperChest };
                            if (IsChildOfBoneType(sensor.GameObject.transform, validBoneParents))
                            {
                                var iostDevice = mDB.FindDevice(device.GetName());
                                if (iostDevice != null)
                                {
                                    if(JustUseMyDevice)
                                    {
                                        matching.Add((sensor, null));
                                    }
                                    else if (sensor is Giver && iostDevice.IsGiver)
                                    {
                                        matching.Add((sensor, null));
                                        continue;
                                    }
                                    else if (sensor is Taker && iostDevice.IsTaker)
                                    {
                                        matching.Add((sensor, null));
                                        continue;
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Error(e.ToString());
                            continue;
                        }
                    }
                    else
                    {
                        // Allow "Giver", "Taker" and "Any" tags to override match by type
                        var iostDevice = mDB.FindDevice(device.GetName());
                        if (iostDevice != null)
                        {
                            if(JustUseMyDevice || sensor.Tag == "Any")
                            {
                                matching.Add((sensor, null));
                                continue;
                            }
                            else if (sensor.Tag == "Giver" && iostDevice.IsGiver)
                            {
                                matching.Add((sensor, null));
                                continue;
                            }
                            else if (sensor.Tag == "Taker" && iostDevice.IsTaker)
                            {
                                matching.Add((sensor, null));
                                continue;
                            }
                        }
                    }
                }

                // HapticsSensor compatibility
                if (sensor is TouchSensor && Regex.Match(sensor.Type, @"HapticsSensor_", RegexOptions.IgnoreCase).Success)
                {
                    try
                    {
                        int index = int.Parse(sensor.Tag.Substring(0, 1));
                        if (index == device.GetIndex())
                        {
                            matching.Add((sensor, null));
                            continue;
                        }
                    }
                    catch (System.FormatException)
                    {
                        Error($"Misconfigured sensor \"{sensor.Name}\". Please refer to the instructions on how to set up and name Touch Zones.");
                    }
                }

                // Assign untagged sensors to all devices for convenience
                if (string.IsNullOrWhiteSpace(sensor.Tag) && !(sensor is Giver || sensor is Taker))
                { // ThrustVectors without tags are auto-assigned a single device instead of all
                    matching.Add((sensor, null));
                    continue;
                }

                // General matching based on standard tag format
                var binding = MatchDevice(sensor.Tag, sensor, device);
                if (binding != null)
                {
                    matching.Add(binding.Value);
                }
            }
            return matching;
        }

        static (Sensor Sensor, int? Feature)? MatchDevice(string needle, Sensor sensor, IAdultToy device)
        {
            // Split needle on commas for multi-device binding
            var parts = needle.Split(',').Select(s => s.Trim());
            foreach (var part in parts)
            {
                if (String.IsNullOrWhiteSpace(part))
                {
                    continue;
                }

                var match = Regex.Match(part, @"^(.+?)?\s*(?:#(\d+?))?\s*$", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    // Partial match on device name
                    if (match.Groups[1].Success && !device.GetName().ToLower().Contains(match.Groups[1].Value.ToLower()))
                    {
                        continue;
                    }

                    // Motor indices are 1-based
                    // null index = all motors
                    int? motorIndex = null;
                    var MotorTypes = device.GetMotorTypes();
                    if (MotorTypes.Count > 0)
                    {
                        var motorCount = MotorTypes.Count;
                        motorIndex = match.Groups[2].Success ? (int?)int.Parse(match.Groups[2].Value) - 1 : null;
                        if (motorIndex != null && ((int)motorIndex < 0 || (int)motorIndex >= motorCount))
                        {
                            Error($"Invalid motor index \"{motorIndex + 1}\" for sensor \"{sensor.Name}\". The device \"{device.GetName()}\" reports only having {motorCount} motor{(motorCount > 0 ? "s" : "")}. Make sure you set up your sensor names correctly.");
                            continue;
                        }
                    }

                    return (sensor, motorIndex);
                }
            }

            return null;
        }

        static bool IsChildOfBoneType(Transform transform, HumanBodyBones[] boneTypes)
        {
            var animator = Util.GetComponentInParent<Animator>(transform, true);
            if (animator == null)
            {
                return false;
            }

            // Create a mapping from bone -> HumanBodyBones since Unity bizzarrely doesn't provide this
            var boneIds = new Dictionary<int, HumanBodyBones>();
            if (animator.isHuman)
            {
                foreach (HumanBodyBones boneId in Enum.GetValues(typeof(HumanBodyBones)))
                {
                    if (boneId == HumanBodyBones.LastBone)
                    {
                        continue;
                    }
                    var boneTransform = animator.GetBoneTransform(boneId);
                    if (boneTransform != null)
                    {
                        boneIds[boneTransform.GetInstanceID()] = boneId;
                    }
                }
            }
            else
            {
                // For generic rigs, match by name since that's the best we can do
                var root = transform.root.GetComponentInChildren<CVRAvatar>(true)?.gameObject;
                if (root == null)
                {
                    return false;
                }
                foreach (var skinnedMeshRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    foreach (var boneTransform in skinnedMeshRenderer.bones)
                    {
                        foreach (HumanBodyBones boneId in Enum.GetValues(typeof(HumanBodyBones)))
                        {
                            if (boneId != HumanBodyBones.LastBone && boneTransform.name == boneId.ToString())
                            {
                                boneIds[boneTransform.GetInstanceID()] = boneId;
                                break;
                            }
                        }
                    }
                }
            }

            // Gather a list of sensor parents, taking constraints into account
            var candidateParents = new List<Transform>();
            // Any parent constraint sources (which there could be multiple for some reason)
            var constraint = Util.GetComponentInParent<ParentConstraint>(transform, true);
            if (constraint != null)
            {
                for (int i = 0; i < constraint.sourceCount; i++)
                {
                    candidateParents.Add(constraint.GetSource(i).sourceTransform);
                }
            }
            // The direct parent of the sensor
            candidateParents.Add(transform.parent);

            // Figure out what's the first parent bone of sensor
            foreach (var candidate in candidateParents)
            {
                for (Transform parent = candidate; parent != null; parent = parent.transform.parent)
                {
                    if (boneIds.ContainsKey(parent.GetInstanceID()))
                    {
                        // If the bone is whitelisted, we're good
                        var boneId = boneIds[parent.GetInstanceID()];
                        if (boneTypes.Contains(boneId))
                        {
                            return true;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return false;
        }
    }
}
