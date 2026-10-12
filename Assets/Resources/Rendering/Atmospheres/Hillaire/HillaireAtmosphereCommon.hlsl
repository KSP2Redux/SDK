#ifndef REDUX_HILLAIRE_ATMOSPHERE_COMMON_INCLUDED
#define REDUX_HILLAIRE_ATMOSPHERE_COMMON_INCLUDED

// Hillaire 2020, A Scalable and Production Ready Sky and Atmosphere Rendering Technique, following the reference code
// at github.com/sebh/UnrealEngineSkyAtmosphere. Lengths are kilometers with the planet centre at the origin.

#define HILLAIRE_PI 3.14159265359

// Lifts sun lookups off the ground so a sample on it is not shadowed by the sphere it sits on.
#define HILLAIRE_PLANET_OFFSET 0.01

float _HillaireBottomRadius;
float _HillaireTopRadius;
float3 _HillaireSolarIrradiance;
float _HillaireSunAngularRadius;
float3 _HillaireSunDirection;
float3 sun_direction;
float3 _HillaireRayleighScattering;
float _HillaireRayleighExpScale;
float3 _HillaireMieScattering;
float3 _HillaireMieExtinction;
float _HillaireMiePhaseG;
float _HillaireMieExpScale;
float3 _HillaireAbsorptionExtinction;

// Bruneton's two density layers for absorption: x is the lower layer's width, then exp term, exp scale and linear
// term, with each layer's constant term in _HillaireAbsorptionConstants.
float4 _HillaireAbsorptionLayer0;
float4 _HillaireAbsorptionLayer1;
float2 _HillaireAbsorptionConstants;

float3 _HillaireGroundAlbedo;
float2 _HillaireExposure;
float _HillaireTransmittanceTint;
float _HillaireMultipleScatteringFactor;
float4 _HillaireRayMarchMinMaxSPP;

Texture2D<float4> _HillaireTransmittance;
Texture2D<float4> _HillaireMultiScattering;
// URP's Core.hlsl already declares this shared sampler through GlobalSamplers.hlsl.
#ifndef UNITY_CORE_SAMPLERS_INCLUDED
SamplerState sampler_LinearClamp;
#endif

struct HillaireMediumSample
{
    float3 scattering;
    float3 extinction;
    float3 rayleigh;
    float3 mie;
};

float3 HillaireSafeSunDirection()
{
    float3 dir = dot(_HillaireSunDirection, _HillaireSunDirection) > 0.001 ? _HillaireSunDirection : sun_direction;
    return normalize(dot(dir, dir) > 0.001 ? dir : float3(0.0, 1.0, 0.0));
}

float HillaireRaySphereNearest(float3 origin, float3 direction, float radius)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float h = b * b - c;
    if (h < 0.0)
    {
        return -1.0;
    }

    h = sqrt(h);
    float t0 = -b - h;
    float t1 = -b + h;
    if (t0 >= 0.0)
    {
        return t0;
    }

    return t1 >= 0.0 ? t1 : -1.0;
}

bool HillaireRaySphereInterval(float3 origin, float3 direction, float radius, out float tNear, out float tFar)
{
    float b = dot(origin, direction);
    float c = dot(origin, origin) - radius * radius;
    float h = b * b - c;
    if (h < 0.0)
    {
        tNear = 0.0;
        tFar = -1.0;
        return false;
    }

    h = sqrt(h);
    tNear = -b - h;
    tFar = -b + h;
    return tFar >= 0.0;
}

float HillaireRayleighPhase(float cosTheta)
{
    return 3.0 / (16.0 * HILLAIRE_PI) * (1.0 + cosTheta * cosTheta);
}

// Cornette-Shanks, as Bruneton's, with cosTheta measured against the reversed view direction.
float HillaireMiePhase(float g, float cosTheta)
{
    float k = 3.0 / (8.0 * HILLAIRE_PI) * (1.0 - g * g) / (2.0 + g * g);
    return k * (1.0 + cosTheta * cosTheta) / pow(max(0.0001, 1.0 + g * g - 2.0 * g * -cosTheta), 1.5);
}

float HillaireLayerDensity(float4 layer, float constantTerm, float height)
{
    return saturate(layer.y * exp(layer.z * height) + layer.w * height + constantTerm);
}

HillaireMediumSample HillaireSampleMedium(float3 position)
{
    float height = max(0.0, length(position) - _HillaireBottomRadius);
    float densityRay = exp(_HillaireRayleighExpScale * height);
    float densityMie = exp(_HillaireMieExpScale * height);
    float densityAbsorption = height < _HillaireAbsorptionLayer0.x
        ? HillaireLayerDensity(_HillaireAbsorptionLayer0, _HillaireAbsorptionConstants.x, height)
        : HillaireLayerDensity(_HillaireAbsorptionLayer1, _HillaireAbsorptionConstants.y, height);

    HillaireMediumSample sampleData;
    sampleData.rayleigh = densityRay * _HillaireRayleighScattering;
    sampleData.mie = densityMie * _HillaireMieScattering;
    sampleData.scattering = sampleData.rayleigh + sampleData.mie;
    sampleData.extinction = sampleData.rayleigh + densityMie * _HillaireMieExtinction +
        densityAbsorption * _HillaireAbsorptionExtinction;
    return sampleData;
}

