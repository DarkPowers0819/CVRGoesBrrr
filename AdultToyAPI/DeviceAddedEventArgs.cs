using Buttplug.Client;
using System;

namespace AdultToyAPI
{
    public class DeviceAddedEventArgs : EventArgs
    {
        public IAdultToy AdultToy;
        public DeviceAddedEventArgs(IAdultToy adultToy)
        {
            AdultToy = adultToy;
        }
    }
}