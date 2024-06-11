using System;

namespace AdultToyAPI
{
    public class DeviceRemovedEventArgs : EventArgs
    {
        public IAdultToy AdultToy;
        public DeviceRemovedEventArgs(IAdultToy adultToy)
        {
            AdultToy = adultToy;
        }
    }
}