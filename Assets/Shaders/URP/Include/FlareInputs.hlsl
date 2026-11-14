#ifndef REDUX_FLARE_INPUTS_INCLUDED
#define REDUX_FLARE_INPUTS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_FlareSceneDepth);
TEXTURE2D(_FlareCoverage);
TEXTURE2D(_FlareVisibility);
TEXTURE2D(_FlarePreviousVisibility);
TEXTURE2D(_OceanDepthTexture);
TEXTURE2D(_UnderOceanMaskTexture);
TEXTURE2D(_CloudTex);
float4 _FlareSource;
float4 _FlareColor;
float4 _FlareDepthParameters;
float4 _FlareVisibilitySettings;
float4 _FlareCoverageRect;
float2 _FlareFrameJitter;
float _FlareAspect, _FlareScale, _FlareCoverageValid, _FlareVisibilityValid, _FlareArtifacts;
float _CloudFlareOpacity;

float FlareLinearDepth(float depth)
{
    return rcp(_FlareDepthParameters.x * depth + _FlareDepthParameters.y);
}

float FlareVisibleDepth(float depth)
{
    // Local-light bias scales with source distance, not the camera's potentially enormous far plane.
    float threshold = _FlareSource.w > 0.5 ? 0.999 : _FlareSource.z * 0.9999;
    return step(threshold, FlareLinearDepth(depth));
}

float FlareCoverage(float2 uv)
{
    uint width, height;
    _FlareSceneDepth.GetDimensions(width, height);
    int2 size = int2(width, height);
    float2 position = saturate(uv + _FlareFrameJitter) * size - 0.5;
    int2 lower = (int2)floor(position);
    float2 blend = frac(position);
    float4 coverage = float4(
        FlareVisibleDepth(LOAD_TEXTURE2D(_FlareSceneDepth, clamp(lower, 0, size - 1)).r),
        FlareVisibleDepth(LOAD_TEXTURE2D(_FlareSceneDepth, clamp(lower + int2(1, 0), 0, size - 1)).r),
        FlareVisibleDepth(LOAD_TEXTURE2D(_FlareSceneDepth, clamp(lower + int2(0, 1), 0, size - 1)).r),
        FlareVisibleDepth(LOAD_TEXTURE2D(_FlareSceneDepth, clamp(lower + int2(1, 1), 0, size - 1)).r));
    return lerp(lerp(coverage.x, coverage.y, blend.x), lerp(coverage.z, coverage.w, blend.x), blend.y);
}

float FlareOceanVisibility(float2 uv)
{
    return FlareVisibleDepth(SAMPLE_TEXTURE2D_LOD(_OceanDepthTexture, sampler_PointClamp,
        saturate(uv + _FlareFrameJitter), 0).r);
}

float FlareCloudTransmission(float2 uv)
{
    return 1.0 - saturate(SAMPLE_TEXTURE2D_LOD(_CloudTex, sampler_LinearClamp, uv, 0).a) * _CloudFlareOpacity;
}

float FlareTransmission(float2 uv)
{
    float coverage;
    if (_FlareCoverageValid > 0.5)
    {
        float2 coverageUv = (uv - _FlareCoverageRect.xy) / _FlareCoverageRect.zw;
        coverage = all(coverageUv >= 0.0) && all(coverageUv <= 1.0)
            ? SAMPLE_TEXTURE2D_LOD(_FlareCoverage, sampler_LinearClamp, coverageUv, 0).r : FlareCoverage(uv);
    }
    else
        coverage = FlareCoverage(uv);
    return coverage * FlareOceanVisibility(uv) * FlareCloudTransmission(uv);
}

#endif
