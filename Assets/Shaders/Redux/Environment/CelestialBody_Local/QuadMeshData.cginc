#ifndef QUADMESHDATA_CGINC
#define QUADMESHDATA_CGINC

// Note the swap between `normal` and `tangent` field meanings -- see WorldTBN.cginc.
struct QuadMeshData
{
    float3 position;  // world-space vertex position (already in PQS local)
    float3 normal;    // tangent direction in object space
    float2 uv;        // surface texcoord
    float4 tangent;   // .xyz = geometric normal (object space), .w = bitangent sign
    float4 height;    // .w = scalar height (used by height blending later)
};

// ----------------------------------------------------------------------------
// Quad-mesh streaming vertex system.  PQSRenderer binds these buffers by name
// (Shader.PropertyToID), not by register slot.
// ----------------------------------------------------------------------------
StructuredBuffer<QuadMeshData> QuadMeshDataBuffer;
Buffer<uint> VisibleQuadMeshIndices;

#endif
