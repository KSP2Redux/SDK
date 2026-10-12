// ============================================================================
// ScatterImpostorCapture.hlsl
//
// ImpostorCapture pass of the scatter stand-ins, drawn only by
// ScatterImpostorBaker. It writes the surface the scatter port lights into four
// targets laid out like the Built-in pipeline G-buffer the baker packs:
//   0 diffuse albedo, occlusion
//   1 specular colour, smoothness
//   2 world normal * 0.5 + 0.5, 1 where drawn
//   3 emission
// The surface inputs are the URP port's (KSP2ScatterOpaque.hlsl in Ksp2Redux
// Assets/ReduxAssets/Shaders/URP/Environment), ungraded like the stock deferred
// pass the baker used to capture: Vegetation Studio grades the billboard
// material at runtime. Keep the surface lines in step with
// KSP2Scatter_ForwardFragment.
//
// The including shader declares the UnityPerMaterial block, and defines
// KSP2_SCATTER_SPECULAR for the specular workflow.
// ============================================================================

#ifndef REDUX_SCATTER_IMPOSTOR_CAPTURE_INCLUDED
#define REDUX_SCATTER_IMPOSTOR_CAPTURE_INCLUDED

#include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Lighting.hlsl"
#include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"

struct ScatterImpostorCaptureOutput
{
    float4 gBuffer0 : SV_Target0;
    float4 gBuffer1 : SV_Target1;
    float4 gBuffer2 : SV_Target2;
    float4 gBuffer3 : SV_Target3;
};

ScatterImpostorCaptureOutput ScatterImpostorCapture_Fragment(KSP2ScatterVaryings input)
{
    float2 mainUV = input.uv.xy;
    float2 detailUV = input.uv.zw;
    float detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, detailUV).a;

    float3 baseNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, mainUV), _BumpScale);
    float3 detailNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, input.uvDetailNormal), _DetailNormalScale);
    float3 blendedNormal = normalize(float3(baseNormal.xy + detailNormal.xy, baseNormal.z * detailNormal.z));
    float3 tangentNormal = baseNormal + detailMask * (blendedNormal - baseNormal);

    float3 geomNormal = input.normalWS;
    float3 tangent = input.tangentWS.xyz;
    float3 bitangent = cross(geomNormal, tangent) * input.tangentWS.w;
    float3 normalWS = normalize(tangentNormal.x * tangent + tangentNormal.y * bitangent + tangentNormal.z * geomNormal);

    float3 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mainUV).rgb;
    float3 detailAlbedo = SAMPLE_TEXTURE2D(_DetailAlbedoMap, sampler_DetailAlbedoMap, detailUV).rgb;
    float3 albedo = KSP2Scatter_Albedo(mainTex, detailAlbedo, detailMask) * _LODDebugColor.rgb;
    float occlusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, mainUV).g * _AOScale + (1.0 - _AOScale);

#if defined(KSP2_SCATTER_SPECULAR)
    float4 specularGloss = SAMPLE_TEXTURE2D(_SpecularSmoothnessMap, sampler_SpecularSmoothnessMap, mainUV);
    KSP2Surface surface = KSP2Scatter_MakeSpecularSurface(albedo, specularGloss.rgb * _SpecularScale, specularGloss.a * _SmoothnessScale, occlusion, 0.0);
#else
    float4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicSmoothnessMap, sampler_MetallicSmoothnessMap, mainUV);
    KSP2Surface surface = KSP2_MakeMetallicSurface(albedo, saturate(metallicGloss.g * _MetallicScale), saturate(metallicGloss.a * _SmoothnessScale), occlusion, 0.0, 1.0);
#endif

    ScatterImpostorCaptureOutput output;
    output.gBuffer0 = float4(surface.diffuseAlbedo, surface.occlusion);
    output.gBuffer1 = float4(surface.F0, surface.smoothness);
    output.gBuffer2 = float4(normalWS * 0.5 + 0.5, 1.0);
    output.gBuffer3 = float4(surface.emission, 1.0);
    return output;
}

#endif // REDUX_SCATTER_IMPOSTOR_CAPTURE_INCLUDED
