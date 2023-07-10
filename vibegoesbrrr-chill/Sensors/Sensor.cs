using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CVRGoesBrrr
{
    public enum SensorOwnerType { LocalPlayer, RemotePlayer, World };

    public abstract class Sensor
    {
        private float[] ArrayOfPastValues;
        private int PastValuesIndex=0;
        public virtual string Name => mName;
        public virtual string Type => mType;
        public virtual string Tag => mTag;
        public SensorOwnerType OwnerType => mOwnerType;
        public abstract GameObject GameObject { get; }
        public virtual bool Enabled { get; set; }

        public abstract float Value { get; }
        public void SetOwnerType(SensorOwnerType newOwnerType)
        {
            mOwnerType = newOwnerType;
        }
        public virtual bool Active
        {
            get
            {
                if (GameObject != null)
                {
                    return GameObject.activeInHierarchy;
                }
                else
                {
                    return false;
                }
            }
        }

        public Sensor(Regex pattern, string name, SensorOwnerType ownerType)
        {
            mName = name;
            mOwnerType = ownerType;
            var match = pattern.Match(mName);
            mType = match.Groups[1].Value;
            mTag = match.Groups[2].Value;
        }

        private string mName;
        private string mType;
        private string mTag;
        private SensorOwnerType mOwnerType;
        private string SafeName;

        internal string GetParameterName()
        {
            if(string.IsNullOrEmpty(SafeName))
            {
                Regex rgx = new Regex("[^a-zA-Z0-9 #]");
                SafeName = rgx.Replace(GameObject.name, "");
            }
            return SafeName;
        }

        internal void AddToAverage(float intensityValue)
        {
            if(ArrayOfPastValues!=null && ArrayOfPastValues.Length>0)
            {
                ArrayOfPastValues[PastValuesIndex++ % ArrayOfPastValues.Length] = intensityValue;
            }
        }
        internal float GetAverage()
        {
            if (ArrayOfPastValues != null && ArrayOfPastValues.Length > 0)
            {
                return ArrayOfPastValues.Average();
            }
            return 0;
        }
        internal void InitAverageValues(int frequency)
        {
            if (ArrayOfPastValues == null)
            {
                int length = 10 * frequency;
                ArrayOfPastValues = new float[length];
                for (int i = 0; i < length; i++)
                {
                    ArrayOfPastValues[i] = 0;
                }
            }
        }
        internal void RemoveAverageValues()
        {
            ArrayOfPastValues = null;
        }
    }
}