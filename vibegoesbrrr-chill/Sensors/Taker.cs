using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace CVRGoesBrrr
{
    public class Taker : Sensor
    {
        public static Regex Pattern => Giver.Pattern; // Giver and Taker use the same pattern, since difference is based on mesh presence or DPS identification

        public float mCumulativeValue = 0f;
        public int mNumPenetrators = 0;

        protected GameObject mGameObject;

        public Taker(string name, SensorOwnerType ownerType, GameObject gameObject)
          : this(Pattern, name, ownerType, gameObject)
        { }

        public Taker(Regex pattern, string name, SensorOwnerType ownerType, GameObject gameObject)
          : base(pattern, name, ownerType)
        {
            mGameObject = gameObject;
        }

        public override GameObject GameObject => mGameObject;
        public override bool Enabled { get; set; }

        public override float Value
        {
            get
            {
                // Taker values are an average of all current givers, for that DP action �
                return mNumPenetrators > 0 ? mCumulativeValue / mNumPenetrators : 0f;
            }
        }
    }
}
