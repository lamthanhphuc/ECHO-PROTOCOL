Shader "Custom/Outline Fill"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest("ZTest", Float) = 0

        [HDR]
        _OutlineColor("Outline Color", Color) = (0, 1, 1, 1)

        _OutlineWidth("Outline Width", Range(0, 10)) = 2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "OutlineFill"

            Cull Off
            ZTest [_ZTest]
            ZWrite Off

            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                Comp NotEqual
            }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _ZTest;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 normalOS =
                    dot(
                        input.smoothNormalOS,
                        input.smoothNormalOS)
                    > 0.0001
                        ? input.smoothNormalOS
                        : input.normalOS;

                float3 positionWS =
                    TransformObjectToWorld(
                        input.positionOS.xyz);

                float3 normalWS =
                    TransformObjectToWorldNormal(
                        normalOS);

                normalWS =
                    normalize(normalWS);

                positionWS +=
                    normalWS
                    * (_OutlineWidth * 0.01);

                output.positionCS =
                    TransformWorldToHClip(
                        positionWS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
