// KSP2CelestialBodyLocalCommon.hlsl
//
// Shared code for every pass of KSP2/Environment/CelestialBody/CelestialBody_Local: the PQS
// quad-mesh vertex stream, the values PQSRenderer sets on the material without a property, the
// texture declarations, the per-triangle flag tests and the normal helpers shared by the forward
// pass and the local-space prepass. Math from the in-repo Redux port
// (Packages/ksp2redux.sdk/Assets/Shaders/Redux/Environment/CelestialBody_Local), checked against
// ksp2-shaders/CelestialBody_Local and the raw decompile.
//
// Included from the SubShader HLSLINCLUDE after the UnityPerMaterial block.

#ifndef KSP2_CELESTIALBODY_LOCAL_COMMON_INCLUDED
#define KSP2_CELESTIALBODY_LOCAL_COMMON_INCLUDED

#include "../Include/KSP2DetailSampling.hlsl"

// ---------------------------------------------------------------------------
// Quad-mesh vertex stream. The terrain has no vertex buffer: PQSRenderer draws it with
// DrawProceduralIndirect and every pass fetches its vertex through SV_VertexID. All four buffers
// are structured buffers on the C# side (ComputeBufferType.Default / GraphicsBuffer.Target.Structured)
// and are bound on the material by name.
// ---------------------------------------------------------------------------
struct QuadMeshData
{
    float3 position;    // world space
    float3 normal;      // PQS (object) space geometric normal
    float2 uv;          // planet UV (scaled-space maps, biome mask)
    float4 tangent;     // .xyz PQS space tangent, .w bitangent sign
    float4 height;      // .w terrain height
};

StructuredBuffer<QuadMeshData> QuadMeshDataBuffer;
StructuredBuffer<uint> VisibleQuadMeshIndices;          // per vertex: quad mesh vertex index
StructuredBuffer<uint> VisibleQuadMeshIndicesMask;      // per triangle: bucketing flags
StructuredBuffer<uint> BucketedQuadInfoDecalIndices;    // per triangle: four 8-bit decal slots, 0xFF unused

// Per-triangle flag bits written by TerrainTriangleCull.compute (BucketTriangles).
#define CBL_BIOME_BITS          0xFu            // bit 0..3: biome R, G, B, A clears its cutoff
#define CBL_ZONE_INF_DECALS     (1u << 27)      // more than four decals
#define CBL_ZONE_4_DECALS       (1u << 28)      // one to four decals
#define CBL_ZONE_NO_DECALS      (1u << 29)      // no decal
#define CBL_ZONE_MASK           0x38000000u
#define CBL_BUCKET_NON_FLAT     (1u << 30)      // 3-axis triplanar
#define CBL_BUCKET_FLAT         (1u << 31)      // single-axis (Y plane) triplanar
#define CBL_BUCKET_MASK         0xC0000000u

// ---------------------------------------------------------------------------
// Values PQS / PQSRenderer set on the material with Set* calls that have no ShaderLab property.
// The integer-looking values go through Material.SetInt, which stores a float, so they are read
// as floats and converted.
// ---------------------------------------------------------------------------
float4x4 _PQSToWorld;                   // PQS.UpdateSurfaceMaterial: transform.localToWorldMatrix
float4x4 _PQSToLocal;                   // PQS.UpdateSurfaceMaterial: transform.worldToLocalMatrix
float4x4 TriplanarBasis;                // PQSRenderer.RenderPrepass
float4x4 TriplanarToWorld;              // PQSRenderer.RenderPrepass
float3   TriplanarContrastBoost;        // PQSRenderer.RenderPrepass (Vector4)
float    _DebugBiomeMask;               // SetInt, biome channel mask (15 = all)
float    _DebugTriplanarBucketing;      // SetInt, 0xC0000000 / 0x80000000 / 0x40000000 as int
float    SphereRadius;                  // SetFloat, body radius
float    decalInstanceCount;            // SetInt

uint CBL_DebugBiomeMask()
{
    return (uint)(int)_DebugBiomeMask;
}

