using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CVRGoesBrrr
{
    public class DPSPenetrator : Giver
    {
        public static float TipID = 0.09f;
        // The Zawoo Hybrid Anthro Peen is set up with a value of .06 by accident
        public static float TipID_ZawooCompat = 0.06f;

        public Light mLight0;

        public DPSPenetrator(string name, SensorOwnerType ownerType, GameObject gameObject, GameObject meshObject, Mesh mesh, Light light)
          : base(name, ownerType, gameObject, meshObject, mesh)
        {
            mLight0 = light;
            this.Enabled = false;
        }

        public override bool Active
        {
            get
            {
                if (mLight0 != null)
                {
                    return mLight0.gameObject.activeInHierarchy;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
