using System;
using System.Collections.Concurrent;
using ABI_RC.Core.Player;
using UnityEngine;

namespace CVRGoesBrrr
{
    static class Util
    {
        public static bool Debug;
        public static bool DebugPerformance;
        public static bool BackgroundThreadsAllowed;
        private static ConcurrentDictionary<string, DateTime> Timers = new ConcurrentDictionary<string, DateTime>();
        public static void DebugLog(string message)
        {
            if (Debug)
            {
                MelonLoader.MelonLogger.Msg(System.ConsoleColor.Cyan, "[DEBUG] " + message);
            }
        }
        public static void Warn(string message)
        {
            MelonLoader.MelonLogger.Warning(message);
        }
        public static void Info(string message)
        {
            MelonLoader.MelonLogger.Msg(System.ConsoleColor.White, "[INFO] " + message);
        }
        public static void Error(string message)
        {
            MelonLoader.MelonLogger.Error(message);
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
            Timers[timerName + "Start"] = DateTime.Now;
        }
        public static void StopTimer(string timerName, double warningThreshold)
        {
            DateTime stopTime = DateTime.Now;
            DateTime startTime = Timers[timerName + "Start"];
            TimeSpan duration = stopTime - startTime;
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
