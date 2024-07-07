using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace CVRGoesBrrr
{
    public class Giver : Sensor
    {
        public static Regex Pattern = new Regex(@"^\s*?(Thrust\s*Vector)\s*(.*)$", RegexOptions.IgnoreCase);

        public float mValue;
        public float mBaseLength=-10;
        public GameObject mMeshObject;
        public Mesh mMesh;

        public float Length
        {
            get
            {
                if (mBaseLength < -1)
                {
                    mBaseLength = CalculateGiverMeshLength(mMesh);
                }
                float forwardMagnitude = mMeshObject.transform.TransformVector(Vector3.forward).magnitude;
                //Util.DebugLog("Giver is on Layer="+this.mMeshObject.layer);
                return mBaseLength * forwardMagnitude;
            }
        }

        protected GameObject mGameObject;

        public Giver(string name, SensorOwnerType ownerType, GameObject gameObject, GameObject meshObject, Mesh mesh)
          : this(Pattern, name, ownerType, gameObject, meshObject, mesh)
        { }

        public Giver(Regex pattern, string name, SensorOwnerType ownerType, GameObject gameObject, GameObject meshObject, Mesh mesh)
          : base(pattern, name, ownerType)
        {
            mGameObject = gameObject;
            mMeshObject = meshObject;
            mMesh = mesh;
        }

        public override GameObject GameObject => mGameObject;
        public override bool Enabled { get; set; }
        public override float Value { get => mValue;}

        /// <summary>
        /// based on how DPS sets up penetrator we can assume the furthest forward vertex has the highest Z value.
        /// another benefit of DPS is that anything in -Z is not bendable, so usable lenght is the same as the greatest z value.
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private static float CalculateGiverMeshLength(Mesh mesh)
        {
            float length = 0f;
            var vertices = mesh.vertices;
            if (vertices.Length<100)
            {
                Util.Warn("penetrator is extreamly low poly="+ vertices.Length);
            }
            foreach (var vertex in vertices)
            {
                if(length<vertex.z)
                {
                    length= vertex.z;
                }
            }
            if (length <0.1 || length > 2)
            {
                Util.Warn("penetrator has unusual length: " + length)
;            }
            return length;
        }
    }
}
