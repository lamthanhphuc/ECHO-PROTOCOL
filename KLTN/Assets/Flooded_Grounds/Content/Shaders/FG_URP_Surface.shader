Shader "Flooded_Grounds/URP/Surface"
{
    Properties
    {
        _MainTex("Base", 2D) = "white" {}
        _BaseMap("Shadow/Depth Base", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Color("Tint", Color) = (1,1,1,1)
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpMap2("Water Second Normal", 2D) = "bump" {}
        _Spc("Metallic / Smoothness", 2D) = "black" {}
        _AO("Occlusion", 2D) = "white" {}
        _layer1Tex("Top Layer", 2D) = "white" {}
        _layer1Norm("Top Layer Normal", 2D) = "bump" {}
        _layer1Breakup("Top Breakup", 2D) = "white" {}
        _layer1Metal("Top Metallic", Float) = 0
        _layer1Tiling("Top Tiling", Float) = 10
        _layer1BreakupAmnt("Breakup Amount", Float) = 0.5
        _Power("Blend Amount", Float) = 1
        _Shift("Blend Height", Float) = 1
        _DetailBump("Detail Normal", 2D) = "bump" {}
        _DetailInt("Detail Intensity", Float) = 0.4
        _DetailTiling("Detail Tiling", Float) = 2
        _BumpMap1("Triplanar Normal", 2D) = "bump" {}
        _TexScale("Triplanar Scale", Float) = 1
        _BlendPlateau("Triplanar Blend", Float) = 0.2
        _Smth("Water Smoothness", Range(0,1)) = 0.9
        _Emis("Water Emission", Float) = 0.1
        _ScrollSpeed("Water Scroll", Float) = 0.2
        _WaveFreq("Wave Frequency", Float) = 20
        _WaveHeight("Wave Height", Float) = 0.1
        _BumpLerp("Water Normal Blend", Float) = 0.5
        _FGMode("0 Top Blend, 1 Triplanar, 2 Water", Float) = 0
        _Cutoff("Alpha Cutoff", Float) = 0.5
    }
    SubShader
    {
        // Object-space moss/triplanar/waves must retain each mesh's local coordinates.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "DisableBatching"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_BumpMap1); SAMPLER(sampler_BumpMap1);
            TEXTURE2D(_BumpMap2); SAMPLER(sampler_BumpMap2);
            TEXTURE2D(_Spc); SAMPLER(sampler_Spc);
            TEXTURE2D(_AO); SAMPLER(sampler_AO);
            TEXTURE2D(_layer1Tex); SAMPLER(sampler_layer1Tex);
            TEXTURE2D(_layer1Norm); SAMPLER(sampler_layer1Norm);
            TEXTURE2D(_layer1Breakup); SAMPLER(sampler_layer1Breakup);
            TEXTURE2D(_DetailBump); SAMPLER(sampler_DetailBump);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Color, _BaseColor;
                float _FGMode, _layer1Tiling, _layer1Metal, _layer1BreakupAmnt, _Power, _Shift;
                float _DetailInt, _DetailTiling, _TexScale, _BlendPlateau;
                float _Smth, _Emis, _ScrollSpeed, _WaveFreq, _WaveHeight, _BumpLerp, _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; float3 positionOS:TEXCOORD4; half fog:TEXCOORD5; UNITY_VERTEX_INPUT_INSTANCE_ID };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                if (_FGMode > 1.5) v.positionOS.y += sin(_Time.y / 20 * _WaveFreq + (v.positionOS.x + v.positionOS.z * 2) * 8) * _WaveHeight;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(v.normalOS,v.tangentOS);
                o.positionCS=p.positionCS; o.positionWS=p.positionWS; o.positionOS=v.positionOS.xyz;
                o.normalWS=n.normalWS; o.tangentWS=half4(n.tangentWS,v.tangentOS.w * GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_MainTex); o.fog=ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n=normalize(i.normalWS), t=normalize(i.tangentWS.xyz), b=cross(n,t)*i.tangentWS.w;
                float2 uv=i.uv;
                if (_FGMode>1.5) uv += _Time.y/20 * _ScrollSpeed * float2(1,.5);
                half4 color;
                half3 normalTS;
                half metallic, smoothness;
                half ao=SAMPLE_TEXTURE2D(_AO,sampler_AO,uv).r;
                if (_FGMode<.5)
                {
                    color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                    normalTS=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv));
                    half4 spec=SAMPLE_TEXTURE2D(_Spc,sampler_Spc,uv);
                    metallic=spec.r; smoothness=spec.a;
                    float2 topUV=i.uv*_layer1Tiling;
                    half breakup=SAMPLE_TEXTURE2D(_layer1Breakup,sampler_layer1Breakup,topUV).r;
                    half blend=saturate(pow(max(0,n.y*_Power+_Shift)*lerp(1,breakup,_layer1BreakupAmnt),3));
                    half4 top=SAMPLE_TEXTURE2D(_layer1Tex,sampler_layer1Tex,topUV);
                    color=lerp(color,top,blend); metallic=lerp(metallic,_layer1Metal,blend); smoothness=lerp(smoothness,top.a,blend);
                    normalTS=normalize(lerp(normalTS,UnpackNormal(SAMPLE_TEXTURE2D(_layer1Norm,sampler_layer1Norm,topUV)),blend));
                    normalTS.xy += UnpackNormal(SAMPLE_TEXTURE2D(_DetailBump,sampler_DetailBump,i.uv*_DetailTiling)).xy*_DetailInt;
                }
                else if (_FGMode<1.5)
                {
                    half3 weights=max(abs(TransformWorldToObjectNormal(n))-_BlendPlateau,0);
                    weights/=max(dot(weights,half3(1,1,1)),.001);
                    float3 p=i.positionOS*_TexScale;
                    color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,p.yz)*weights.x + SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,p.zx)*weights.y + SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,p.xy)*weights.z;
                    normalTS=half3(0,0,1); metallic=0; smoothness=color.a;
                }
                else
                {
                    color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,uv);
                    normalTS=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,uv));
                    normalTS=normalize(lerp(normalTS,UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap2,sampler_BumpMap2,i.uv-_Time.y*.01)),_BumpLerp));
                    color*=_Color; metallic=0; smoothness=_Smth;
                }
                InputData input=(InputData)0;
                input.positionWS=i.positionWS; input.normalWS=normalize(normalTS.x*t+normalTS.y*b+normalTS.z*n);
                input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                input.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                input.bakedGI=SampleSH(input.normalWS); input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                input.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;
                surface.albedo=color.rgb; surface.metallic=metallic; surface.smoothness=smoothness;
                surface.normalTS=normalTS; surface.occlusion=ao; surface.alpha=1;
                surface.emission=_FGMode>1.5 ? color.rgb*_Emis : 0;
                half4 output=UniversalFragmentPBR(input,surface); output.rgb=MixFog(output.rgb,i.fog); return output;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
