using System;
using System.Reflection;
using ABI.CCK.Components;
using ABI_RC.Core.Player;
using HarmonyLib;
using System.Linq;
using ABI_RC.Systems.InputManagement;
using ABI_RC.Core;
using ABI_RC.Core.Util;
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
            CVRGameEventSystem.Spawnable.OnPropSpawned.AddListener(OnPropInstantiated);
            CVRGameEventSystem.Avatar.OnLocalAvatarLoad.AddListener(OnLocalAvatarLoad);
            CVRGameEventSystem.Avatar.OnRemoteAvatarLoad.AddListener(OnRemoteAvatarLoad);
        }

        private static void OnPropInstantiated(string spawnedBy, CVRSyncHelper.PropData propSpawnable)
        {
            CVRAttachment[] attachments = propSpawnable.Spawnable.GetComponentsInChildren<CVRAttachment>();

            if (attachments != null && attachments.Length > 0)
            {
                foreach (var item in attachments)
                {
                    Util.DebugLog($"Attachable prop detected, adding events to CVRAttachment component! GUID: {propSpawnable.Spawnable.guid} | Name: {propSpawnable.Spawnable.name}");

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

            PropIsReady?.Invoke(propSpawnable.Spawnable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="__0"></param>
        private static void OnLocalAvatarLoad(CVRAvatar avatar)
        {
            Util.StartTimer("OnLocalAvatarLoad");
            LocalAvatarIsReady?.Invoke();
            Util.StopTimer("OnLocalAvatarLoad", 10);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__instance"></param>
        private static void OnRemoteAvatarLoad(CVRPlayerEntity player, CVRAvatar avatar)
        {
            Util.DebugLog($"RemoteAvatarLoad fired - Username: {player.Username} | Name: {avatar.name}");
            RemoteAvatarIsReady.Invoke(player.PuppetMaster, player.PlayerDescriptor);
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
            bool parameterExists = PlayerSetup.Instance.AnimatorManager.Parameters.Select((c)=>c.Value.name).Contains(parameterName);
            if (parameterExists)
            {
                Util.DebugLog($"setting Avatar parameter {parameterName} to {intensityValue}");
                PlayerSetup.Instance.ChangeAnimatorParam(parameterName, intensityValue);
            }
        }
    }
}