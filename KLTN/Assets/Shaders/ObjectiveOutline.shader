Shader "EchoProtocol/ObjectiveOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline Color", Color) = (0.0, 0.85, 1.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.001, 0.05)) = 0.012
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _PulseSpeed;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normal = normalize(input.normalOS);
                
                // Extrude vertex along normal in object space
                float3 extruded = input.positionOS.xyz + normal * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extruded);

                // Breathing pulse
                float pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);
                output.color = _OutlineColor * pulse;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(input.color.rgb, input.color.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
