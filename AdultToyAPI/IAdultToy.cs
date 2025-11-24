using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdultToyAPI
{
    public interface IAdultToy
    {
        string GetName();
        int GetIndex();
        bool SupportsVibration();
        bool SupportsContraction();
        bool SupportsRotation();
        bool SupportsOscillate();
        bool SupportsInflate();
        bool SupportsPosition();

        bool HasBattery();
        /// <summary>
        /// Returns the Battery Level of the toy. Do not call from the same thread as the Device Added Event. Normalized between 0.0 and 1.0
        /// </summary>
        /// <returns></returns>
        double GetBatteryLevel();

        int MotorCount();
        /// <summary>
        /// returns a list of the supported motors of this device
        /// </summary>
        /// <returns></returns>
        List<MotorType> GetMotorTypes();
        /// <summary>
        /// stop all device motors
        /// </summary>
        void Stop();
    }
}
