Shader "Orthographic Depth" {
  SubShader {
    Pass
    {
      Tags { "LightMode" = "ForwardBase" }
      Cull Front
      Color (1,1,1,1)
    }

    Pass {
      Tags { "LightMode" = "ForwardBase" }
      Lighting Off 
      Fog { Mode Off }
      Cull Back
      ZWrite Off
      ZTest Always
      
      CGPROGRAM

      #pragma vertex vert
      #pragma fragment frag

      struct v2f {
        float4 pos : POSITION;
        float3 Z : TEXCOORD0;
      };

      v2f vert (float4 vertex : POSITION) {
        v2f o;
        float4 oPos = UnityObjectToClipPos(vertex);
        o.pos = oPos;
        o.Z = oPos.zzz;
        return o;
      }

      half4 frag(v2f i) : COLOR {
        return i.Z.xxxx;
      }

      ENDCG
    }
  }
}