// The transmittance table covers rays that leave through the top without meeting the ground, in Bruneton's
// parameterization: the distance to the top along the ray, and the height as rho over the horizon distance.
void HillaireUvToTransmittanceParams(float2 uv, out float viewHeight, out float viewZenithCosAngle)
{
    float h = sqrt(max(0.0, _HillaireTopRadius * _HillaireTopRadius - _HillaireBottomRadius * _HillaireBottomRadius));
    float rho = h * uv.y;
    viewHeight = sqrt(rho * rho + _HillaireBottomRadius * _HillaireBottomRadius);

    float dMin = _HillaireTopRadius - viewHeight;
    float dMax = rho + h;
    float d = dMin + uv.x * (dMax - dMin);
    viewZenithCosAngle = d == 0.0 ? 1.0 : (h * h - rho * rho - d * d) / (2.0 * viewHeight * d);
    viewZenithCosAngle = clamp(viewZenithCosAngle, -1.0, 1.0);
}

float2 HillaireTransmittanceParamsToUv(float viewHeight, float viewZenithCosAngle)
{
    float h = sqrt(max(0.0, _HillaireTopRadius * _HillaireTopRadius - _HillaireBottomRadius * _HillaireBottomRadius));
    float rho = sqrt(max(0.0, viewHeight * viewHeight - _HillaireBottomRadius * _HillaireBottomRadius));
    float discriminant = viewHeight * viewHeight * (viewZenithCosAngle * viewZenithCosAngle - 1.0) +
        _HillaireTopRadius * _HillaireTopRadius;
    float d = max(0.0, -viewHeight * viewZenithCosAngle + sqrt(max(0.0, discriminant)));
    float dMin = _HillaireTopRadius - viewHeight;
    float dMax = rho + h;
    return float2((d - dMin) / max(0.0001, dMax - dMin), rho / max(0.0001, h));
}

float3 HillaireTransmittanceToTop(float viewHeight, float viewZenithCosAngle)
{
    float2 uv = HillaireTransmittanceParamsToUv(viewHeight, viewZenithCosAngle);
    return _HillaireTransmittance.SampleLevel(sampler_LinearClamp, uv, 0).rgb;
}

// Sunlight reaching a point at viewHeight with the sun at sunZenithCosAngle. The planet's shadow edge is softened over
// the sun's disk, as Bruneton's precompute does, so the terminator has no hard step.
float3 HillaireSunTransmittance(float viewHeight, float sunZenithCosAngle)
{
    float r = max(viewHeight, _HillaireBottomRadius + HILLAIRE_PLANET_OFFSET);
    float sinHorizon = _HillaireBottomRadius / r;
    float cosHorizon = -sqrt(max(0.0, 1.0 - sinHorizon * sinHorizon));
    float edge = sinHorizon * max(_HillaireSunAngularRadius, 0.0001);
    float visible = smoothstep(-edge, edge, sunZenithCosAngle - cosHorizon);
    return visible * HillaireTransmittanceToTop(r, sunZenithCosAngle);
}

// The isotropic multiple-scattering transfer for unit sun illuminance, by sun angle and height.
float3 HillaireMultipleScattering(float viewHeight, float sunZenithCosAngle)
{
    float2 uv = saturate(float2(
        sunZenithCosAngle * 0.5 + 0.5,
        (viewHeight - _HillaireBottomRadius) / max(0.001, _HillaireTopRadius - _HillaireBottomRadius)));
    return _HillaireMultiScattering.SampleLevel(sampler_LinearClamp, uv, 0).rgb;
}

// Paper's sample count: from the minimum to the maximum as the segment grows to 100 km.
int HillaireSampleCount(float tMax)
{
    return (int)max(1.0, floor(lerp(_HillaireRayMarchMinMaxSPP.x, _HillaireRayMarchMinMaxSPP.y, saturate(tMax * 0.01))));
}

// In-scattered luminance along a segment of the view ray, with the transmittance across it. Samples are spaced
// quadratically so they bunch near the start, and each segment is integrated analytically against its extinction.
// offset places the sample inside each segment, in [0, 1).
float3 HillaireRayMarch(float3 origin, float3 direction, float tMax, int sampleCount, float offset, out float3 transmittance)
{
    float3 sunDir = HillaireSafeSunDirection();
    float cosTheta = -dot(direction, sunDir);
    float rayleighPhase = HillaireRayleighPhase(cosTheta);
    float miePhase = HillaireMiePhase(_HillaireMiePhaseG, cosTheta);

    float3 luminance = 0.0;
    transmittance = 1.0;
    float tPrevious = 0.0;

    [loop]
    for (int i = 0; i < sampleCount; i++)
    {
        float tNext = (i + 1.0) / sampleCount;
        tNext = tNext * tNext * tMax;
        float dt = tNext - tPrevious;
        float3 position = origin + direction * (tPrevious + dt * offset);
        tPrevious = tNext;

        float viewHeight = length(position);
        float sunZenithCosAngle = dot(position / viewHeight, sunDir);
        HillaireMediumSample medium = HillaireSampleMedium(position);
        float3 sunTransmittance = HillaireSunTransmittance(viewHeight, sunZenithCosAngle);
        float3 multipleScattering = HillaireMultipleScattering(viewHeight, sunZenithCosAngle) * _HillaireMultipleScatteringFactor;

        float3 source = _HillaireSolarIrradiance *
            (sunTransmittance * (medium.rayleigh * rayleighPhase + medium.mie * miePhase) +
            multipleScattering * medium.scattering);
        float3 extinction = max(medium.extinction, 1e-6);
        float3 stepTransmittance = exp(-extinction * dt);
        luminance += transmittance * (source - source * stepTransmittance) / extinction;
        transmittance *= stepTransmittance;
    }

    return luminance;
}

#endif
