Shader "ECHO/Loading/BloodProgress"
{
    Properties
    {
        [PerRendererData] _MainTex ("White Fill Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EffectTime ("Effect Time", Float) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex HorrorUIVert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "HorrorLoadingUI.cginc"

            fixed4 frag(HorrorUIVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                fixed4 sprite = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float grain = HorrorHash(floor(uv * float2(850, 52)));
                float stain = HorrorNoise(uv * float2(22, 4));
                float fine = HorrorNoise(uv * float2(94, 11));
                float crackField = sin(uv.x * 136 + stain * 13 + uv.y * 9) + sin(uv.y * 34 + uv.x * 5) * 0.4;
                float cracks = 1 - smoothstep(0.025, 0.105, abs(crackField));
                float chips = step(0.79, fine) * step(0.47, grain);
                float edge = smoothstep(0, 0.15, uv.y) * (1 - smoothstep(0.83, 1, uv.y));
                float3 blood = lerp(float3(0.19, 0.003, 0.014), float3(0.96, 0.032, 0.042), stain * 0.76 + fine * 0.24);
                blood += float3(0.25, 0.033, 0.021) * grain * edge;
                // Uneven wet highlights remain red/salmon rather than turning the whole fill white.
                float wet = smoothstep(0.55, 0.84, fine) * smoothstep(0.28, 0.70, stain);
                blood = lerp(blood, float3(1.0, 0.23, 0.16), wet * 0.65 * edge);
                blood *= (0.65 + edge * 0.35) * (1 - cracks * 0.78) * (1 - chips * 0.8);
                float scanLine = exp(-abs(uv.x - frac(_EffectTime * 0.105)) * 240);
                float glow = exp(-abs(uv.x - frac(_EffectTime * 0.105)) * 30);
                blood += float3(0.75, 0.12, 0.07) * scanLine * (0.6 + grain * 0.4);
                blood += float3(0.16, 0.006, 0.009) * glow;
                // Image.Filled supplies the measured geometry. This shader never reveals extra area.
                return HorrorUIClip(fixed4(saturate(blood) * sprite.rgb, sprite.a) * input.color, input);
            }
            ENDCG
        }
    }
}
