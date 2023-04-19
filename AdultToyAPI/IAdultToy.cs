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
