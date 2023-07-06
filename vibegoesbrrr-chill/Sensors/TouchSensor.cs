using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace CVRGoesBrrr
{
    public class TouchSensor : Sensor
    {
        public static Regex Pattern = new Regex(@"^\s*?(Touch\s*?Zone|Vibe\s*?Sensor|HapticsSensor_)\s*(.*)$", RegexOptions.IgnoreCase);
        public static Shader Shader;

        public readonly Camera Camera;
        private OrthographicDepth mSampler;

        public TouchSensor(string name, SensorOwnerType ownerType, Camera camera)
          : base(Pattern, name, ownerType)
        {
            Camera = camera;
        }

        public override GameObject GameObject => Camera.gameObject;

        public override bool Enabled
        {
            get
            {
                if (Camera != null)
                {
                    return Camera.enabled;
                }
                else
                {
                    return false;
                }
            }

            set
            {
                if (Camera != null)
                {
                    Camera.enabled = value;
                    if (mSampler != null)
                    {
                        mSampler.enabled = value;
                    }
                }
            }
        }

        public override float Value
        {
            get
            {
                if (mSampler == null)
                {

                    mSampler = Camera.gameObject.AddComponent<OrthographicDepth>();
                    mSampler.SetShader(Shader);
                }
                return mSampler.Depth;
            }
        }
    }
}
