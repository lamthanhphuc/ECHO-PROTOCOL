//
//  OutlineFill.shader
//  QuickOutline (Universal Render Pipeline)
//
//  Created by Chris Nolet on 2/21/18.
//  URP compatibility & Clip-Space offset by EchoProtocol
//

Shader "Custom/Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0

    [HDR] _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "RenderPipeline" = "UniversalPipeline"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref 1
        Comp NotEqual
      }

      HLSLPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS : NORMAL;
        float3 smoothNormalOS : TEXCOORD3;
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float4 color : COLOR;
      };

      CBUFFER_START(UnityPerMaterial)
        float4 _OutlineColor;
        float _OutlineWidth;
        float _ZTest;
      CBUFFER_END

      Varyings vert(Attributes input) {
        Varyings output;

        // 1. Transform vertex directly to clip space (guarantees 100% exact alignment with Mask pass)
        float4 clipPos = TransformObjectToHClip(input.positionOS.xyz);

        // 2. Transform normal to clip space
        float3 n = any(input.smoothNormalOS) ? input.smoothNormalOS : input.normalOS;
        float3 normalWS = TransformObjectToWorldNormal(n, true);
        float3 normalCS = TransformWorldToHClipDir(normalWS, true);

        // 3. Screen-space pixel offset (constant thickness, centers perfectly around mesh without one-sided drift)
        float len = length(normalCS.xy);
        float2 screenNormal = len > 0.0001 ? (normalCS.xy / len) : float2(0.0, 0.0);
        float2 offset = screenNormal * (2.0 / _ScreenParams.xy) * _OutlineWidth * clipPos.w;
        clipPos.xy += offset;

        output.positionCS = clipPos;
        output.color = _OutlineColor;

        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        return input.color;
      }
      ENDHLSL
    }
  }
  FallBack Off
}
