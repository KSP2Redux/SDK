// KSP2ScatterOpaque.hlsl
//
// Passes shared by the two opaque scatter shaders:
//   KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque           metallic workflow
//   KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular  KSP2_SCATTER_SPECULAR
// Material math from ksp2-shaders/KSP2_Environment_Scatter_Scatter_Instanced_Indirect_Opaque[_Specular]/final
// (the DEFERRED pass is the surface the stock game drew; the forward passes match it).
// The including shader declares the UnityPerMaterial block and defines KSP2_SCATTER_FOG where the
// stock forward pass applied fog. Include KSP2Lighting.hlsl first in the forward pass.

#ifndef KSP2_SCATTER_OPAQUE_INCLUDED
#define KSP2_SCATTER_OPAQUE_INCLUDED

#include "KSP2ScatterCommon.hlsl"

// ---------------------------------------------------------------------------
// Forward pass
// ---------------------------------------------------------------------------
#if defined(KSP2_URP_LIGHTING_INCLUDED)

#if defined(KSP2_GBUFFER_PASS)
    #include "../Include/KSP2GBuffer.hlsl"
#endif

TEXTURE2D(_MainTex);                    SAMPLER(sampler_MainTex);
#if defined(KSP2_SCATTER_SPECULAR)
TEXTURE2D(_SpecularSmoothnessMap);      SAMPLER(sampler_SpecularSmoothnessMap);
#else
TEXTURE2D(_MetallicSmoothnessMap);      SAMPLER(sampler_MetallicSmoothnessMap);
#endif
TEXTURE2D(_BumpMap);                    SAMPLER(sampler_BumpMap);
TEXTURE2D(_OcclusionMap);               SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_DetailMask);                 SAMPLER(sampler_DetailMask);
TEXTURE2D(_DetailAlbedoMap);            SAMPLER(sampler_DetailAlbedoMap);
TEXTURE2D(_DetailNormalMap);            SAMPLER(sampler_DetailNormalMap);

// Forward-only terrain blend of the stock specular scatter under SCATTER_SYSTEM: close to the
// terrain behind it the lit colour fades to 1.4x the terrain colour. Off: the stock game drew the
// scatter through its deferred pass, which has no blend, and _PQSTargetBufferTexture comes from a
// CameraEvent.AfterReflections command buffer (CreateTerrainBlendBuffers) that URP never runs.
#ifndef KSP2_SCATTER_PQS_TERRAIN_BLEND
    #define KSP2_SCATTER_PQS_TERRAIN_BLEND 0
#endif

#if KSP2_SCATTER_PQS_TERRAIN_BLEND && defined(KSP2_SCATTER_SPECULAR) && defined(SCATTER_SYSTEM)
TEXTURE2D(_PQSTargetBufferTexture);     SAMPLER(sampler_PQSTargetBufferTexture);
TEXTURE2D(_PQSDepthTexture);            SAMPLER(sampler_PQSDepthTexture);

float3 KSP2Scatter_TerrainBlend(float3 color, float4 positionCS)
{
    float2 screenUV = GetNormalizedScreenSpaceUV(positionCS);
    float terrainEye = LinearEyeDepth(SAMPLE_TEXTURE2D(_PQSDepthTexture, sampler_PQSDepthTexture, screenUV).r, _ZBufferParams);
    float fragmentEye = LinearEyeDepth(positionCS.z, _ZBufferParams);
    float distanceFade = 1.0 - saturate((terrainEye - fragmentEye) * 5.0 / (10.0 * _BlendDistance));
    float blend = saturate(distanceFade * _BlendStrength * 3.0);
    float3 terrain = SAMPLE_TEXTURE2D(_PQSTargetBufferTexture, sampler_PQSTargetBufferTexture, screenUV).rgb;
    return lerp(color, terrain * 1.4, blend);
}
#endif

struct KSP2ScatterAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv0        : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct KSP2ScatterVaryings
{
    float4 positionCS     : SV_POSITION;
    float4 uv             : TEXCOORD0; // xy main (_texcoord_ST), zw detail albedo and mask (_DetailAlbedoMap_ST)
    float2 uvDetailNormal : TEXCOORD1; // _DetailNormalMap_ST
    float3 positionWS     : TEXCOORD2;
    float3 normalWS       : TEXCOORD3;
    float4 tangentWS      : TEXCOORD4; // w = bitangent sign
#if defined(KSP2_SCATTER_FOG)
    float  fogFactor      : TEXCOORD5;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

// Albedo with the masked detail albedo (unity_ColorSpaceDouble), then the scatter system's
// brightness, saturation and contrast grade.
float3 KSP2Scatter_Albedo(float3 mainTex, float3 detailAlbedo, float detailMask)
{
    float3 base = (detailMask * detailAlbedo * 4.594794 + (1.0 - detailMask)) * mainTex;
#if defined(SCATTER_SYSTEM)
    float3 tinted = saturate(base * _Color.rgb * _Brightness);
    float luminance = dot(tinted, float3(0.2125, 0.7154, 0.0721));
    float3 saturated = _Saturation * (tinted - luminance) + luminance;
    return saturated * saturate(_Contrast * (saturated - 0.5) + 0.5);
#else
    return base;
#endif
}

KSP2ScatterVaryings KSP2Scatter_ForwardVertex(KSP2ScatterAttributes input)
{
    KSP2ScatterVaryings output = (KSP2ScatterVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
    output.uv = float4(input.uv0 * _texcoord_ST.xy + _texcoord_ST.zw, input.uv0 * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw);
    output.uvDetailNormal = input.uv0 * _DetailNormalMap_ST.xy + _DetailNormalMap_ST.zw;
#if defined(KSP2_SCATTER_FOG)
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
#endif
    return output;
}

KSP2Surface KSP2Scatter_Surface(KSP2ScatterVaryings input, out float3 normalWS, out float3 viewDirWS)
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);

