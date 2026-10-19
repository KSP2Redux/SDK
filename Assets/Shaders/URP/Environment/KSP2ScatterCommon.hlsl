// KSP2ScatterCommon.hlsl
//
// Vegetation Studio procedural instancing and small helpers shared by the ported scatter and
// impostor shaders in this folder.
//
// VegetationSystemPro draws every scatter LOD with Graphics.DrawMeshInstancedIndirect and binds the
// camera's culled instance list to the MaterialPropertyBlock as both VisibleShaderDataBuffer and
// IndirectShaderDataBuffer (one GraphicsBuffer). The custom billboard (impostor) draw binds
// VisibleShaderDataBuffer only, so that is the name read here. Each element is
// CameraComputeBuffers.ScatterRenderInstanceData, appended by the GPUFrustumCulling compute shader:
//   float4x4 PositionMatrix   bytes  0-63  object to world, last row (0, 0, 0, 1)
//   float4   LODFadeInfo      bytes 64-79  x = dither cross-fade factor, 0 to 2, 1 and above shown
// The stock programs read element (SV_InstanceID + unity_BaseInstanceID) in the vertex stage and
// again in the fragment stage for the cross-fade. UNITY_SETUP_INSTANCE_ID does the same and calls
// setup() below in every pass that declares
//   #pragma multi_compile_instancing
//   #pragma instancing_options procedural:setup
// Include per pass after URP Core.hlsl, and after KSP2Lighting.hlsl in lit passes.

#ifndef KSP2_SCATTER_COMMON_INCLUDED
#define KSP2_SCATTER_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
struct ScatterRenderInstanceData
{
    float4x4 PositionMatrix;
    float4 LODFadeInfo;
};

StructuredBuffer<ScatterRenderInstanceData> VisibleShaderDataBuffer;
#endif

// Shadow casting light, set by URP's shadow caster pass.
float3 _LightDirection;
float3 _LightPosition;

// Inverse of a rotation, scale and translation matrix. The stock programs invert the whole matrix by
// cofactors, which gives the same result for this layout.
float4x4 KSP2Scatter_InverseAffine(float4x4 m)
{
    float3 row0 = m._m00_m01_m02;
    float3 row1 = m._m10_m11_m12;
    float3 row2 = m._m20_m21_m22;
    float3x3 inverse = transpose(float3x3(cross(row1, row2), cross(row2, row0), cross(row0, row1))) / dot(row0, cross(row1, row2));
    float3 translation = -mul(inverse, m._m03_m13_m23);
    return float4x4(
        float4(inverse[0], translation.x),
        float4(inverse[1], translation.y),
        float4(inverse[2], translation.z),
        float4(0.0, 0.0, 0.0, 1.0));
}

// Procedural instancing entry point named by instancing_options in each pass.
void setup()
{
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    ScatterRenderInstanceData instance = VisibleShaderDataBuffer[unity_InstanceID];
    unity_ObjectToWorld = instance.PositionMatrix;
    unity_WorldToObject = KSP2Scatter_InverseAffine(instance.PositionMatrix);
    unity_LODFade = instance.LODFadeInfo;
#endif
}

// Stock dithered LOD cross-fade: discard where fade - sign(fade) * dither < 0, with sign(0) = -1
// (UnityApplyDitherCrossFade). URP's dithering texture stands in for unity_DitherMask.
void KSP2Scatter_LODCrossFade(float4 positionCS)
{
#if defined(LOD_FADE_CROSSFADE)
    float dither = SAMPLE_TEXTURE2D(_DitheringTexture, sampler_PointRepeat, positionCS.xy * _DitheringTextureInvSize).a;
    float fade = unity_LODFade.x;
    clip(fade - (fade > 0.0 ? dither : -dither));
#endif
}

#if defined(KSP2_URP_LIGHTING_INCLUDED)
// Unity's specular workflow (StandardSpecular) as a KSP2Surface: specular colour is F0 and the
// diffuse albedo loses max(specular). KSP2_IndirectLighting takes 1 - reflectivity as
// 0.96 - 0.96 * metallic, so metallic is set to give 1 - max(specular) there.
KSP2Surface KSP2Scatter_MakeSpecularSurface(float3 albedo, float3 specular, float smoothness, float occlusion, float3 emission)
{
    float maxSpecular = max(max(specular.r, specular.g), specular.b);
    KSP2Surface s;
    s.albedo = albedo;
    s.diffuseAlbedo = albedo * (1.0 - maxSpecular);
    s.F0 = specular;
    s.metallic = (maxSpecular - 0.04) / 0.96;
    s.smoothness = smoothness;
    s.occlusion = occlusion;
    s.emission = emission;
    s.alpha = 1.0;
    return s;
}
#endif

#endif // KSP2_SCATTER_COMMON_INCLUDED