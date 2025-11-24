using System;
using System.Collections.Generic;
using ABI_RC.Core.Player;
using ABI.CCK.Components;
using UnityEngine;
using static MelonLoader.MelonLogger;
using CVRGoesBrrr.CVRIntegration;

namespace CVRGoesBrrr
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
            CVRHooks.LocalAvatarIsReady += LocalAvatarIsReady;
            CVRHooks.RemoteAvatarIsReady += RemoteAvatarIsReady;
            CVRHooks.PropIsReady += OnPropIsReady;
            CVRHooks.PropAttached += CVRHooks_PropAttached;
            CVRHooks.PropDettached += CVRHooks_PropDettached;
        }

        private void RemoteAvatarIsReady(PuppetMaster puppetMaster, PlayerDescriptor playerDescriptor)
        {
            ScanAvatarHierarchy(puppetMaster.AvatarObject, false);
        }

        private void LocalAvatarIsReady()
        {
            ScanAvatarHierarchy(PlayerSetup.Instance.AvatarObject, true);
        }

        private void CVRHooks_PropDettached(CVRAttachment attachment)
        {
            if (mSensorInstances.ContainsKey(attachment.gameObject.GetInstanceID()))
            {
                Util.DebugLog("changing prop owner to world");
                mSensorInstances[attachment.gameObject.GetInstanceID()].SetOwnerType(SensorOwnerType.World);
            }
        }

        private void CVRHooks_PropAttached(CVRAttachment attachment)
        {
            if (mSensorInstances.ContainsKey(attachment.gameObject.GetInstanceID()))
            {
                Util.DebugLog("changing prop owner to Local Player");
                mSensorInstances[attachment.gameObject.GetInstanceID()].SetOwnerType(SensorOwnerType.LocalPlayer);
            }
        }

        public void Dispose()
        {
            CVRHooks.LocalAvatarIsReady -= LocalAvatarIsReady;
            CVRHooks.RemoteAvatarIsReady -= RemoteAvatarIsReady;
            CVRHooks.PropIsReady -= OnPropIsReady;
            CVRHooks.PropAttached -= CVRHooks_PropAttached;
            CVRHooks.PropDettached -= CVRHooks_PropDettached;
        }
        private void OnPropIsReady(CVRSpawnable prop)
        {
            foreach (var light in prop.GetComponentsInChildren<Light>(true))
            {
                MatchDPSLight(light, SensorOwnerType.World);
            }
            foreach (var gameObject in prop.GetComponentsInChildren<GameObject>(true))
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
                var sceneName = light.gameObject.scene.name;

                if (sceneName != "AdditiveContentScene" && sceneName != "DontDestroyOnLoad" && sceneName != "HideAndDontSave")
                {
                    MatchDPSLight(light, SensorOwnerType.World);
                }
            }
            foreach (var gameObject in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
            {
                var sceneName = gameObject.scene.name;

                if (sceneName != "AdditiveContentScene" && sceneName != "DontDestroyOnLoad" && sceneName != "HideAndDontSave")
                {
                    MatchThrustVector(gameObject, SensorOwnerType.World);
                }
            }
        }

        private void ScanAvatarHierarchy(GameObject avatarRoot, bool isLocal)
        {
            foreach (var light in avatarRoot.GetComponentsInChildren<Light>(true))
            {
                MatchDPSLight(light, isLocal ? SensorOwnerType.LocalPlayer : SensorOwnerType.RemotePlayer);
            }
            foreach (var gameObject in avatarRoot.GetComponentsInChildren<GameObject>(true))
            {
                MatchThrustVector(gameObject, isLocal ? SensorOwnerType.LocalPlayer : SensorOwnerType.RemotePlayer);
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
            if (!Giver.Pattern.Match(gameObject.name).Success) return;

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

        public void OnUpdate(HashSet<Sensor> activeSensors)
        {
            // Check for destructions
            var lostSensors = new Dictionary<int,Sensor>();
            foreach (var kv in mSensorInstances)
            {
                if (kv.Value.GameObject == null)
                {
                    lostSensors.Add(kv.Key,kv.Value);
                }
            }
            foreach (var kv in lostSensors)
            {
                mSensorInstances.Remove(kv.Key);
                mGivers.Remove(kv.Key);
                mTakers.Remove(kv.Key);
                SensorLost?.Invoke(this, kv.Value);
            }

            foreach (var orifice in mTakers.Values)
            {
                orifice.mCumulativeValue = 0f;
                orifice.mNumPenetrators = 0;
            }
            //ZeroOutSensors(activeSensors);
            if (activeSensors == null)
            {
                CalculateUpdates();
            }
            else
            {
                CalculateSensorUpdates(activeSensors);
            }
        }

        private void ZeroOutSensors(HashSet<Sensor> activeSensors)
        {
            foreach(var sensor in activeSensors)
            {
                if(sensor is Giver)
                {
                    (sensor as Giver).mValue = 0;
                }
                if(sensor is Taker)
                {
                    (sensor as Taker).mNumPenetrators = 0;
                    (sensor as Taker).mCumulativeValue = 0;
                }
            }
        }

        private void CalculateSensorUpdates(Giver giver, Taker taker)
        {
            if(giver.mMeshObject==null)
            {
                return;
            }
            if(taker.GameObject==null)
            {
                return;
            }
            var p0 = giver.mMeshObject.transform.position;
            var p1 = taker.GameObject.transform.position;
            
            if (!taker.Active || !giver.Active) return;
            float distance = Vector3.Distance(p0, p1);
            float depth = Math.Max(0f, Math.Min(1 - distance / giver.Length, 1f));
            //if (depth > giver.mValue)
            //{
            //    Util.DebugLog($"{giver.Name}: 1 - {distance} / {giver.Length} = {depth}");
                giver.mValue = depth;
            //}
            //if (depth > 0)
            //{
                taker.mCumulativeValue += depth;
                taker.mNumPenetrators += 1;
            //}
        }
        private void CalculateSensorUpdates(HashSet<Sensor> activeSensors)
        {
            foreach(var activeSensor in activeSensors)
            {
                if (activeSensor is TouchSensor)
                    continue;
                if (!activeSensor.Active)
                    continue;
                if(activeSensor.Active && activeSensor.Enabled)
                {
                    if(activeSensor is Giver)
                    {
                        foreach(var taker in mTakers.Values)
                        {
                            var giver = activeSensor as Giver;
                            CalculateSensorUpdates(giver, taker);
                        }    
                    }
                    if (activeSensor is Taker)
                    {
                        foreach (var giver in mGivers.Values)
                        {
                            var taker = activeSensor as Taker;
                            CalculateSensorUpdates(giver, taker);
                        }
                    }
                }
            }
        }

        private void CalculateUpdates()
        {
            // Update penetrations
            int calculations = 0;
            foreach (var giver in mGivers.Values)
            {
                giver.mValue = 0f;
                if (!giver.Active) continue;
                var p0 = giver?.mMeshObject?.transform?.position;
                if (p0 == null)
                {
                    continue;
                }

                foreach (var taker in mTakers.Values)
                {
                    if (!taker.Active) continue;
                    if (!taker.Enabled && !giver.Enabled) continue;

                    // Simple euclidian distance
                    // Dynamic Penetration System doesn't take the increased distance of the bezier into account, it simply stretches the penetrator to fit along it, so this is actually fine!
                    var p1 = taker?.GameObject?.transform?.position;
                    if (p1 == null)
                    {
                        continue;
                    }
                    // TODO: Angle limit?
                    float distance = Vector3.Distance(p0.Value, p1.Value);
                    calculations++;
                    float depth = Math.Max(0f, Math.Min(1 - distance / giver.Length, 1f));
                    //if (depth > giver.mValue)
                    //{
                    //    Util.DebugLog($"{giver.Name}: 1 - {distance} / {giver.Length} = {depth}");
                        giver.mValue = depth;
                    //}
                    //if (depth > 0)
                    //{
                        taker.mCumulativeValue += depth;
                        taker.mNumPenetrators += 1;
                    //}
                }
            }
            Util.DebugLog("ThurstVectorProvider ran " + calculations + " calculations");
        }
    }
}
