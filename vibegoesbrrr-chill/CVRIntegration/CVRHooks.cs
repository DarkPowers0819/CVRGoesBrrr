using System;
using System.Collections.Generic;
using System.Reflection;
using ABI.CCK.Components;
using ABI_RC.Core.EventSystem;
using ABI_RC.Core.IO;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using CVRGoesBrrr.Unity.Assets.VibeGoesBrrr_Internal;
using System.Linq;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Core.Networking.API.Responses;
using ABI_RC.Core.Util.Encryption;

namespace CVRGoesBrrr.CVRIntegration
{
    /// <summary>
    /// A utility class to add our needed bindings into CVR code. Mostly we care about Avatars and Props.
    /// </summary>
    static class CVRHooks
    {
        private static readonly List<string> BlockedNames = new List<string>() {"CVRLoading Variant", "RightHandGrab","LeftHandGrab","Play","Pause","Video Buffer Slider","Image", "LoadingAvatar(Clone)", "VideoCurrentTimeLeft","VideoCurrentTimeLeftLive"};
        
        
        private static readonly HashSet<string> PendingProps = new HashSet<string>();

        public static void AddHooksIntoCVR(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(typeof(PlayerSetup).GetMethod(nameof(PlayerSetup.SetupAvatar), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnLocalAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(PuppetMaster).GetMethod(nameof(PuppetMaster.AvatarInstantiated), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnRemoteAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(CVRObjectLoader).GetMethod(nameof(CVRObjectLoader.InstantiateSpawnableFromBundle), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(InstantiateProp), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(GameObject).GetMethod(nameof(GameObject.SetActive), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(SetActive), BindingFlags.NonPublic | BindingFlags.Static)));
        }
        public static void OnPropAttached(CVRAttachment __instance)
        {
            if (__instance.IsAttached())
            {
                CVREventProcessor.Instance.QueueCVREvent(new CVREvent() { EventData = __instance.gameObject, EventType = CVREventType.PropAttached });
            }
        }
        public static void OnPropDettached(CVRAttachment __instance)
        {
            if (!__instance.IsAttached())
            {
                CVREventProcessor.Instance.QueueCVREvent(new CVREvent() { EventData = __instance.gameObject, EventType = CVREventType.PropDettached });
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__0"></param>
        private static void OnLocalAvatarLoad(GameObject __0)
        {
            CVREventProcessor.Instance.QueueCVREvent(new CVREvent() { EventData = __0, EventType = CVREventType.LocalAvatarChange });
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__instance"></param>
        private static void OnRemoteAvatarLoad(PuppetMaster __instance)
        {
            CVREventProcessor.Instance.QueueCVREvent(new CVREvent() { EventData = __instance, EventType = CVREventType.RemoteAvatarChange });
        }
        /// <summary>
        /// our hook into CVR InstantiateProp. gives us notice when a prop has finished downloading. store the prop name in a list so that when SetActive is called we can get the actual GameObject.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tags"></param>
        /// <param name="objectId"></param>
        /// <param name="instTarget"></param>
        /// <param name="b"></param>
        ///                                 string objectId, string fileHash, string instantiationTarget, CVREncryptionRouter router, AssetManagement.PropTags propTags, CompatibilityVersions compatibilityVersion, string blockReason = ""
        private static void InstantiateProp(string objectId, string fileHash, string instantiationTarget, CVREncryptionRouter router, AssetManagement.PropTags propTags, CompatibilityVersions compatibilityVersion, string blockReason = "")
        {
            PendingProps.Add(instantiationTarget);
        }
        /// <summary>
        /// our hook into unity GameObject SetActive, by matching the gameobject name to the prop names we can get the gameobject from here.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="value"></param>
        private static void SetActive(GameObject __instance, bool value)
        {
            if (value == true && PendingProps.Count > 0 && PendingProps.Remove(__instance.name))
            {
                CVREventProcessor.Instance.QueueCVREvent(new CVREvent() { EventData = __instance, EventType = CVREventType.PropLoaded });
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="duration"></param>
        /// <param name="frequency"></param>
        /// <param name="amplitude"></param>
        /// <param name="hand">true=left hand, false=right hand</param>
        public static void VibratePlayerHands(float delay = 0.0f, float duration = 0.0f, float frequency = 440f, float amplitude = 1f, bool hand = false)
        {
            CVRInputManager.Instance.Vibrate(delay, duration, frequency, amplitude, hand);
        }
        public static void SetAdvancedAvatarParameter(string parameterName, float intensityValue)
        {
            Util.DebugLog($"checking if Avatar parameter {parameterName} exists");
            bool parameterExists = PlayerSetup.Instance.animatorManager.animator.parameters.Select((c) => c.name).Contains(parameterName);
            if (parameterExists)
            {
                Util.DebugLog($"setting Avatar parameter {parameterName} to {intensityValue}");
                PlayerSetup.Instance.changeAnimatorParam(parameterName, intensityValue);
            }
        }
    }
}