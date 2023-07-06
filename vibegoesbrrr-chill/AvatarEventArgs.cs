using ABI_RC.Core.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CVRGoesBrrr
{
    /// <summary>
    /// An event class for notifying our virtual sensors that an avatar has been detected.
    /// </summary>
    public class AvatarEventArgs : EventArgs
    {
        public GameObject Avatar;
        public PlayerDescriptor Player;
    }
}
