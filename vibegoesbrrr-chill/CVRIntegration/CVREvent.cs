using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVRGoesBrrr.CVRIntegration
{
    public class CVREvent
    {
        public CVREventType EventType;
        public Object EventData;
    }
    public enum CVREventType
    {
        LocalAvatarChange,RemoteAvatarChange,PropLoaded, PropAttached, PropDettached
    }
}
