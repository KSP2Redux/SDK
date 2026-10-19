// KSP2DepthPasses.hlsl
//
// ShadowCaster, DepthOnly, DepthNormals and MotionVectors passes for KSP2 opaque and cutout shaders.
// Include after the shader's UnityPerMaterial block. For alpha-tested shaders, define
// KSP2_DEPTH_ALPHA_CLIP and a function float KSP2_DepthAlpha(float2 uv, float4 color) plus
// KSP2_DEPTH_UV(input) returning the UV it needs, before including this file.

#ifndef KSP2_URP_DEPTH_PASSES_INCLUDED
#define KSP2_URP_DEPTH_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct KSP2DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv0        : TEXCOORD0;
    float4 color      : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct KSP2DepthVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
    float4 color      : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

float3 _LightDirection;
float3 _LightPosition;

void KSP2_DepthClip(KSP2DepthVaryings input)
{
#if defined(KSP2_DEPTH_ALPHA_CLIP)
    clip(KSP2_DepthAlpha(input.uv, input.color));
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
}

KSP2DepthVaryings KSP2_ShadowVertex(KSP2DepthAttributes input)
{
    KSP2DepthVaryings output = (KSP2DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    output.positionCS = ApplyShadowClamping(positionCS);
    output.uv = input.uv0;
    output.normalWS = normalWS;
    output.color = input.color;
    return output;
}

half4 KSP2_ShadowFragment(KSP2DepthVaryings input) : SV_Target
{
    KSP2_DepthClip(input);
    return 0;
}

KSP2DepthVaryings KSP2_DepthVertex(KSP2DepthAttributes input)
{
    KSP2DepthVaryings output = (KSP2DepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv0;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.color = input.color;
    return output;
}

half4 KSP2_DepthFragment(KSP2DepthVaryings input) : SV_Target
{
    KSP2_DepthClip(input);
    return input.positionCS.z;
}

half4 KSP2_DepthNormalsFragment(KSP2DepthVaryings input) : SV_Target
{
    KSP2_DepthClip(input);
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

// Object motion vectors for URP's MotionVectors pass, which draws only the renderers that moved this frame (the
// camera motion covers the rest). The same clip as the depth passes. The current position goes through the
// non-jittered view projection, the previous one through the renderer's previous model matrix and the previous view
// projection, as URP's ObjectMotionVectors.hlsl does: rigid and skinned motion, no vertex animation.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"

struct KSP2MotionVectorAttributes
{
    float4 positionOS  : POSITION;
    float2 uv0         : TEXCOORD0;
    float4 color       : COLOR;
    float3 positionOld : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct KSP2MotionVectorVaryings
{
    float4 positionCS                 : SV_POSITION;
    float4 positionCSNoJitter         : TEXCOORD0;
    float4 previousPositionCSNoJitter : TEXCOORD1;
    float2 uv                         : TEXCOORD2;
    float4 color                      : TEXCOORD3;
    UNITY_VERTEX_OUTPUT_STEREO
};

KSP2MotionVectorVaryings KSP2_MotionVectorVertex(KSP2MotionVectorAttributes input)
{
    KSP2MotionVectorVaryings output = (KSP2MotionVectorVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float4 positionOS = float4(input.positionOS.xyz, 1.0);
    output.positionCS = TransformObjectToHClip(positionOS.xyz);
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, positionOS));
    // Skinned renderers carry last frame's skinned position in TEXCOORD4.
    float4 previousPositionOS = unity_MotionVectorsParams.x == 1 ? float4(input.positionOld, 1.0) : positionOS;
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, previousPositionOS));
    output.uv = input.uv0;
    output.color = input.color;
    return output;
}

float4 KSP2_MotionVectorFragment(KSP2MotionVectorVaryings input) : SV_Target
{
#if defined(KSP2_DEPTH_ALPHA_CLIP)
    clip(KSP2_DepthAlpha(input.uv, input.color));
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter, input.previousPositionCSNoJitter), 0.0, 0.0);
}

#endif // KSP2_URP_DEPTH_PASSES_INCLUDED