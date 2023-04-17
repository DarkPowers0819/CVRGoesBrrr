using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ABI_RC.Core.Player;
using UnityEngine;
using static MelonLoader.MelonLogger;
using UnityEngine.Events;

namespace VibeGoesBrrr
{
    class ThrustVectorProvider : ISensorProvider, IDisposable
    {

        public bool Enabled { get; set; }
        public IEnumerable<Sensor> Sensors => mSensorInstances.Values;
        public int Count => mSensorInstances.Count;
        public event EventHandler<Sensor> SensorDiscovered;
        public event EventHandler<Sensor> SensorLost;

        private Dictionary<int, Sensor> mSensorInstances = new Dictionary<int, Sensor>();
        private Dictionary<int, Giver> mGivers = new Dictionary<int, Giver>();
        private Dictionary<int, Taker> mTakers = new Dictionary<int, Taker>();

        public ThrustVectorProvider()
        {
            // m_avatarSetupCompleted += OnAvatarIsReady;
            // PlayerSetup.Instance.avatarSetupCompleted.AddListener(m_avatarSetupCompleted);
            CVRHooks.AvatarIsReady += OnAvatarIsReady;
            CVRHooks.PropIsReady += OnPropIsReady;
            CVRHooks.PropAttached += CVRHooks_PropAttached;
            CVRHooks.PropDettached += CVRHooks_PropDettached;
        }

        private void CVRHooks_PropDettached(object sender, GameObject e)
        {
            if (mSensorInstances.ContainsKey(e.GetInstanceID()))
            {
                Util.DebugLog("changing prop owner to world");
                mSensorInstances[e.GetInstanceID()].SetOwnerType(SensorOwnerType.World);
            }
        }

        private void CVRHooks_PropAttached(object sender, GameObject e)
        {
            if (mSensorInstances.ContainsKey(e.GetInstanceID()))
            {
                Util.DebugLog("changing prop owner to Local Player");
                mSensorInstances[e.GetInstanceID()].SetOwnerType(SensorOwnerType.LocalPlayer);
            }
        }

        public void Dispose()
        {
            CVRHooks.AvatarIsReady -= OnAvatarIsReady;
            CVRHooks.PropIsReady -= OnPropIsReady;
        }
        private void OnPropIsReady(object sender, GameObject obj)
        {
            foreach (var light in obj.GetComponentsInChildren<Light>(true))
            {
                MatchDPSLight(light, SensorOwnerType.World);
            }
            foreach (var gameObject in obj.GetComponentsInChildren<GameObject>(true))
            {
                MatchThrustVector(gameObject, SensorOwnerType.World);
            }
        }

