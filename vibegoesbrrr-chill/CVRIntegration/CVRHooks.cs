using System;
using System.Reflection;
using ABI.CCK.Components;
using ABI_RC.Core.Player;
using HarmonyLib;
using System.Linq;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Core;
using ABI_RC.Systems.GameEventSystem;
using ABI_RC.Systems.IK.SubSystems;

namespace CVRGoesBrrr.CVRIntegration
{
    /// <summary>
    /// A utility class to add our needed bindings into CVR code. Mostly we care about Avatars and Props.
    /// </summary>
    static class CVRHooks
    {
        public static Action LocalAvatarIsReady;
        public static Action<PuppetMaster, PlayerDescriptor> RemoteAvatarIsReady;
        public static Action<CVRSpawnable> PropIsReady;
        public static Action<CVRAttachment> PropAttached;
        public static Action<CVRAttachment> PropDettached;

        public static void AddHooksIntoCVR(HarmonyLib.Harmony harmony)
        {
            //Use the CVRGameEventSystem for what we can
            CVRGameEventSystem.Spawnable.OnInstantiate.AddListener(OnPropInstantiated);

            harmony.Patch(typeof(BodySystem).GetMethod(nameof(BodySystem.InitializeAvatar), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnLocalAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
            harmony.Patch(typeof(PuppetMaster).GetMethod(nameof(PuppetMaster.AvatarInstantiated), BindingFlags.Public | BindingFlags.Instance), postfix: new HarmonyMethod(typeof(CVRHooks).GetMethod(nameof(OnRemoteAvatarLoad), BindingFlags.NonPublic | BindingFlags.Static)));
        }

        private static void OnPropInstantiated(string spawnedBy, CVRSpawnable propSpawnable)
        {
            CVRAttachment[] attachments = propSpawnable.GetComponentsInChildren<CVRAttachment>();

            if (attachments != null && attachments.Length > 0)
            {
                foreach (var item in attachments)
                {
                    Util.DebugLog($"Attachable prop detected, adding events to CVRAttachment component! GUID: {propSpawnable.guid} | Name: {propSpawnable.name}");

                    item.onAttach.AddListener(() =>
                    {
                        PropAttached?.Invoke(item);
                    });

                    item.onDeattach.AddListener(() =>
                    {
                        PropDettached?.Invoke(item);
                    });
                }
            }

            PropIsReady?.Invoke(propSpawnable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="__0"></param>
        private static void OnLocalAvatarLoad()
        {
            Util.StartTimer("OnLocalAvatarLoad");
            LocalAvatarIsReady?.Invoke();
            Util.StopTimer("OnLocalAvatarLoad", 10);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__instance"></param>
        private static void OnRemoteAvatarLoad(PuppetMaster __instance)
        {
            Util.StartTimer("OnRemoteAvatarLoad");
            var descriptor = __instance.GetPlayerDescriptor();
            Util.DebugLog($"RemoteAvatarLoad fired - Username: {descriptor.userName} | Name: {__instance.avatarObject.name}");
            RemoteAvatarIsReady.Invoke(__instance, descriptor);
            Util.StopTimer("OnRemoteAvatarLoad", 10);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="duration"></param>
        /// <param name="frequency"></param>
        /// <param name="amplitude"></param>
        /// <param name="hand">true=left hand, false=right hand</param>
        public static void VibratePlayerHands(float delay = 0.0f, float duration = 0.0f, float frequency = 440f, float amplitude = 1f, CVRHand hand=CVRHand.Left)
        {
            CVRInputManager.Instance.Vibrate(delay, duration, frequency, amplitude, hand);
        }
        public static void SetAdvancedAvatarParameter(string parameterName, float intensityValue)
        {
            Util.DebugLog($"checking if Avatar parameter {parameterName} exists");
            bool parameterExists = PlayerSetup.Instance.animatorManager.Parameters.Select((c)=>c.Value.name).Contains(parameterName);
            if (parameterExists)
            {
                Util.DebugLog($"setting Avatar parameter {parameterName} to {intensityValue}");
                PlayerSetup.Instance.changeAnimatorParam(parameterName, intensityValue);
            }
        }
    }
}