using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ABI.CCK.Components;
using UnityEngine;

namespace VibeGoesBrrr
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
      CVRHooks.AvatarIsReady += OnAvatarIsReady;
    }

    public void Dispose()
    {
      CVRHooks.AvatarIsReady -= OnAvatarIsReady;
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
      foreach (var kv in mSensorInstances) {
        if (kv.Value.Camera == null) {
          lostSensors.Add(kv.Key);
        }
      }
      foreach (var id in lostSensors) {
        var lostSensor = mSensorInstances[id];
        mSensorInstances.Remove(id);
        SensorLost?.Invoke(this, lostSensor);
      }
    }

    private void OnAvatarIsReady(object sender, AvatarEventArgs args)
    {
      foreach (var camera in args.Avatar.GetComponentsInChildren<Camera>(true)) {
        if (IsSensor(camera)) {
          if (!mSensorInstances.ContainsKey(camera.GetInstanceID())) {
            var newSensor = new TouchSensor(camera.name, args.Avatar == CVRHooks.LocalAvatar ? SensorOwnerType.LocalPlayer : SensorOwnerType.RemotePlayer, camera);
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
