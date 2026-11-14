#ifndef REDUX_FLARE_COMMON_INCLUDED
#define REDUX_FLARE_COMMON_INCLUDED

#include "FlareInputs.hlsl"

// Choose source visibility for optical ghosts, scene depth for luminous geometry, or both.
static const uint FLARE_VISIBILITY = 1;
static const uint FLARE_DEPTH = 2;
static const uint FLARE_ARTIFACT = 4;

struct FlareElement
{
    float4 layer; // Width, height, brightness, rotation in degrees.
    float2 center; // Bottom-origin viewport coordinates.
    float3 tint;
    uint flags;
};

FlareElement FlareLayer(float4 layer, uint flags)
{
    FlareElement element;
    element.layer = layer;
    element.center = _FlareSource.xy;
    element.tint = 1.0;
    element.flags = flags;
    return element;
}

// Each appearance supplies these functions in its own HLSL file.
FlareElement GetFlareElement(uint index);
float3 EvaluateFlareElement(uint index, float2 coordinate);

struct FlareVaryings
{
    float4 positionCS : SV_POSITION;
    float2 coordinate : TEXCOORD0;
    nointerpolation uint index : TEXCOORD1;
    nointerpolation uint flags : TEXCOORD2;
    nointerpolation float3 color : TEXCOORD3;
};

FlareVaryings FlareVertex(uint id : SV_VertexID, uint instance : SV_InstanceID)
{
    FlareElement element = GetFlareElement(instance);
    static const float2 corners[6] = {
        float2(-1,-1), float2(-1,1), float2(1,-1),
        float2(1,-1), float2(-1,1), float2(1,1)
    };
    float2 corner = corners[id];
    float sine, cosine;
    sincos(element.layer.w * (PI / 180.0), sine, cosine);
    float2 local = corner * element.layer.xy * _FlareScale;
    float2 offset = float2(cosine * local.x - sine * local.y, sine * local.x + cosine * local.y);
    offset.x /= _FlareAspect;
    float brightness = element.layer.z * _FlareColor.a;
    if (element.flags & FLARE_ARTIFACT)
        brightness *= _FlareArtifacts;
    if (element.flags & FLARE_VISIBILITY)
        brightness *= SAMPLE_TEXTURE2D_LOD(_FlareVisibility, sampler_PointClamp, 0.5, 0).r;
    FlareVaryings output;
    output.positionCS = brightness > 0.0
        ? float4(element.center * 2.0 - 1.0 + offset, 0.0, 1.0) : float4(0, 0, 0, 1);
    // Match the GPU projection used by scene geometry in the intermediate camera target.
    #if UNITY_UV_STARTS_AT_TOP
    output.positionCS.y = -output.positionCS.y;
    #endif
    output.coordinate = corner;
    output.index = instance;
    output.flags = element.flags;
    output.color = element.tint * _FlareColor.rgb * (3.0 * brightness);
    return output;
}

half4 FlareFragment(FlareVaryings input) : SV_Target
{
    float2 uv = input.positionCS.xy / _ScreenSize.xy;
    if (SAMPLE_TEXTURE2D_LOD(_UnderOceanMaskTexture, sampler_PointClamp, uv, 0).r > 0.0)
        discard;
    // Branch around depth work for optical spread. A conditional expression can evaluate both sides.
    float transmission = 1.0;
    [branch] if (input.flags & FLARE_DEPTH)
        transmission = FlareTransmission(uv);
    return half4(input.color * EvaluateFlareElement(input.index, input.coordinate) * transmission, 0);
}

#endif
