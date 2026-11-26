Shader "ECHO/Salvage/Energy"
{
    Properties { [HDR]_Tint("Tint",Color)=(1,1,1,1) _Shape("Beam 0 / Field 2 / Particle 3",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;float _Shape;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
            Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.uv=v.uv;o.color=v.color;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float alpha;
                if(_Shape>2.5)
                {
                    float r=length((i.uv-.5)*2);alpha=exp(-r*r*5)*(1-smoothstep(.75,1,r));
                }
                else if(_Shape>1.5)
                {
                    float ribs=pow(saturate(sin(i.uv.y*75.398)),14);
                    float packets=pow(saturate(sin(i.uv.x*20-_Time.y*5)),18);
                    alpha=(.012+ribs*.025+packets*.035)*sin(i.uv.x*3.14159);
                }
                else
                {
                    float edge=pow(saturate(1-abs(i.uv.y*2-1)),1.6);
                    float packet=pow(saturate(sin(i.uv.x*22-_Time.y*7)),14);
                    alpha=edge*(.55+packet*.45)*smoothstep(0,.035,i.uv.x)*(1-smoothstep(.97,1,i.uv.x));
                }
                return half4(_Tint.rgb*i.color.rgb,alpha*_Tint.a*i.color.a);
            }
            ENDHLSL
        }
    }
}
