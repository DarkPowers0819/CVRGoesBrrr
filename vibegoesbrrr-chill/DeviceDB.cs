using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using static MelonLoader.MelonLogger;

namespace CVRGoesBrrr
{
    class DeviceDB
    {
        //private static string Endpoint = "https://iostindex.com/devices.json";
        private static TimeSpan CacheLifetime = new TimeSpan(1, 0, 0, 0); // 1 day

        private static Task FetchTask;
        private static List<IoSTDevice> Devices;

        private string CachePath => Path.Combine(NativeMethods.TempPath, "devices.json");

        public class IoSTDevice
        {
            public string Brand { get; set; }
            public string Device { get; set; }
            public string Detail { get; set; }
            public string Availability { get; set; }
            public string Connection { get; set; }
            public string Type { get; set; }

            public class IoSTDeviceButtplug
            {
                public bool ButtplugSupport { get; set; }
            }
            public IoSTDeviceButtplug Buttplug { get; set; }

            public static string[] GiverTypes = {
        "Cock Ring",
        "Onahole",
        "Strap-on"
      };

            public static string[] TakerTypes = {
        "Buttplug",
        "Insertable Vibrator",
        "Prostate Vibrator",
        "Clip Vibrator",
        "Kegel",
        "Love Egg",
        "Panty Vibrator",
        "Rabbit",
        "Ride-on Vibrator",
        "Butterfly",
        "Suction Vibrator",
        "Wand",
        "Nipple Clamps"
      };

            public bool IsGiver
            {
                get
                {
                    return GiverTypes.Contains(Type.Trim());
                }
            }

            public bool IsTaker
            {
                get
                {
                    return TakerTypes.Contains(Type.Trim());
                }
            }
        }

        public DeviceDB()
        {
            if (FetchTask == null)
            {
                FetchTask = Fetch();
            }
        }

        private async Task Fetch(bool forceCached = false)
        {
            // Force using cached copy if cache is fresh
            //try
            //{
            //    var cacheLastWrite = File.GetLastWriteTimeUtc(CachePath);
            //    if (cacheLastWrite > DateTime.Now - CacheLifetime)
            //    {
            //        Util.DebugLog($"Device database is only {(DateTime.Now - cacheLastWrite).ToString()} old. Using cached copy.");
            //        var json = File.ReadAllText(CachePath, Encoding.UTF8);
            //        Devices = JsonConvert.DeserializeObject<List<IoSTDevice>>(json);
            //        return;
            //    }
            //}
            //catch { }

            // Fetch device database from IoSTIndex.com, courtesy of blackspherefollower ❤️
            //try
            //{
            //    var req = HttpWebRequest.CreateHttp(Endpoint);
            //    var response = (HttpWebResponse)(await req.GetResponseAsync());
            //    var json = new StreamReader(response.GetResponseStream()).ReadToEnd();
            //    Devices = JsonConvert.DeserializeObject<List<IoSTDevice>>(json);

            //    // Cache DB to disk
            //    File.WriteAllText(CachePath, json);
            //    var cacheFile = File.Open(CachePath, FileMode.OpenOrCreate);
            //    var encoded = Encoding.UTF8.GetBytes(json);
            //    await cacheFile.WriteAsync(encoded, 0, encoded.Length);

            //    return;
            //}
            //catch (Exception)
            //{
            //    // Warning("Failed to fetch device database from IoSTIndex.com. Using cached copy.");
            //    // Warning(e.Message);
            //}

            // If all else fails, load from packed resources
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("devices.json"))
            {
                var json = await new StreamReader(stream).ReadToEndAsync();
                Devices = JsonConvert.DeserializeObject<List<IoSTDevice>>(json);
            }
        }

        public IoSTDevice FindDevice(string name)
        {
            foreach (var device in Devices)
            {
                var fullName = $"{device.Brand} {device.Device}";
                if (fullName.ToLower().Contains(name.ToLower()))
                {
                    return device;
                }
            }
            return null;
        }
    }
}