uint CBL_DebugTriplanarBucketing()
{
    return (uint)(int)_DebugTriplanarBucketing;
}

// ---------------------------------------------------------------------------
// Textures. Shared repeat sampler as in the Redux port (URP GlobalSamplers.hlsl), clamp for the
// screen-space prepass targets.
// ---------------------------------------------------------------------------
TEXTURE2D(_AlbedoScaledTex);
TEXTURE2D(_NormalScaledTex);
TEXTURE2D(_PackedScaledTex);
TEXTURE2D(_EmissionScaledTex);
TEXTURE2D(_BiomeMaskTex);
TEXTURE2D(_SubzoneMaskTex);
TEXTURE2D(_GlobalGradienceTex);
TEXTURE2D(_LargeNormalR);       TEXTURE2D(_LargeNormalG);       TEXTURE2D(_LargeNormalB);       TEXTURE2D(_LargeNormalA);
TEXTURE2D(_MidNormalR);         TEXTURE2D(_MidNormalG);         TEXTURE2D(_MidNormalB);         TEXTURE2D(_MidNormalA);
TEXTURE2D(_LargeGradienceR);    TEXTURE2D(_LargeGradienceG);    TEXTURE2D(_LargeGradienceB);    TEXTURE2D(_LargeGradienceA);
TEXTURE2D(_MidGradienceR);      TEXTURE2D(_MidGradienceG);      TEXTURE2D(_MidGradienceB);      TEXTURE2D(_MidGradienceA);
TEXTURE2D(_Subzone3GradienceR); TEXTURE2D(_Subzone3GradienceG); TEXTURE2D(_Subzone3GradienceB); TEXTURE2D(_Subzone3GradienceA);
TEXTURE2D(_Subzone4GradienceR); TEXTURE2D(_Subzone4GradienceG); TEXTURE2D(_Subzone4GradienceB); TEXTURE2D(_Subzone4GradienceA);
TEXTURE2D_ARRAY(_SubZonesNormalTextureArray);
TEXTURE2D_ARRAY(_SmallAlbedoArray);
TEXTURE2D_ARRAY(_SmallNormalArray);
TEXTURE2D_ARRAY(_SmallMetalArray);
TEXTURE2D_ARRAY(_DecalAlbedo);
TEXTURE2D_ARRAY(_DecalNormalSAO);
TEXTURE2D_ARRAY(_DecalAlphaMask);       // PQSRenderer.UpdateDecalInstances, no stock property
TEXTURE2D(_LocalSpacePrepassTex0);
TEXTURE2D(_LocalSpacePrepassTex1);
TEXTURE2D(_LocalSpacePrepassTex2);
TEXTURE2D(_LocalSpacePrepassTex3);
TEXTURE2D(_LocalSpacePrepassTex4);

// ---------------------------------------------------------------------------
// Decal instances. HLSL mirror of Assets/Code/Root/DecalInstance.cs (49 floats, 196 bytes),
// uploaded by PQSRenderer.UpdateDecalInstances as "decalInstances".
// ---------------------------------------------------------------------------
struct DecalInstance
{
    float4x4 worldToLocal;
    float3   position;              int   index;
    float    clipPos;               float cullDist;
    float    heightScale;           float heightOffset;
    int      blendParams;           float fadeStrength;
    float    verticalOffset;
    int      useAlphaMask;          int   useDecalTexturing;
    float    AlbedoOpacity;         float NormalOpacity;
    int      NormalBlend;           float GradientOpacity;
    float4   TintColor;
    int      UseTextureAlphaMask;   int   UseTextureHeightmapFade;
    float3   DecalUp;
    float3   DecalForward;
    float3   DecalRight;
    float    GradientScale;
};

StructuredBuffer<DecalInstance> decalInstances;

// Decal footprint UV (x, z rows of worldToLocal), in [-0.5, 0.5] inside the box. A float4x4 in a
// structured buffer is column_major, the layout Unity uploads a Matrix4x4 in, so worldToLocal[r] is
// row r, as CullDecal and PQSQuadConstructor kernel 10 read it.
float2 CBL_DecalLocal(DecalInstance decal, float4 localPos)
{
    return float2(dot(decal.worldToLocal[0], localPos), dot(decal.worldToLocal[2], localPos));
}

