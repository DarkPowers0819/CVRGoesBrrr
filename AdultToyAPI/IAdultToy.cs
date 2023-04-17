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
    }
}
