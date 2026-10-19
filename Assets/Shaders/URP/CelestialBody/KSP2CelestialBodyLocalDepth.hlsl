// KSP2CelestialBodyLocalDepth.hlsl
//
// Geometry-only passes of CelestialBody_Local, all fed from the quad-mesh buffers through
// SV_VertexID:
//   ShadowCaster / DepthOnly / DepthNormals  URP passes. They clip the same triangles as the
//       forward pass and apply the _Transition dither.
//   CBL_PQSDepth*      the custom PQS depth prepass (stock pass 12), drawn by
//       PQSRenderer.DrawEarlyPasses into PQSDepthBuffer, with the frame's jittered projection.
//   CBL_DecalMask*     the deferred decal mask (stock pass 16), drawn by
//       PQSRenderer.DrawEarlyPasses: constant red on every terrain pixel.
// The stock pass 12 and 16 programs had no clip distance and no dither, and neither do these.

#ifndef KSP2_CELESTIALBODY_LOCAL_DEPTH_INCLUDED
#define KSP2_CELESTIALBODY_LOCAL_DEPTH_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// Shadow casting light, set by URP's shadow caster pass.
float3 _LightDirection;
float3 _LightPosition;

struct CBLDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float  clipDist   : SV_ClipDistance0;
    float3 normalWS   : TEXCOORD0;
    float3 screenPos  : TEXCOORD1;  // prepass target UV = xy / z
};

CBLDepthVaryings CBL_ShadowVertex(uint vertexID : SV_VertexID)
{
    QuadMeshData q = CBL_FetchVisibleVertex(vertexID);
    float3 normalWS = normalize(mul((float3x3)_PQSToWorld, q.normal));
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - q.position);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    CBLDepthVaryings o;
    o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(q.position, normalWS, lightDirectionWS)));
    o.clipDist = CBL_ClipDistance(CBL_TriangleFlags(vertexID), CBL_ZONE_MASK);
    o.normalWS = normalWS;
    o.screenPos = 0.0;
    return o;
}

CBLDepthVaryings CBL_DepthVertex(uint vertexID : SV_VertexID)
{
    QuadMeshData q = CBL_FetchVisibleVertex(vertexID);
    CBLDepthVaryings o;
    o.positionCS = TransformWorldToHClip(q.position);
    o.clipDist = CBL_ClipDistance(CBL_TriangleFlags(vertexID), CBL_ZONE_MASK);
    o.normalWS = normalize(mul((float3x3)_PQSToWorld, q.normal));
    o.screenPos = CBL_PrepassScreenPos(o.positionCS);
    return o;
}

half4 CBL_ShadowFragment(CBLDepthVaryings i) : SV_Target
{
    CBL_DitherAlphaTest(i.positionCS.xy);
    return 0;
}

half4 CBL_DepthFragment(CBLDepthVaryings i) : SV_Target
{
    CBL_DitherAlphaTest(i.positionCS.xy);
    return i.positionCS.z;
}

// The forward pass shades with the prepass normal (direction of _LocalSpacePrepassTex4), so the
// depth-normals texture uses it too, falling back to the geometric normal where it is empty.
half4 CBL_DepthNormalsFragment(CBLDepthVaryings i) : SV_Target
{
    CBL_DitherAlphaTest(i.positionCS.xy);
    float3 prepassNormal = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex4, sampler_LinearClamp, i.screenPos.xy / i.screenPos.z, 0).xyz;
    float3 normalWS = dot(prepassNormal, prepassNormal) > 1e-4 ? prepassNormal : i.normalWS;
    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
}

// ---------------------------------------------------------------------------
// Custom PQS depth prepass and deferred decal mask
// ---------------------------------------------------------------------------
float4 CBL_PositionOnlyVertex(uint vertexID : SV_VertexID) : SV_POSITION
{
    return TransformWorldToHClip(CBL_FetchVisibleVertex(vertexID).position);
}

float4 CBL_PQSDepthFragment(float4 positionCS : SV_POSITION) : SV_Target0
{
    return float4(0.0, 0.0, 0.0, 0.0);
}

float4 CBL_DecalMaskFragment(float4 positionCS : SV_POSITION) : SV_Target0
{
    return float4(1.0, 0.0, 0.0, 1.0);
}

#endif // KSP2_CELESTIALBODY_LOCAL_DEPTH_INCLUDED