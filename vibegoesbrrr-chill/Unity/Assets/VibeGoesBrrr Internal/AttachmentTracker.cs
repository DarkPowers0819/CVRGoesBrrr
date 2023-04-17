using ABI.CCK.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VibeGoesBrrr.Unity.Assets.VibeGoesBrrr_Internal
{
    public class AttachmentTracker : MonoBehaviour
    {
        private bool WasAttached = false;
        public CVRAttachment AttachmentToMonitor;
        void Update()
        {
            if(AttachmentToMonitor!=null)
            {
                if(!WasAttached && AttachmentToMonitor.IsAttached())
                {
                    // need to trigger attached
                    CVRHooks.OnPropAttached(this.AttachmentToMonitor);
                    WasAttached = true;
                    Util.DebugLog("Attachment was attached " + gameObject.GetInstanceID());
                }
                if(WasAttached && !AttachmentToMonitor.IsAttached())
                {
                    // need to dettach
                    CVRHooks.OnPropDettached(this.AttachmentToMonitor);
                    Util.DebugLog("Attachment was dettached " + gameObject.GetInstanceID());
                    WasAttached = false;
                }
            }
        }
    }
}
