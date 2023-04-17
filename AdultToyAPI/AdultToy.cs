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
    }
}
