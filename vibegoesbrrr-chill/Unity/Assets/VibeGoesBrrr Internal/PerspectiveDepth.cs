using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class PerspectiveDepth : MonoBehaviour
{
  public float Depth = 0f;
  public Shader Shader;

  private new Camera camera;

  private AsyncGPUReadbackRequest? mReadRequest;

  void Awake()
  {
    mReadRequest = null;

    camera = GetComponent<Camera>();
    camera.clearFlags = CameraClearFlags.SolidColor;
    // camera.cullingMask = -1;
    camera.backgroundColor = Color.black;
    camera.targetTexture = new RenderTexture(48, 48, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
    camera.depthTextureMode = DepthTextureMode.Depth;
    // camera.depthTextureMode = DepthTextureMode.None;
    // camera.SetReplacementShader(Shader, null);
  }

  void Update()
  {
    if (mReadRequest == null) {
      mReadRequest = AsyncGPUReadback.Request(camera.targetTexture, 0, TextureFormat.RGBA32);
    }

    var request = (AsyncGPUReadbackRequest)mReadRequest;
    if (request.done) {
      try {
        var texture = new Texture2D(camera.targetTexture.width, camera.targetTexture.height, TextureFormat.RGBA32, false);

        var data = request.GetData<Color>();
        // Debug.Log($"Size: {data.Length} != {texture.GetPixels().Length}");
        texture.LoadRawTextureData(data);

        Depth = texture.GetPixel(texture.width / 2, texture.height/ 2).r;
        // Depth = texture.GetPixels32(0)[0].g;
        // Debug.Log($"{texture.GetPixels()[0][0]}, {texture.GetPixels()[0][1]}, {texture.GetPixels()[0][2]}, {texture.GetPixels()[0][3]}");

        mReadRequest = null;
      } catch (System.InvalidOperationException) {
        Debug.Log("Failed");
        mReadRequest = null;
      }
    }
  }

  void OnRenderImage(RenderTexture source, RenderTexture destination) {
    Graphics.Blit(source, destination, new Material(Shader));
  }
}