// Screen derivatives of the decal UV, from the screen derivatives of the PQS-space position, so the
// decal textures can be sampled with gradients inside per-decal branches.
void CBL_DecalUVGradients(DecalInstance decal, float3 localPosDdx, float3 localPosDdy, out float2 uvDdx, out float2 uvDdy)
{
    uvDdx = CBL_DecalLocal(decal, float4(localPosDdx, 0.0));
    uvDdy = CBL_DecalLocal(decal, float4(localPosDdy, 0.0));
}

// Distance ramp on _DecalFadeParams (start and end distances).
float CBL_DecalFade(float cameraDist)
{
    return lerp(_DecalFadeParams.z, _DecalFadeParams.w,
                saturate((cameraDist - _DecalFadeParams.x) / max(_DecalFadeParams.y - _DecalFadeParams.x, 0.001)));
}

// Decal edge falloff: the sign bit of blendParams picks a box (Chebyshev) or round footprint.
float CBL_DecalEdgeFade(DecalInstance decal, float2 decalUV)
{
    float2 centered = decalUV - 0.5;
    float edge = (asfloat(decal.blendParams) < 0.0)
        ? 1.0 - 2.0 * max(abs(centered.x), abs(centered.y))
        : max(1.0 - 2.0 * length(centered), 0.0);
    return pow(max(edge, 0.0), decal.fadeStrength);
}

// ---------------------------------------------------------------------------
// Vertex fetch and frame
// ---------------------------------------------------------------------------
QuadMeshData CBL_FetchVisibleVertex(uint vertexID)
{
    return QuadMeshDataBuffer[VisibleQuadMeshIndices[vertexID]];
}

uint CBL_TriangleFlags(uint vertexID)
{
    return VisibleQuadMeshIndicesMask[vertexID / 3u];
}

CBLFrame CBL_BuildFrame(QuadMeshData q)
{
    CBLFrame f;
    f.tangentWS = normalize(mul((float3x3)_PQSToWorld, q.tangent.xyz));
    f.normalWS = normalize(mul((float3x3)_PQSToWorld, q.normal));
    f.bitangentWS = (q.tangent.w * unity_WorldTransformParams.w) * cross(f.normalWS, f.tangentWS);
    return f;
}

// Screen position as BRP's ComputeScreenPos built it (xy / w is the UV). The PQS prepass targets are
// sampled with it: following _ProjectionParams.x, it addresses a render texture drawn with the
// flipped projection the same way whether this pass draws into a texture or the back buffer.
float3 CBL_PrepassScreenPos(float4 positionCS)
{
    float4 o = positionCS * 0.5;
    return float3(float2(o.x, o.y * _ProjectionParams.x) + o.w, positionCS.w);
}

// SV_ClipDistance0 of the base passes and the prepass: the triangle must be in the zone, in an
// enabled triplanar bucket and touch an enabled biome channel.
float CBL_ClipDistance(uint flags, uint zoneMask)
{
    float clipDist = 1.0;
    if ((flags & zoneMask) == 0u)
        clipDist = -1.0;
    if ((flags & CBL_DebugTriplanarBucketing() & CBL_BUCKET_MASK) == 0u)
        clipDist = -1.0;
    if ((flags & CBL_DebugBiomeMask()) == 0u)
        clipDist = -1.0;
    return clipDist;
}

// LOD transition: 4x4 Bayer ordered dither against _Transition.
void CBL_DitherAlphaTest(float2 pixelPos)
{
    static const float4x4 kDitherMatrix = float4x4(
         1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
         4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0);
    uint2 dc = uint2(pixelPos) & 3u;
    if ((1.0 - _Transition) < kDitherMatrix[dc.y][dc.x])
        discard;
}

#endif // KSP2_CELESTIALBODY_LOCAL_COMMON_INCLUDED