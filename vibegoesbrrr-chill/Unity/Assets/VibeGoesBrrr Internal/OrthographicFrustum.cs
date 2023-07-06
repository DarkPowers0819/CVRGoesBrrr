using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CVRGoesBrrr;

namespace CVRGoesBrrr
{
#if UNITY_STANDALONE
[ExecuteInEditMode]
#endif
  public class OrthographicFrustum : MonoBehaviour
  {
    private bool mActive = false;

    void Update()
    {
      var cam = transform.parent.GetComponent<Camera>();
      transform.position = transform.parent.position + (cam.farClipPlane + cam.nearClipPlane) / 2 * transform.forward;
      transform.localScale = new Vector3(
          cam.orthographicSize * 2 / transform.parent.lossyScale.x,
          cam.orthographicSize * 2 / transform.parent.lossyScale.y,
          (cam.farClipPlane - cam.nearClipPlane) / transform.parent.lossyScale.z
      );

      var sensor = transform.parent.GetComponent<OrthographicDepth>();
      if (sensor != null) {
        var renderer = GetComponent<MeshRenderer>();
        if (sensor.Depth > 0 && !mActive) {
          renderer.materials[0].color = new Color(119 / 255f, 255 / 255f, 109 / 255f, 159 / 255f);
          mActive = true;
        } else if (sensor.Depth <= 0 && mActive) {
          renderer.materials[0].color = new Color(255 / 255f, 125 / 255f, 109 / 255f, 159 / 255f);
          mActive = false;
        }
      }
    }
  }
}
