using System;
using System.Collections.Concurrent;
using System.Reflection;
using ABI_RC.Core.Player;
using MelonLoader;
using UnityEngine;

namespace CVRGoesBrrr
{
    static class Util
    {
        public static MelonLogger.Instance Logger;
        public static bool Debug;
        public static bool DebugPerformance;
        private static ConcurrentDictionary<string, System.Diagnostics.Stopwatch> Stopwatches = new ConcurrentDictionary<string, System.Diagnostics.Stopwatch>();
        private static FieldInfo _getPlayerDescriptor = typeof(PuppetMaster).GetField("_playerDescriptor", BindingFlags.Instance | BindingFlags.NonPublic);
        public static void DebugLog(string message)
        {
            if (Debug)
            {
                Logger.Msg(System.ConsoleColor.Cyan, "[DEBUG] " + message);
            }
        }
        public static void Warn(string message)
        {
            Logger.Warning("[WARN] "+message);
        }
        public static void Info(string message)
        {
            Logger.Msg(System.ConsoleColor.White, "[INFO] " + message);
        }
        public static void Error(string message)
        {
            Logger.Error("[ERROR] "+message);
        }
        public static bool AlmostEqual(double a, double b)
        {
            const double delta = 0.0001;
            return Math.Abs(a - b) < delta;
        }

        public static bool IsChildOf(GameObject parent, GameObject child)
        {
            if (parent == child)
            {
                return true;
            }

            for (int i = 0; i < parent.transform.childCount; i++)
            {
                if (IsChildOf(parent.transform.GetChild(i).gameObject, child))
                {
                    return true;
                }
            }

            return false;
        }

        public static PlayerDescriptor GetPlayerDescriptor(this PuppetMaster pm)
        {
            return (PlayerDescriptor)_getPlayerDescriptor.GetValue(pm);
        }

        // Since there is no GetComponentInParent that gets inactive components...
        // GetComponent functions not including active components and not making it a conscious
        // choice for the programmer is one of the biggest mistakes and source of bugs Unity ever caused.
        public static T GetComponentInParent<T>(Component c, bool inactive)
        {
            var components = c.GetComponentsInParent<T>(inactive);
            return components.Length > 0 ? components[0] : default(T);
        }

        public static void StartTimer(string timerName)
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            Stopwatches[timerName] = watch;
            watch.Reset();
            watch.Start();
        }
        public static void StopTimer(string timerName, double warningThreshold)
        {
            Stopwatches[timerName].Stop();
            TimeSpan duration = Stopwatches[timerName].Elapsed;
            string durationMessage = "Timer " + timerName + $" ran for {duration.TotalMilliseconds} milliseconds";
            if (duration.TotalMilliseconds > warningThreshold)
            {
                Util.Warn(durationMessage);
            }
            else if (DebugPerformance)
            {
                Util.DebugLog(durationMessage);
            }
        }
    }
}
