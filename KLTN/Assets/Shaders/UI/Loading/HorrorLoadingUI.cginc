#ifndef ECHO_HORROR_LOADING_UI_INCLUDED
#define ECHO_HORROR_LOADING_UI_INCLUDED
#include "UnityCG.cginc"
#include "UnityUI.cginc"

struct HorrorUIVertex
{
    float4 vertex : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct HorrorUIVaryings
{
    float4 vertex : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    half4 mask : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

sampler2D _MainTex;
float4 _MainTex_ST;
float4 _MainTex_TexelSize;
fixed4 _Color;
fixed4 _TextureSampleAdd;
float4 _ClipRect;
float _UIMaskSoftnessX, _UIMaskSoftnessY;
float _EffectTime;

HorrorUIVaryings HorrorUIVert(HorrorUIVertex input)
{
    HorrorUIVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.vertex = UnityObjectToClipPos(input.vertex);
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.color = input.color * _Color;
    float2 pixelSize = output.vertex.w;
    pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clipRect = clamp(_ClipRect, -2e10, 2e10);
    output.mask = half4(input.vertex.xy * 2 - clipRect.xy - clipRect.zw,
        0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
    return output;
}

fixed4 HorrorUIClip(fixed4 tint, HorrorUIVaryings input)
{
    #ifdef UNITY_UI_CLIP_RECT
        half2 maskFade = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
        tint.a *= maskFade.x * maskFade.y;
    #endif
    #ifdef UNITY_UI_ALPHACLIP
        clip(tint.a - 0.001);
    #endif
    return tint;
}

// Two small value-noise octaves, with no textures, mutable random state or post processing.
float HorrorHash(float2 position)
{
    position = frac(position * float2(123.34, 345.45));
    position += dot(position, position + 34.345);
    return frac(position.x * position.y);
}

float HorrorNoise(float2 position)
{
    float2 cell = floor(position);
    float2 blend = frac(position);
    blend = blend * blend * (3 - 2 * blend);
    return lerp(lerp(HorrorHash(cell), HorrorHash(cell + float2(1, 0)), blend.x),
        lerp(HorrorHash(cell + float2(0, 1)), HorrorHash(cell + 1), blend.x), blend.y);
}
#endif
