using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace VibeGoesBrrr
{
    public class DPSOrifice : Taker
    {
        public static float MiddleID = 0.05f;
        public static float EntranceID_A = 0.01f;
        public static float EntranceID_B = 0.02f;

        public Light mLight1 = null;
        public Light mLight2 = null;

        public DPSOrifice(string name, SensorOwnerType ownerType, GameObject gameObject)
          : base(name, ownerType, gameObject)
        { }

        public override bool Active
        {
            get
            {
                if (mLight1 != null && mLight2 != null)
                {
                    return mLight1.gameObject.activeInHierarchy && mLight2.gameObject.activeInHierarchy;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
