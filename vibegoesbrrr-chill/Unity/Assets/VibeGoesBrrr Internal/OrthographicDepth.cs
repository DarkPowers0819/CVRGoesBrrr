using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
#if !UNITY_STANDALONE
using System;
#endif

namespace CVRGoesBrrr
{
#if UNITY_STANDALONE
  [ExecuteInEditMode]
#endif
    public class OrthographicDepth : MonoBehaviour
    {
        private const int TargetTextureWidth=14;
        private const int TargetTextureHeight = 14;

        public float Depth = 0f;

        private Camera mCamera;
        private AsyncGPUReadbackRequest? mReadRequest;

        public void SetShader(Shader shader)
        {
            mCamera.SetReplacementShader(shader, null);
        }

        void Awake()
        {
            mCamera = GetComponent<Camera>();
            mCamera.clearFlags = CameraClearFlags.SolidColor;
            /*
             * from testing in game:
             * Player Local = 8
             * Remote Player =
             * Props = 0? this doesn't make sense
             * 
             * So what I have figured out so far is that props should probably be using the CVRPickup or CVRInteractable layers
             * but this is not being enforced by the game. Instead it appears whatever layer the prop was uploaded on is used.
             * this will make it very dificult to detect props via cameras because the layer used is the "Default" layer which
             * is probably also used by worlds.
             * 
             * the values for the culling Mask below were discovered as they are generated into the unity sceene files.
            */
            mCamera.cullingMask = 394240; // CVR: PlayerNetwork | CVRPickup | CVRInteractable
#if DEBUG
            mCamera.cullingMask |= CVRLayersUtil.LayerToCullingMask(CVRLayers.PlayerLocal);
#endif
            mCamera.backgroundColor = Color.black;
            mCamera.depthTextureMode = DepthTextureMode.None;
            mCamera.SetReplacementShader(Shader.Find("Orthographic Depth"), null);
            
        }

        private bool NeedsNewTexture(Camera mCamera)
        {
            if (mCamera == null || mCamera.targetTexture == null)
                return true;
            return mCamera.targetTexture.width != TargetTextureWidth || mCamera.targetTexture.height != TargetTextureHeight || mCamera.targetTexture.depth != 0 || mCamera.targetTexture.format != RenderTextureFormat.R8;
        }

        void OnDisable()
        {
            Depth = 0f;
        }

        void Update()
        {
            if (NeedsNewTexture(mCamera))
            {
                mCamera.targetTexture = new RenderTexture(TargetTextureWidth, TargetTextureHeight, 0, RenderTextureFormat.R8);
                Util.Warn("touch zone does not have ideal target texture settings, creating new texture");
                Util.Warn("target texture should be 14x14 width depth=0 and and format=R8_UNORM");
            }
            if (mReadRequest == null)
            {
                mReadRequest = AsyncGPUReadback.Request(mCamera.targetTexture);
            }

            var request = (AsyncGPUReadbackRequest)mReadRequest;
            if (request.done)
            {
                try
                {
                    var texture = new Texture2D(mCamera.targetTexture.width, mCamera.targetTexture.height, TextureFormat.R8, false);
                    // #if UNITY_STANDALONE
                    var data = request.GetData<float>();
                    texture.LoadRawTextureData(data);
                    // #else
                    //           var data = request.GetDataRaw(0);
                    //           texture.LoadRawTextureData(data, request.layerDataSize);
                    // #endif

                    //var pixels = BitConverter.texture.GetRawTextureData();
                    //var pixels = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<float>((void*)texture.GetWritableImageData(0), (int)(texture.GetRawImageDataSize() / UnsafeUtility.SizeOf<float>()), Allocator.None);
                    var pixels = texture.GetPixels();
                    var n = 0;
                    var closeness = 0f;
                    foreach (var p in pixels)
                    {
                        if (p.r > 0)
                        {
                            closeness += p.r;
                            n++;
                        }
                    }
                    closeness = n > 0 ? closeness / n : 0;
                    var area = n / pixels.Length;

                    Depth = closeness;
                    //Util.DebugLog("camera is on layer=" + this.gameObject.layer);
                    //Util.DebugLog("Touch zone depth=" + Depth);
                    mReadRequest = null;
                }
                catch (System.InvalidOperationException)
                {
                    mReadRequest = null;
                }
            }
        }
    }
}
