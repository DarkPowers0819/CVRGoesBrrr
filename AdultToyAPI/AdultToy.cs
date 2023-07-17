using Buttplug.Core.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdultToyAPI
{
    public class AdultToy : IAdultToy
    {
        private Buttplug.Client.ButtplugClientDevice Device;
        private DateTime LastBatteryCheck;
        private double LastBatteryValue;

        internal double LastWarnedBattery;

        public AdultToy(Buttplug.Client.ButtplugClientDevice device)
        {
            Device = device;
        }
        public int GetIndex()
        {
            return (int)Device.Index;
        }

        public string GetName()
        {
            return Device.Name;
        }

        public int MotorCount()
        {
            return Device.MessageAttributes.ScalarCmd.Length;
        }
        public List<MotorType> GetMotorTypes()
        {
            List<MotorType> types = new List<MotorType>();
            foreach(var msgInfo in Device.MessageAttributes.ScalarCmd)
            {
                types.Add(GetMotorTypeFromActuatorType(msgInfo.ActuatorType));
            }
            return types;
        }

        private MotorType GetMotorTypeFromActuatorType(Buttplug.Core.Messages.ActuatorType actuatorType)
        {
            switch(actuatorType)
            {
                case ActuatorType.Constrict:
                    return MotorType.Constrict;
                case ActuatorType.Inflate:
                    return MotorType.Inflate;
                case ActuatorType.Oscillate:
                    return MotorType.Oscillate;
                case ActuatorType.Position:
                    return MotorType.Position;
                case ActuatorType.Rotate:
                    return MotorType.Rotate;
                case ActuatorType.Vibrate:
                    return MotorType.Vibrate;
                default:
                    return MotorType.Vibrate;
            }
        }

        public bool SupportsContraction()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Constrict);
        }

        public bool SupportsInflate()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Inflate);
        }

        public bool SupportsOscillate()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Oscillate);
        }

        public bool SupportsPosition()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Position);
        }

        public bool SupportsRotation()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Rotate);
        }

        public bool SupportsVibration()
        {
            return SupportsAction(Buttplug.Core.Messages.ActuatorType.Vibrate);
        }

        private bool SupportsAction(Buttplug.Core.Messages.ActuatorType action)
        {
            foreach (var message in Device.MessageAttributes.ScalarCmd)
            {
                if (message.ActuatorType == action)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// stop all device motors
        /// </summary>
        public void Stop()
        {
            Device.Stop();
        }

        public bool HasBattery()
        {
            return Device.HasBattery;
        }
        /// <summary>
        /// Returns the Battery Level of the toy. Do not call from the same thread as the Device Added Event. Normalized between 0.0 and 1.0
        /// </summary>
        /// <returns></returns>
        internal double GetBatteryLevelSync()
        {
            return GetBatteryLevelInternal(null);
        }
        /// <summary>
        /// Returns the Battery Level of the toy. Do not call from the same thread as the Device Added Event. Normalized between 0.0 and 1.0
        /// </summary>
        /// <param name="timeout"></param>
        /// <returns></returns>
        internal double GetBatteryLevelSync(int timeout)
        {
            return GetBatteryLevelInternal(timeout);
        }
        private double GetBatteryLevelInternal(int? timeout)
        {
            if (LastBatteryCheck == null || (DateTime.Now - LastBatteryCheck).TotalMinutes > 1)
            {
                LastBatteryCheck = DateTime.Now;
                var batteryTask = Device.BatteryAsync();
                if (timeout.HasValue)
                {
                    batteryTask.Wait(timeout.GetValueOrDefault());
                }
                else
                { 
                    batteryTask.Wait(); 
                }
                LastBatteryValue = batteryTask.Result;
                return LastBatteryValue;
            }
            return LastBatteryValue;
        }
        public double GetBatteryLevel()
        {
            return LastBatteryValue;
        }
    }
}
