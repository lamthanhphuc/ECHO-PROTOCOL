Shader "ECHO/Loading/LogoSignal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Logo RGBA", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EffectTime ("Effect Time", Float) = 0
        _GlitchIntensity ("Signal Damage", Range(0,1)) = 0.12
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
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "HorrorLoadingUI.cginc"
            float _GlitchIntensity;

            fixed4 frag(HorrorUIVaryings input) : SV_Target
            {
                float frameIndex = floor(_EffectTime * 11);
                float band = floor(input.uv.y * 43);
                float seed = HorrorHash(float2(band, frameIndex));
                float tear = step(0.966, seed) * saturate(_GlitchIntensity);
                float offset = (HorrorHash(float2(frameIndex, band + 18)) - 0.5) * 0.043 * tear;
                float2 uv = input.uv + float2(offset, 0);
                fixed4 logo = tex2D(_MainTex, uv) + _TextureSampleAdd;
                float flicker = 1 - saturate(_GlitchIntensity) *
                    (0.035 * (0.5 + 0.5 * sin(_EffectTime * 7.3)) + 0.06 * step(0.97, HorrorHash(float2(frameIndex, 2))));
                logo.rgb = lerp(logo.rgb, float3(max(logo.r, 0.65), logo.g * 0.16, logo.b * 0.12), tear * 0.8);
                logo.rgb *= flicker;
                // Keep the transparent artwork's alpha; no opaque shader backing is introduced.
                return HorrorUIClip(logo * input.color, input);
            }
            ENDCG
        }
    }
}
