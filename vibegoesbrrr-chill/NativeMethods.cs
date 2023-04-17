using Buttplug;
using MelonLoader;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using static MelonLoader.MelonLogger;
using static VibeGoesBrrr.Util;

namespace VibeGoesBrrr
{
    static public class NativeMethods
    {
        public static string TempPath
        {
            get
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"{BuildInfo.Name}-{BuildInfo.Version}");
                if (!Directory.Exists(tempPath))
                {
                    Directory.CreateDirectory(tempPath);
                }
                return tempPath;
            }
        }
    }
}