        public void OnSceneWasInitialized()
        {
            // In CVR, avatars load before the main scene is initialized.
            // We shouldn't need this as long as OnUpdate removes destroyed sensors :)
            // foreach (var kv in mSensorInstances) {
            //   SensorLost?.Invoke(this, kv.Value);
            // }
            // mSensorInstances.Clear();
            // mGivers.Clear();
            // mTakers.Clear();

            foreach (var light in Resources.FindObjectsOfTypeAll(typeof(Light)) as Light[])
            {
                if (CVRHooks.LocalAvatar == null || !light.transform.IsChildOf(CVRHooks.LocalAvatar.transform))
                {
                    MatchDPSLight(light, SensorOwnerType.World);
                }
            }
            foreach (var gameObject in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
            {
                if (CVRHooks.LocalAvatar == null || !gameObject.transform.IsChildOf(CVRHooks.LocalAvatar.transform))
                {
                    MatchThrustVector(gameObject, SensorOwnerType.World);
                }
            }
        }

        private void OnAvatarIsReady(object sender, AvatarEventArgs args)
        {
            foreach (var light in args.Avatar.GetComponentsInChildren<Light>(true))
            {
                MatchDPSLight(light, args.Player ? SensorOwnerType.RemotePlayer : SensorOwnerType.LocalPlayer);
            }
            foreach (var gameObject in args.Avatar.GetComponentsInChildren<GameObject>(true))
            {
                MatchThrustVector(gameObject, args.Player ? SensorOwnerType.RemotePlayer : SensorOwnerType.LocalPlayer);
            }
        }

        private void MatchDPSLight(Light light, SensorOwnerType sensorType)
        {
            if (DPSLightId(light, DPSPenetrator.TipID) || DPSLightId(light, DPSPenetrator.TipID_ZawooCompat))
            {
                // Step up the hierarchy until we find an object with a mesh as child
                GameObject root;
                for (root = light.gameObject; root != null && root.GetComponentInChildren<MeshRenderer>(true) == null && root.GetComponentInChildren<SkinnedMeshRenderer>(true) == null; root = root.transform.parent?.gameObject) ;
                if (root == null)
                {
                    Error("Discovered a misconfigured Dynamic Penetration System penetrator. Penetrators must have a mesh somewhere in its hierarchy!");
                    return;
                }

                GameObject meshObject;
                Mesh mesh;
                var meshRenderer = root.GetComponentInChildren<MeshRenderer>(true);
                if (meshRenderer)
                {
                    meshObject = meshRenderer.gameObject;
                    mesh = meshRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                }
                else
                {
                    var skinnedMeshRenderer = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    meshObject = skinnedMeshRenderer?.gameObject;
                    mesh = skinnedMeshRenderer?.sharedMesh;
                }
                if (mesh == null)
                {
                    Error("Misconfigured Dynamic Penetration System penetrator; Couldn't find mesh");
                    return;
                }

                var penetrator = new DPSPenetrator(root.name, sensorType, root, meshObject, mesh, light);
                mGivers[root.GetInstanceID()] = penetrator;
                mSensorInstances[root.GetInstanceID()] = penetrator;
                SensorDiscovered?.Invoke(this, penetrator);
            }
            else if (DPSLightId(light, DPSOrifice.MiddleID) || DPSLightId(light, DPSOrifice.EntranceID_A) || DPSLightId(light, DPSOrifice.EntranceID_B))
            {
                var gameObject = light.gameObject.transform.parent.gameObject;
                DPSOrifice orifice;
                if (!mTakers.ContainsKey(gameObject.GetInstanceID()))
                {
                    orifice = new DPSOrifice(gameObject.name, sensorType, gameObject);
                }
                else
                {
                    orifice = mTakers[gameObject.GetInstanceID()] as DPSOrifice;
                }
                if (DPSLightId(light, DPSOrifice.MiddleID))
                {
                    orifice.mLight1 = light;
                }
                else if (DPSLightId(light, DPSOrifice.EntranceID_A) || DPSLightId(light, DPSOrifice.EntranceID_B))
                {
                    orifice.mLight2 = light;
                }
                mTakers[gameObject.GetInstanceID()] = orifice;
                mSensorInstances[gameObject.GetInstanceID()] = orifice;
                if (orifice.mLight1 != null && orifice.mLight2 != null)
                {
                    SensorDiscovered?.Invoke(this, orifice);
                }
            }
        }

        private static bool DPSLightId(Light light, float id)
        {
            return light.color.maxColorComponent < 0.01f && Mathf.Abs((light.range % 0.1f) - id) < 0.001f;
        }

        private void MatchThrustVector(GameObject gameObject, SensorOwnerType sensorType)
        {
            if (!Giver.Pattern.Match(gameObject.name).Success || !Taker.Pattern.Match(gameObject.name).Success) return;

            // Don't overwrite DPS sensors if user does manual configuration
            if (mSensorInstances.ContainsKey(gameObject.GetInstanceID()))
            {
                Util.DebugLog($"Skipping ThustVector \"{gameObject.name}\" since it was already claimed by DPS");
                return;
            }

            // If object contains mesh, treat it as a penetrator, otherwise orifice
            GameObject meshObject;
            Mesh mesh;
            var meshRenderer = gameObject.GetComponentInChildren<MeshRenderer>(true);
            if (meshRenderer)
            {
                meshObject = meshRenderer.gameObject;
                mesh = meshRenderer.GetComponent<MeshFilter>()?.sharedMesh;
            }
            else
            {
                var skinnedMeshRenderer = gameObject.GetComponentInChildren<SkinnedMeshRenderer>(true);
                meshObject = skinnedMeshRenderer?.gameObject;
                mesh = skinnedMeshRenderer?.sharedMesh;
            }

            if (mesh != null)
            {
                var penetrator = new Giver(gameObject.name, sensorType, gameObject, meshObject, mesh);
                mGivers[gameObject.GetInstanceID()] = penetrator;
                mSensorInstances[gameObject.GetInstanceID()] = penetrator;
                SensorDiscovered?.Invoke(this, penetrator);
            }
            else
            {
                var orifice = new Taker(gameObject.name, sensorType, gameObject);
                mTakers[gameObject.GetInstanceID()] = orifice;
                mSensorInstances[gameObject.GetInstanceID()] = orifice;
                SensorDiscovered?.Invoke(this, orifice);
            }
        }

        public void OnUpdate()
        {
            // Check for destructions
            var lostSensors = new List<int>();
            foreach (var kv in mSensorInstances)
            {
                if (kv.Value.GameObject == null)
                {
                    lostSensors.Add(kv.Key);
                }
            }
            foreach (var id in lostSensors)
            {
                var lostSensor = mSensorInstances[id];
                mSensorInstances.Remove(id);
                mGivers.Remove(id);
                mTakers.Remove(id);
                SensorLost?.Invoke(this, lostSensor);
            }

            // Zero sensor values
            foreach (var penetrator in mGivers.Values)
            {
                penetrator.mValue = 0f;
            }
            foreach (var orifice in mTakers.Values)
            {
                orifice.mCumulativeValue = 0f;
                orifice.mNumPenetrators = 0;
            }

            // Update penetrations
            foreach (var giver in mGivers.Values)
            {
                if (!giver.Active) continue;

                var p0 = giver.mMeshObject.transform.position;

                foreach (var taker in mTakers.Values)
                {
                    if (!taker.Active) continue;
                    if (!giver.Enabled && !taker.Enabled) continue;

                    // Simple euclidian distance
                    // Dynamic Penetration System doesn't take the increased distance of the bezier into account, it simply stretches the penetrator to fit along it, so this is actually fine!
                    var p1 = taker.GameObject.transform.position;
                    // TODO: Angle limit?
                    float distance = Vector3.Distance(p0, p1);
                    float depth = Math.Max(0f, Math.Min(1 - distance / giver.Length, 1f));
                    if (depth > giver.mValue)
                    {
                        // Util.DebugLog($"{giver.Name}: 1 - {distance} / {giver.Length} = {depth}");
                        giver.mValue = depth;
                    }
                    if (depth > 0)
                    {
                        taker.mCumulativeValue += depth;
                        taker.mNumPenetrators += 1;
                    }
                }
            }
        }
    }
}
