// KSP2SceneryStandardOpaqueForward.hlsl
//
// ForwardLit pass of KSP2/Scenery/Standard (Opaque). Material math from
// ksp2-shaders/KSP2_Scenery_Standard_Opaque/final. Also included by the SDK stand-in
// KSP2/Parts/Paintable (Packages/ksp2redux.sdk/Assets/Shaders/KSP2/Parts/Paintable.shader),
// so SDK-authored part materials look the same in the editor as on the swapped-in port.
//
// The including shader declares the UnityPerMaterial block and the pass keywords before including
// this file.

#ifndef KSP2_SCENERY_STANDARD_OPAQUE_FORWARD_INCLUDED
#define KSP2_SCENERY_STANDARD_OPAQUE_FORWARD_INCLUDED

#include "../Include/KSP2Lighting.hlsl"
#include "../Include/KSP2Reentry.hlsl"
#if defined(KSP2_GBUFFER_PASS)
    #include "../Include/KSP2GBuffer.hlsl"
#endif
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_DetailBumpMap);      SAMPLER(sampler_DetailBumpMap);
TEXTURE2D(_DetailMask);         SAMPLER(sampler_DetailMask);
TEXTURE2D(_PaintMaskGlossMap);  SAMPLER(sampler_PaintMaskGlossMap);
TEXTURE2D(_OcclusionMap);       SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

// Redux look for bare metal on parts (a deliberate change from stock, see URP/DECISIONS.md "reflections look"), set
// by PartMaterialLook.cs: x scales metallic, y scales smoothness where the surface is metal. All zero (unset, the SDK)
// leaves the material as authored. Parts are told apart by the rendering layers LegacyLightCullingMask gives the
// OAB.Parts, Local.Parts and Local.PartsActive GameObject layers.
float4 _KSP2PartMetalLook;
#define KSP2_PART_RENDERING_LAYERS ((1u << 9) | (1u << 19) | (1u << 20))

void KSP2StandardOpaque_PartMetalLook(inout float metallic, inout float smoothness)
{
    if (all(_KSP2PartMetalLook.xy == 0.0) || (GetMeshRenderingLayer() & KSP2_PART_RENDERING_LAYERS) == 0)
        return;
    float metal = saturate(metallic);
    metallic = saturate(metallic * _KSP2PartMetalLook.x);
    smoothness = lerp(smoothness, smoothness * _KSP2PartMetalLook.y, metal);
}

struct KSP2StandardOpaqueAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv0        : TEXCOORD0;
    float2 uv1        : TEXCOORD1;
};

struct KSP2StandardOpaqueVaryings
{
    float4 positionCS : SV_POSITION;
    float4 uv         : TEXCOORD0; // xy main, zw paint mask
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float4 tangentWS  : TEXCOORD3; // w = bitangent sign
    float  fogFactor  : TEXCOORD4;
};

KSP2StandardOpaqueVaryings KSP2StandardOpaque_ForwardVertex(KSP2StandardOpaqueAttributes input)
{
    KSP2StandardOpaqueVaryings output;
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
    output.uv = float4(input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw, input.uv1 * _PaintMaskGlossMap_ST.xy + _PaintMaskGlossMap_ST.zw);
    output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
    return output;
}

// The material: the same for the forward pass and the G-buffer pass.
KSP2Surface KSP2StandardOpaque_Surface(KSP2StandardOpaqueVaryings input, out float3 normalWS, out float3 viewDirWS)
{
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    float2 mainUV = input.uv.xy;
    float2 paintUV = input.uv.zw;

    float3 geomNormal = input.normalWS;
    float3 tangent = input.tangentWS.xyz;
    float3 bitangent = cross(geomNormal, tangent) * input.tangentWS.w;

    // Base normal blended with the masked detail normal (KSP2 RNM approximation).
    float3 baseNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, mainUV), 1.0);
    float3 detailNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_DetailBumpMap, sampler_DetailBumpMap, _DetailBumpTiling * mainUV), _DetailBumpScale);
    float3 combined = float3(detailNormal.xy + baseNormal.xy, baseNormal.z * detailNormal.z);
    float detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, mainUV).a;
    float3 tangentNormal = baseNormal + detailMask * (normalize(combined) - baseNormal);
    normalWS = normalize(tangentNormal.x * tangent + tangentNormal.y * bitangent + tangentNormal.z * geomNormal);

    viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

    // Two-layer paint over albedo, metallic map, paint-mask smoothness override.
    float4 paintMask = SAMPLE_TEXTURE2D(_PaintMaskGlossMap, sampler_PaintMaskGlossMap, paintUV);
    float4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV);
    float paintAW = _PaintA.a * paintMask.r;
    float paintBW = _PaintB.a * paintMask.g;
    float3 albedoB = lerp(_Color.rgb * albedoTex.rgb, _PaintB.rgb * paintMask.b, paintBW);
    float3 albedo = lerp(albedoB, _PaintA.rgb * paintMask.b, paintAW);
    float paintMax = max(paintAW, paintBW);

    float4 metalGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, mainUV);
    float metallic = _Metallic * metalGloss.g * (1.0 - paintMax);
    float glossFromMap = _GlossMapScale * metalGloss.a;
#if defined(_SMOOTHNESSOVERRIDE_ON)
    float smoothBase = paintMask.a;
#else
    float smoothBase = glossFromMap;
#endif
    float smoothness = lerp(glossFromMap, smoothBase * _PaintGlossMapScale, paintMax);

    float occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, mainUV).g * _OcclusionStrength + (1.0 - _OcclusionStrength);

    // Emission: rim + (time-of-day gated) emission map + reentry heat.
    float3 tangentView = float3(dot(tangent, viewDirWS), dot(bitangent, viewDirWS), dot(geomNormal, viewDirWS));
    float3 emission = KSP2_RimEmission(tangentView, baseNormal, _RimFalloff, _RimColor);
    float3 emissionSample = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, mainUV).rgb;
#if defined(USE_TIME_OF_DAY)
    emissionSample *= 1.0 - saturate((_TimeOfDayKSC - _TimeOfDayDotMin) / max(_TimeOfDayDotMax - _TimeOfDayDotMin, 0.001));
#endif
    emission += emissionSample * _EmissionColor.rgb;
#if defined(_REENTRYEMISSION_ON)
    emission += KSP2_ReentryEmission(mainUV, geomNormal, input.positionWS, metallic);
#endif

    // After the reentry glow, which keeps the authored metallic.
    KSP2StandardOpaque_PartMetalLook(metallic, smoothness);
    return KSP2_MakeMetallicSurface(albedo, metallic, smoothness * _MipBias, occlusion, emission, 1.0);
}

float4 KSP2StandardOpaque_ForwardFragment(KSP2StandardOpaqueVaryings input) : SV_Target
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = KSP2StandardOpaque_Surface(input, normalWS, viewDirWS);
    float3 color = KSP2_Shade(input.positionWS, input.positionCS, normalWS, viewDirWS, surface);
    color = MixFog(color, input.fogFactor);
    return float4(color, 1.0);
}

#if defined(KSP2_GBUFFER_PASS)
GBufferFragOutput KSP2StandardOpaque_GBufferFragment(KSP2StandardOpaqueVaryings input)
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = KSP2StandardOpaque_Surface(input, normalWS, viewDirWS);
    return KSP2_GBufferOutput(surface, input.positionWS, input.positionCS, normalWS, viewDirWS);
}
#endif

#endif // KSP2_SCENERY_STANDARD_OPAQUE_FORWARD_INCLUDED