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

namespace CVRGoesBrrr
{
    /// <summary>
    /// A utility class to add our needed bindings into CVR code. Mostly we care about Avatars and Props.
    /// </summary>
    static class CVRHooks
    {
        private static readonly List<string> BlockedNames = new List<string>() { "RightHandGrab","LeftHandGrab","Play","Pause","Video Buffer Slider","VideoCurrentTimeLeft","VideoCurrentTimeLeftLive"};
        public static event EventHandler<AvatarEventArgs> AvatarIsReady;
        public static event EventHandler<GameObject> PropIsReady;
        public static event EventHandler<GameObject> PropAttached;
        public static event EventHandler<GameObject> PropDettached;
        public static GameObject LocalAvatar = null;
        private static readonly HashSet<string> PendingProps = new HashSet<string>();

        public static void AddHooksIntoCVR(HarmonyLib.Harmony harmony)
        {
            harmony.Patch(typeof(PlayerSetup).GetMethod(nameof(PlayerSetup.SetupAvatar), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnLocalAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(PuppetMaster).GetMethod(nameof(PuppetMaster.AvatarInstantiated), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnRemoteAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(CVRObjectLoader).GetMethod(nameof(CVRObjectLoader.InstantiateProp), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(InstantiateProp), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(GameObject).GetMethod(nameof(GameObject.SetActive), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(SetActive), BindingFlags.NonPublic | BindingFlags.Static)));
            //harmony.Patch(typeof(CVRAttachment).GetMethod(nameof(CVRAttachment.Attach), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnPropAttached), BindingFlags.NonPublic | BindingFlags.Static)));
            //harmony.Patch(typeof(CVRAttachment).GetMethod(nameof(CVRAttachment.DeAttach), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnPropDettached), BindingFlags.NonPublic | BindingFlags.Static)));
        }
        public static void OnPropAttached(CVRAttachment __instance)
        {
            if (__instance.IsAttached())
            {
                Util.DebugLog("Prop was attached " + __instance.gameObject.name);
                PropAttached?.Invoke(null, __instance.gameObject);
            }
        }
        public static void OnPropDettached(CVRAttachment __instance)
        {
            if (!__instance.IsAttached())
            {
                Util.DebugLog("Prop was Dettached " + __instance.gameObject.name);
                PropDettached?.Invoke(null, __instance.gameObject);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__0"></param>
        private static void OnLocalAvatarLoad(GameObject __0)
        {
            Util.StartTimer("OnLocalAvatarLoad");
            LocalAvatar = __0;
            AvatarIsReady?.Invoke(null, new AvatarEventArgs { Avatar = __0, Player = null });
            Util.StopTimer("OnLocalAvatarLoad");
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__instance"></param>
        private static void OnRemoteAvatarLoad(PuppetMaster __instance)
        {
            Util.StartTimer("OnRemoteAvatarLoad");
            var playerDescriptor = Traverse.Create(__instance).Field("_playerDescriptor").GetValue<PlayerDescriptor>();
            Util.DebugLog($"OnRemoteAvatarLoad: {playerDescriptor.userName}");
            AvatarIsReady?.Invoke(null, new AvatarEventArgs { Avatar = __instance.avatarObject, Player = playerDescriptor });
            Util.StopTimer("OnRemoteAvatarLoad");
        }
        /// <summary>
        /// our hook into CVR InstantiateProp. gives us notice when a prop has finished downloading. store the prop name in a list so that when SetActive is called we can get the actual GameObject.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="tags"></param>
        /// <param name="objectId"></param>
        /// <param name="instTarget"></param>
        /// <param name="b"></param>
        private static void InstantiateProp(DownloadTask.ObjectType t, AssetManagement.PropTags tags, string objectId, string instTarget, byte[] b)
        {
            Util.DebugLog("InstantiateProp: "+ instTarget);
            if (t == DownloadTask.ObjectType.Prop)
            {
                PendingProps.Add(instTarget);
            }
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
                //Util.DebugLog("prop " + __instance.name + " is on layer " + __instance.layer);
                CVRAttachment[] attachments = __instance.GetComponentsInChildren<CVRAttachment>();
                if(attachments!=null && attachments.Length>0)
                {
                    foreach (var item in attachments)
                    {
                        Util.DebugLog("Adding attachment tracker to: " + item.gameObject.GetInstanceID());
                        Util.DebugLog("Attachment has name: " + item.gameObject.name);
                        Util.DebugLog("root obj has name: " + __instance.name);
                        AttachmentTracker tracker = __instance.AddComponent<AttachmentTracker>();
                        tracker.AttachmentToMonitor = item;
                        PropIsReady?.Invoke(null, item.gameObject);
                    }
                }
                else
                {
                    PropIsReady?.Invoke(null, __instance);
                }
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
    }
}