Shader "EchoProtocol/AlienHuntFlame"
{
    Properties { _BaseMap("Flame",2D)="white"{} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.uv=v.uv; o.color=v.color; return o; }
            half4 frag(Varyings i):SV_Target { half4 c=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*i.color; c.rgb*=2; return c; }
            ENDHLSL
        }
    }
}
