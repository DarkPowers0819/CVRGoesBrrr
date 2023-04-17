using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnhollowerRuntimeLib;
using UnityEngine;
using UnityEngine.Audio;
using static VibeGoesBrrr.Util;
using UnhollowerBaseLib;

namespace VibeGoesBrrr
{
  class AudioProvider : ISensorProvider
  {
    public bool Enabled { get; set; }
    public IEnumerable<Sensor> Sensors => mSensorInstances.Values;
    public int Count => mSensorInstances.Count;
    public event EventHandler<Sensor> SensorDiscovered;
    public event EventHandler<Sensor> SensorLost;

    private Dictionary<int, Sensor> mSensorInstances = new Dictionary<int, Sensor>();

    public class AudioSensor : Sensor
    {
      public override GameObject GameObject => mSource?.gameObject;
      
      private Il2CppStructArray<float> spectrum = new Il2CppStructArray<float>(256);
      public override float Value {
        get {
          mSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
          float val = 0f;
          for (int i = 1; i < 50; i++) {
            if (spectrum[i] > val) {
              val = spectrum[i];
            }
          }
          return val;
        }
      }

      private AudioSource mSource;
      public AudioSensor(string name, AudioSource source)
        : base(new Regex(".*"), name, SensorOwnerType.LocalPlayer)
      {
        mSource = source;
      }
    }

    public void OnSceneWasInitialized()
    {
      foreach (var kv in mSensorInstances) {
        SensorLost?.Invoke(this, kv.Value);
      }
      mSensorInstances.Clear();
      
      AudioMixerGroup mixerGroup = null;
      foreach (var gameObject in GameObject.FindObjectsOfTypeAll(Il2CppType.Of<AudioMixerGroup>())) {
        if (gameObject.name == "World") {
          mixerGroup = gameObject.Cast<AudioMixerGroup>();
          break;
        }
      }

      foreach (var gameObject in GameObject.FindObjectsOfTypeAll(Il2CppType.Of<AudioSource>())) {
        var audioSource = gameObject.Cast<AudioSource>();
        if (audioSource.outputAudioMixerGroup == mixerGroup) {
          var sensor = new AudioSensor(audioSource.name, audioSource);
          mSensorInstances[audioSource.GetInstanceID()] = sensor;
          SensorDiscovered?.Invoke(this, sensor);
        }
      }
    }
  }
}