using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdultToyAPI
{
    public interface IAdultToyAPI
    {
        bool IsConnected();
        event EventHandler<ErrorEventArgs> ErrorReceived;
        event EventHandler<DeviceRemovedEventArgs> DeviceRemoved;
        event EventHandler<DeviceAddedEventArgs> DeviceAdded;
        event EventHandler<ServerDisconnectEventArgs> ServerDisconnect;
        List<IAdultToy> GetConnectedDevices();
        void SetMotorSpeed(IAdultToy device, MotorType motor, float speed);
    }
    public enum MotorType
    {
        Vibrate = 0,
        Rotate = 1,
        Oscillate = 2,
        Constrict = 3,
        Inflate = 4,
        Position = 5
    }
}
