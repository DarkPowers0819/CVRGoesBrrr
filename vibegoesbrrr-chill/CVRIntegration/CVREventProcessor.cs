using ABI.CCK.Components;
using ABI_RC.Core.Player;
using ABI_RC.Core.Savior;
using CVRGoesBrrr;
using CVRGoesBrrr.Unity.Assets.VibeGoesBrrr_Internal;
using HarmonyLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using UnityEngine;

namespace CVRGoesBrrr.CVRIntegration
{
    public class CVREventProcessor
    {
        private static CVREventProcessor instance = new CVREventProcessor();
        public static CVREventProcessor Instance { get { return instance; } }

        private System.Timers.Timer SystemTimer;
        private ConcurrentQueue<CVREvent> ItemsToProcess = new ConcurrentQueue<CVREvent>();

        public static event EventHandler<AvatarEventArgs> AvatarIsReady;
        public static event EventHandler<GameObject> PropIsReady;
        public static event EventHandler<GameObject> PropAttached;
        public static event EventHandler<GameObject> PropDettached;
        public static GameObject LocalAvatar = null;
        private CVREventProcessor()
        {
            SystemTimer = new System.Timers.Timer(1);
            SystemTimer.Elapsed += SystemTimer_Elapsed;
            SystemTimer.AutoReset = true;
            SystemTimer.Enabled = true;
            SystemTimer.Start();
        }

        private void SystemTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (ItemsToProcess.Count > 0)
                {
                    CVREvent cvrEvent = null;
                    if (ItemsToProcess.TryDequeue(out cvrEvent))
                    {
                        ProcessEvent(cvrEvent);
                    }
                }
            }
            catch (Exception error)
            {
                Util.Error(error.ToString());
            }
        }
        public void QueueCVREvent(CVREvent eventToQueue)
        {
            if (Util.BackgroundThreadsAllowed)
            {
                ItemsToProcess.Enqueue(eventToQueue);
            }
            else
            {
                ProcessEvent(eventToQueue);
            }
        }
        private void ProcessEvent(CVREvent cvrEvent)
        {
            if (cvrEvent.EventType == CVREventType.LocalAvatarChange)
            {
                OnLocalAvatarLoad(cvrEvent.EventData as GameObject);
            }
            if (cvrEvent.EventType == CVREventType.RemoteAvatarChange)
            {
                OnRemoteAvatarLoad(cvrEvent.EventData as PuppetMaster);
            }
            if (cvrEvent.EventType == CVREventType.PropLoaded)
            {
                PropLoaded(cvrEvent.EventData as GameObject);
            }
            if (cvrEvent.EventType == CVREventType.PropAttached)
            {
                OnPropAttached(cvrEvent.EventData as GameObject);
            }
            if (cvrEvent.EventType == CVREventType.PropDettached)
            {
                OnPropDettached(cvrEvent.EventData as GameObject);
            }
        }


        public static void OnPropAttached(GameObject __instance)
        {
            PropAttached?.Invoke(null, __instance);
        }
        public static void OnPropDettached(GameObject __instance)
        {
            PropDettached?.Invoke(null, __instance);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="__0"></param>
        private void OnLocalAvatarLoad(GameObject __0)
        {
            Util.StartTimer("OnLocalAvatarLoad");
            LocalAvatar = __0;
            AvatarIsReady?.Invoke(null, new AvatarEventArgs { Avatar = __0, Player = null });
            Util.StopTimer("OnLocalAvatarLoad", 10);
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
            Util.StopTimer("OnRemoteAvatarLoad", 10);
        }
        private static void PropLoaded(GameObject __instance)
        {
            CVRAttachment[] attachments = __instance.GetComponentsInChildren<CVRAttachment>();
            if (attachments != null && attachments.Length > 0)
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
}
