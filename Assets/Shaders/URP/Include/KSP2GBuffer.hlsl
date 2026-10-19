// KSP2GBuffer.hlsl
//
// G-buffer output for the KSP2 lit shaders under the Deferred+ renderer. A shader's UniversalGBuffer pass builds
// its KSP2Surface exactly as its forward pass does and returns KSP2_GBufferOutput. The surface goes into URP's
// layout, so URP's own consumers (screen-space reflections, the rendering debugger) read it correctly:
//   gBuffer0  metallic setup: base albedo, specular setup: diffuse colour. Material flags in a.
//   gBuffer1  metallic setup: reflectivity (0.04 + 0.96 metallic) in r, specular setup: F0 in rgb. Occlusion in a.
//   gBuffer2  normal, smoothness
//   colour    the surface's indirect light plus emission
// URP decodes the metallic layout with the Standard shader's terms (dielectric F0 0.04, diffuse scaled by
// 0.96 - 0.96 metallic), the same as KSP2_MakeMetallicSurface, so the KSP2 deferred light pass
// (KSP2ClusterDeferred) sees exactly the surface the forward pass lit. Shaders that build their surface from a
// specular colour define KSP2_SPECULAR_SETUP before including this file. Their diffuse colour need not follow
// Standard Specular's albedo * (1 - max(F0)) (the PQS terrain accumulates it on its own, as stock's G-buffer
// did), so it is stored as is and kKSP2MaterialFlagDiffuseAsIs tells KSP2ClusterDeferred to read it unscaled.
// URP's own readers of the G-buffer ignore the flag and see that diffuse scaled by 1 - max(F0).
//
// Stock KSP2 rendered deferred and its G-buffer carried the same split: indirect light and emission in the
// lighting buffer, the sun and other lights added by the deferred light passes.

#ifndef KSP2_GBUFFER_INCLUDED
#define KSP2_GBUFFER_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "KSP2Lighting.hlsl"
#include "KSP2GBufferFlags.hlsl"

// For shaders whose indirect light is not KSP2_IndirectLighting (lightmaps, SH only, a fixed probe mip): the
// lighting target gets indirectLight, which must already include the surface's emission. Everything the forward
// pass adds besides KSP2_DirectLighting goes here, so G-buffer colour plus the deferred direct light equals the
// forward result. extraMaterialFlags takes URP flags the shader decides at run time, such as
// kMaterialFlagSpecularHighlightsOff (the light pass then drops the specular lobe, as KSP2_StandardDirectLight does
// for a zero F0).
GBufferFragOutput KSP2_GBufferOutputWithIndirect(KSP2Surface s, float4 positionCS, float3 N, float3 indirectLight,
    uint extraMaterialFlags)
{
    uint materialFlags = extraMaterialFlags;
#if defined(_RECEIVE_SHADOWS_OFF)
    materialFlags |= kMaterialFlagReceiveShadowsOff;
#endif
#if defined(KSP2_SPECULAR_SETUP)
    materialFlags |= kMaterialFlagSpecularSetup | kKSP2MaterialFlagDiffuseAsIs;
    half3 packedAlbedo = s.diffuseAlbedo;
    half3 packedSpecular = s.F0;
#else
    half3 packedAlbedo = s.albedo;
    half3 packedSpecular = half3(1.0 - OneMinusReflectivityMetallic(s.metallic), 0.0, 0.0);
#endif

    GBufferFragOutput output;
    output.gBuffer0 = half4(packedAlbedo, PackGBufferMaterialFlags(materialFlags));
    output.gBuffer1 = half4(packedSpecular, s.occlusion);
    output.gBuffer2 = half4(PackGBufferNormal(N), s.smoothness);
    output.color = half4(indirectLight, 1.0);
    // Debug view 7 (KSP2ClusterDeferred.hlsl): indirect light that is not finite, or too bright for the half-float
    // lighting target, in bright green.
    if (_KSP2DeferredDebug > 6.5 && (any(!isfinite(indirectLight)) || any(indirectLight > 60000.0)))
        output.color = half4(0.0, 50.0, 0.0, 1.0);

#if defined(GBUFFER_FEATURE_DEPTH)
    output.depth = positionCS.z;
#endif
#if defined(GBUFFER_FEATURE_SHADOWMASK)
    output.shadowMask = half4(1, 1, 1, 1);
#endif
#if defined(GBUFFER_FEATURE_RENDERING_LAYERS)
    output.meshRenderingLayers = EncodeMeshRenderingLayer();
#endif
    return output;
}

GBufferFragOutput KSP2_GBufferOutputWithIndirect(KSP2Surface s, float4 positionCS, float3 N, float3 indirectLight)
{
    return KSP2_GBufferOutputWithIndirect(s, positionCS, N, indirectLight, 0u);
}

// The usual case: KSP2 indirect light (KSP2_IndirectLighting) plus emission, as KSP2_Shade adds them. The
// environment specular is KSP2's, so the screen-space reflections may replace it.
GBufferFragOutput KSP2_GBufferOutput(KSP2Surface s, float3 positionWS, float4 positionCS, float3 N, float3 V)
{
    float3 ambientDiffuse, envSpecular;
    KSP2_IndirectLighting(positionWS, N, V, s, GetNormalizedScreenSpaceUV(positionCS), ambientDiffuse, envSpecular);
    return KSP2_GBufferOutputWithIndirect(s, positionCS, N, s.diffuseAlbedo * ambientDiffuse + envSpecular + s.emission,
        kKSP2MaterialFlagEnvironmentReflections);
}

#endif // KSP2_GBUFFER_INCLUDED