Shader "Perspective Depth" {
  SubShader {
    Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+3000" }

    Pass {
      Fog { Mode Off }
      Cull Off
      ZWrite On
      ZTest Always

      CGPROGRAM
      #include "UnityCG.cginc"
      #pragma vertex vert
      #pragma fragment frag

      struct appdata
      {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
      };
  
    //   struct v2f {
    //     float4 pos : POSITION;
    //     float3 Z : TEXCOORD0;
    // };
      struct v2f
      {
          float4 vertex : SV_POSITION;
          float2 uv : TEXCOORD0;
      };
  
      v2f vert(appdata v)
      {
          v2f o;
          o.vertex = UnityObjectToClipPos(v.vertex);
          o.uv = v.uv;
          return o;
      }
      // v2f vert (float4 vertex : POSITION) {
      //   // v2f o;
      //   // float4 oPos = UnityObjectToClipPos(vertex);
      //   // o.pos = oPos;
      //   // o.Z = oPos.zzz;
      //   // return o;
      //   v2f o;
      //   o.pos = UnityObjectToClipPos(vertex);
      //   o.Z = 0;
      //   return o;
      // }

      sampler2D _CameraDepthTexture;
      fixed4 frag(v2f i) : COLOR {
        // // return i.Z.xxxx;
        float depth = tex2D(_CameraDepthTexture, i.uv).r;
        depth = Linear01Depth(depth);
        // // depth = depth * _ProjectionParams.z;
        // // return float4(depth, depth, depth, 1);
        // return float4(0.5, 0.5, depth, 0);
        return float4(depth, depth, depth, depth);
      }
      ENDCG
    }
  }
}
