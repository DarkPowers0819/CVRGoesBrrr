using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VibeGoesBrrr
{
    public enum SensorOwnerType { LocalPlayer, RemotePlayer, World };

    public abstract class Sensor
    {
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
    }
}