    float2 mainUV = input.uv.xy;
    float2 detailUV = input.uv.zw;
    float detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, detailUV).a;

    // Base normal and detail normal (Unity BlendNormals), blended in by the detail mask.
    float3 baseNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, mainUV), _BumpScale);
    float3 detailNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.uvDetailNormal), _DetailNormalScale);
    float3 blendedNormal = normalize(float3(baseNormal.xy + detailNormal.xy, baseNormal.z * detailNormal.z));
    float3 tangentNormal = baseNormal + detailMask * (blendedNormal - baseNormal);

    float3 geomNormal = input.normalWS;
    float3 tangent = input.tangentWS.xyz;
    float3 bitangent = cross(geomNormal, tangent) * input.tangentWS.w;
    normalWS = normalize(tangentNormal.x * tangent + tangentNormal.y * bitangent + tangentNormal.z * geomNormal);

    float3 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV).rgb;
    float3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUV).rgb;
    float3 albedo = KSP2Scatter_Albedo(mainTex, detailAlbedo, detailMask) * _LODDebugColor.rgb;
    float occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, mainUV).g * _AOScale + (1.0 - _AOScale);

#if defined(KSP2_SCATTER_SPECULAR)
    float4 specularGloss = SAMPLE_TEXTURE2D(_SpecularSmoothnessMap, sampler_SpecularSmoothnessMap, mainUV);
    #if defined(SCATTER_SYSTEM)
        float3 specular = saturate(specularGloss.rgb * _SpecularScale + _Specular) * _SpecularTintColor.rgb;
        float smoothness = saturate(specularGloss.a * _SmoothnessScale + _Smoothness);
    #else
        float3 specular = specularGloss.rgb * _SpecularScale;
        float smoothness = specularGloss.a * _SmoothnessScale;
    #endif
    KSP2Surface surface = KSP2Scatter_MakeSpecularSurface(albedo, specular, smoothness, occlusion, 0.0);
#else
    // Metallic from the green channel. The G-buffer the stock game wrote clamped both to [0, 1].
    float4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicSmoothnessMap, sampler_MetallicSmoothnessMap, mainUV);
    #if defined(SCATTER_SYSTEM)
        float metallic = metallicGloss.g * _MetallicScale + _Metallic;
        float smoothness = metallicGloss.a * _SmoothnessScale + _Smoothness;
    #else
        float metallic = metallicGloss.g * _MetallicScale;
        float smoothness = metallicGloss.a * _SmoothnessScale;
    #endif
    KSP2Surface surface = KSP2_MakeMetallicSurface(albedo, saturate(metallic), saturate(smoothness), occlusion, 0.0, 1.0);
#endif

    viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    return surface;
}

float4 KSP2Scatter_ForwardFragment(KSP2ScatterVaryings input) : SV_Target
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = KSP2Scatter_Surface(input, normalWS, viewDirWS);
    float3 color = KSP2_Shade(input.positionWS, input.positionCS, normalWS, viewDirWS, surface);
#if KSP2_SCATTER_PQS_TERRAIN_BLEND && defined(KSP2_SCATTER_SPECULAR) && defined(SCATTER_SYSTEM)
    color = KSP2Scatter_TerrainBlend(color, input.positionCS);
#endif
#if defined(KSP2_SCATTER_FOG)
    color = MixFog(color, input.fogFactor);
#endif
    return float4(color, 1.0);
}

#if defined(KSP2_GBUFFER_PASS)
GBufferFragOutput KSP2Scatter_GBufferFragment(KSP2ScatterVaryings input)
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = KSP2Scatter_Surface(input, normalWS, viewDirWS);
    return KSP2_GBufferOutput(surface, input.positionWS, input.positionCS, normalWS, viewDirWS);
}
#endif

#endif // KSP2_URP_LIGHTING_INCLUDED

// ---------------------------------------------------------------------------
// ShadowCaster, DepthOnly and DepthNormals, with the same instance transform
// ---------------------------------------------------------------------------
struct KSP2ScatterDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct KSP2ScatterDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

KSP2ScatterDepthVaryings KSP2Scatter_ShadowVertex(KSP2ScatterDepthAttributes input)
{
    KSP2ScatterDepthVaryings output = (KSP2ScatterDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
    output.normalWS = normalWS;
    return output;
}

KSP2ScatterDepthVaryings KSP2Scatter_DepthVertex(KSP2ScatterDepthAttributes input)
{
    KSP2ScatterDepthVaryings output = (KSP2ScatterDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 KSP2Scatter_ShadowFragment(KSP2ScatterDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    return 0;
}

half4 KSP2Scatter_DepthFragment(KSP2ScatterDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    return input.positionCS.z;
}

half4 KSP2Scatter_DepthNormalsFragment(KSP2ScatterDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    KSP2Scatter_LODCrossFade(input.positionCS);
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

#endif // KSP2_SCATTER_OPAQUE_INCLUDED