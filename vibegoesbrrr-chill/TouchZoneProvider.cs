using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ABI_RC.Core.Player;
using ABI.CCK.Components;
using CVRGoesBrrr.CVRIntegration;
using UnityEngine;

namespace CVRGoesBrrr
{
    class TouchZoneProvider : ISensorProvider, IDisposable
    {


        public IEnumerable<Sensor> Sensors => mSensorInstances.Values;
        public int Count => mSensorInstances.Count;
        public event EventHandler<Sensor> SensorDiscovered;
        public event EventHandler<Sensor> SensorLost;

        public static Dictionary<int, TouchSensor> mSensorInstances = new Dictionary<int, TouchSensor>();

        public TouchZoneProvider()
        {
            CVRHooks.RemoteAvatarIsReady += RemoteAvatarIsReady;
            CVRHooks.LocalAvatarIsReady += LocalAvatarIsReady;
        }

        private void LocalAvatarIsReady()
        {
            SetupSensors(PlayerSetup.Instance.AvatarObject, true);
        }

        private void RemoteAvatarIsReady(PuppetMaster puppetMaster, PlayerDescriptor playerDescriptor)
        {
            SetupSensors(puppetMaster.AvatarObject, false);
        }

        public void Dispose()
        {
            CVRHooks.RemoteAvatarIsReady -= RemoteAvatarIsReady;
            CVRHooks.LocalAvatarIsReady -= LocalAvatarIsReady;
        }

        public void OnSceneWasInitialized()
        {
            // In CVR, avatars load before the main scene is initialized.
            // We shouldn't need this as long as OnUpdate removes destroyed sensors :)
            // foreach (var kv in mSensorInstances) {
            //   Util.DebugLog($"TouchZoneProvider: OnSceneWasInitialized: Dropping sensor {kv.Value.Name}");
            //   SensorLost?.Invoke(this, kv.Value);
            // }
            // mSensorInstances.Clear();

#if DEBUG
        // World sensors (for debugging)
        foreach (var camera in GameObject.FindObjectsOfType<Camera>()) {
          if (IsSensor(camera) && camera.GetComponentInParent<CVRAvatar>() == null) {
            var newSensor = new TouchSensor(camera.name, SensorOwnerType.World, camera); 
            mSensorInstances[camera.GetInstanceID()] = newSensor;
            SensorDiscovered?.Invoke(this, newSensor);
          }
        }
#endif
        }

        public void OnUpdate()
        {
            // Check for destructions
            var lostSensors = new List<int>();
            foreach (var kv in mSensorInstances)
            {
                if (kv.Value.Camera == null)
                {
                    lostSensors.Add(kv.Key);
                }
            }
            foreach (var id in lostSensors)
            {
                var lostSensor = mSensorInstances[id];
                mSensorInstances.Remove(id);
                SensorLost?.Invoke(this, lostSensor);
            }
        }

        private void SetupSensors(GameObject avatarRoot, bool isLocal)
        {
            foreach (var camera in avatarRoot.GetComponentsInChildren<Camera>(true))
            {
                if (IsSensor(camera))
                {
                    if (!mSensorInstances.ContainsKey(camera.GetInstanceID()))
                    {
                        var newSensor = new TouchSensor(camera.name, isLocal ? SensorOwnerType.LocalPlayer : SensorOwnerType.RemotePlayer, camera);
                        mSensorInstances[camera.GetInstanceID()] = newSensor;
                        SensorDiscovered?.Invoke(this, newSensor);
                    }
                }
            }
        }

        public bool IsSensor(Camera camera)
        {
            return TouchSensor.Pattern.Match(camera.name).Success;
        }
    }
}
