using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CVRGoesBrrr
{
    public enum CVRLayers : int
    {
        Default = 0,
        TransparentFX = 1,
        IgnoreRaycast = 2,
        Water = 4,
        UI = 5,
        PlayerLocal = 8,
        PlayerClone = 9,
        PlayerNetwork = 10,
        MirrorReflection = 11,
        CVRReserved1 = 12,
        CVRReserved2 = 13,
        CVRReserved3 = 14,
        CVRReserved4 = 15,
        PostProcessing = 16,
        CVRPickup = 17,
        CVRInteractable = 18
    }
    public class CVRLayersUtil
    {
        public static int LayerToCullingMask(CVRLayers layer)
        {
            return 1 << (int)layer;
        }
    }
}
