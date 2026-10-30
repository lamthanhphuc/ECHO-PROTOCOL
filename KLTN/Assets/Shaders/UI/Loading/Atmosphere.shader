Shader "ECHO/Loading/Atmosphere"
{
    Properties
    {
        [PerRendererData] _MainTex ("White Overlay Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EffectTime ("Effect Time", Float) = 0
        _Intensity ("Overall Intensity", Range(0,2)) = 1
        _FogIntensity ("Low Fog", Range(0,1)) = 0.2
        _ParticleIntensity ("Drifting Dust", Range(0,1)) = 0.12
        _RedFlickerIntensity ("Emergency Light", Range(0,1)) = 0.15
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
        // Airborne light adds scattering; it must never paint dark shapes over the facility.
        Blend SrcAlpha One
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
            float _Intensity, _FogIntensity, _ParticleIntensity, _RedFlickerIntensity;

            float DustLayer(float2 uv, float scale, float seed)
            {
                float2 p = uv * float2(17, 10) * scale + float2(_EffectTime * 0.014, -_EffectTime * 0.035);
                float2 cell = floor(p);
                float2 center = float2(HorrorHash(cell + seed), HorrorHash(cell + seed + 23));
                float2 offset = (frac(p) - (0.13 + center * 0.74)) / float2(0.022, 0.04);
                float spark = saturate(1 - length(offset));
                return spark * step(0.96, HorrorHash(cell + seed + 51));
            }

            fixed4 frag(HorrorUIVaryings input) : SV_Target
            {
                float2 uv = input.uv;
                float noise0 = HorrorNoise(uv * float2(5.2, 3.1) + float2(_EffectTime * 0.014, -_EffectTime * 0.006));
                float noise1 = HorrorNoise(uv * float2(12, 5.4) + float2(-_EffectTime * 0.009, _EffectTime * 0.01));
                float low = 1 - smoothstep(0.025, 0.48, uv.y);
                float fog = low * (0.28 + noise0 * 0.5 + noise1 * 0.22) * _FogIntensity;
                float dust = (DustLayer(uv, 1, 7) + DustLayer(uv, 1.65, 41) * 0.5) * _ParticleIntensity;
                float2 lightOffset = (uv - float2(0.82, 0.75)) * float2(7, 10);
                float light = exp(-dot(lightOffset, lightOffset));
                float flicker = 0.84 + sin(_EffectTime * 3.7) * 0.06 + sin(_EffectTime * 16.1) * 0.025;
                flicker *= 1 - step(0.974, HorrorHash(float2(floor(_EffectTime * 3), 4))) * 0.13;
                float red = light * flicker * _RedFlickerIntensity;
                float alpha = saturate((fog + dust + red) * _Intensity);
                float3 contribution = float3(0.32, 0.29, 0.3) * fog
                    + float3(0.76, 0.61, 0.56) * dust + float3(0.86, 0.018, 0.012) * red;
                float3 tint = contribution / max(fog + dust + red, 0.001);
                fixed4 sprite = tex2D(_MainTex, uv) + _TextureSampleAdd;
                return HorrorUIClip(fixed4(tint * sprite.rgb, alpha * sprite.a) * input.color, input);
            }
            ENDCG
        }
    }
}
