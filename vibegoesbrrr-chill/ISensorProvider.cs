using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVRGoesBrrr
{
    public interface ISensorProvider
    {
        IEnumerable<Sensor> Sensors { get; }
        int Count { get; }
        event EventHandler<Sensor> SensorDiscovered;
        event EventHandler<Sensor> SensorLost;
    }
}
