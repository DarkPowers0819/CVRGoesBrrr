using System;
using ABI_RC.Core.Player;
using UnityEngine;

namespace VibeGoesBrrr
{
    static class Util
    {
        public static void DebugLog(string message)
        {
#if DEBUG
            MelonLoader.MelonLogger.Msg(System.ConsoleColor.Cyan, "[DEBUG] " + message);
#endif
        }
        public static void Warn(string message)
        {
            MelonLoader.MelonLogger.Warning(message);
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
    }
}
