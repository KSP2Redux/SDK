#ifndef REDUX_PQS_OVERLAY_COMMON
#define REDUX_PQS_OVERLAY_COMMON

// Shared vertex stage, varyings, and helper math for every Redux PQS overlay shader.
// The QuadMeshData stream is supplied by PQSRenderer.DrawPQSOverlays via SetBuffer
// on the overlay material (buffer names "QuadMeshDataBuffer" and "VisibleQuadMeshIndices").
//
// The quad-mesh layout mirrors the include used by the existing CelestialBody_Local_Overlay
// shader in DebugTools. The version here is inlined so each overlay shader only needs to
// include this one file; copying the buffer declarations rather than depending on a
// sibling-module path keeps the include graph local to this folder.

// NOTE: each overlay shader declares `#pragma multi_compile_local _USE_PQS_BUFFER`
// in its own HLSLPROGRAM. Including the pragma here is unreliable across the
// Unity shader compiler when this file is pulled in via #include.

#include "UnityCG.cginc"

struct QuadMeshData
{
    float3 position;
    float3 normal;
    float2 uv;
    float4 tangent;
    float4 height;
};

struct appdata
{
    uint vertexID : SV_VertexID;
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float4 normal : NORMAL0;
    float4 tangent : TANGENT0;
};

#if _USE_PQS_BUFFER
Buffer<uint> VisibleQuadMeshIndices;
StructuredBuffer<QuadMeshData> QuadMeshDataBuffer;
#endif

QuadMeshData GetQuadMeshVert(appdata v)
{
    QuadMeshData data;
    #if _USE_PQS_BUFFER
    uint meshIndex = VisibleQuadMeshIndices[v.vertexID];
    data = QuadMeshDataBuffer[meshIndex];
    #else
    data.position = v.vertex;
    data.normal   = v.normal;
    data.uv       = v.uv;
    data.tangent  = v.tangent;
    data.height   = 0;
    #endif
    return data;
}

struct overlay_v2f
{
    float4 vertex   : SV_POSITION;
    float2 uv       : TEXCOORD0;
    // .xyz = object-space position (PQS local); .w = scalar height from quad stream
    float4 posH     : TEXCOORD1;
    // .xyz = geometric normal in object space (from QuadMeshData.tangent.xyz);
    // .w   = bitangent sign (unused by overlays, kept for parity).
    float4 geomN    : TEXCOORD2;
};

overlay_v2f OverlayVert(appdata v)
{
    QuadMeshData q = GetQuadMeshVert(v);

    overlay_v2f o;
    o.vertex = UnityObjectToClipPos(q.position);
    o.uv     = q.uv;
    o.posH   = float4(q.position, q.height.w);
    o.geomN  = q.tangent;
    return o;
}

// Returns slope as 0..1 where 0 = flat (normal aligned with radial) and 1 = vertical wall.
float OverlaySlope(float3 objectSpacePos, float3 objectSpaceGeomNormal)
{
    float3 radial = normalize(objectSpacePos);
    return saturate(1.0 - abs(dot(normalize(objectSpaceGeomNormal), radial)));
}

// Returns radial distance from planet origin in object-space units. Useful as the
// raw height signal for altitude-band visualization.
float OverlayRadial(float3 objectSpacePos)
{
    return length(objectSpacePos);
}

// Standard transparent-blend tail. SrcAlpha/OneMinusSrcAlpha is set in the shader pass.
float4 OverlayCompose(float3 rgb, float strength)
{
    return float4(rgb, saturate(strength));
}

#endif
