// Scaled bodies use their own receiver-to-star lighting in every drawing context. The menu and SDK previews
// have no simulation receiver, so they keep their scene lights. Include after the material's body-sun fields.
#ifndef KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED

// Scaled coordinates are outside the local terrain shadow cascades.
#define KSP2_NO_TERRAIN_SHADOW
#include "KSP2CelestialBodyScaledPhotometry.hlsl"
#include "KSP2CelestialBodyGasSun.hlsl"
#include "../Include/KSP2Lighting.hlsl"

// Legacy explicit cubemap lighting, used only by materials without a simulation receiver.
float4 _ScaledSpaceLightPos;
float4 _ScaledSpaceLightColor;

// Fallback for a custom scaled view whose material has not received its body data yet.
float _KSP2ScaledViewActive;
float4 _KSP2ScaledSunDirection;
float4 _KSP2ScaledSunColor;

bool KSP2_CelestialBodyUsesScaledLighting()
{
    return _KSP2BodySunDirection.w > 0.5 || _KSP2ScaledViewActive > 0.5;
}

// The smooth sphere's N.L range over which the map and menu fade in at the terminator.
#define KSP2_MAP_NIGHT_RAMP 0.2

float KSP2_CelestialBodySolarVisibility(float3 positionWS, float3 horizonNormalWS,
    float3 sunDirectionWS, bool useMapNightRamp)
{
    float4 indices0 = _KSP2EclipseOccluderIndices0;
    float nightRamp = 1.0;
    // The map table lists the body itself first. Its own night uses the smooth sphere's ramp,
    // while neighbors still occlude the star per pixel.
    [branch] if (useMapNightRamp && _KSP2EclipseMapActive > 0.5 && indices0.x >= 0.0)
    {
        float4 selfOccluder = _KSP2EclipseOccluders[min((int)indices0.x, KSP2_ECLIPSE_MAX_OCCLUDERS - 1)];
        [branch] if (distance(selfOccluder.xyz, TransformObjectToWorld(float3(0.0, 0.0, 0.0))) < selfOccluder.w * 0.01)
        {
            indices0.x = -1.0;
            nightRamp = smoothstep(0.0, KSP2_MAP_NIGHT_RAMP, dot(horizonNormalWS, sunDirectionWS));
        }
    }

    return nightRamp * KSP2_EclipseVisibility(positionWS, indices0, _KSP2EclipseOccluderIndices1);
}

float3 KSP2_CelestialBodyDirectLighting(float3 positionWS, float4 positionCS, float3 normalWS,
    float3 viewDirWS, KSP2Surface surface, float2 normalizedScreenSpaceUV, float3 horizonNormalWS, bool useMapNightRamp)
{
    [branch] if (KSP2_CelestialBodyUsesScaledLighting())
    {
        bool hasReceiver = _KSP2BodySunDirection.w > 0.5;
        Light sun = (Light)0;
        sun.direction = hasReceiver ? _KSP2BodySunDirection.xyz : _KSP2ScaledSunDirection.xyz;
        sun.color = hasReceiver ? _KSP2BodySunColor.rgb : _KSP2ScaledSunColor.rgb;
        sun.distanceAttenuation = 1.0;
        sun.shadowAttenuation = KSP2_CelestialBodySolarVisibility(positionWS, horizonNormalWS,
            sun.direction, useMapNightRamp);
        sun.color *= KSP2_CelestialBodyGasSunTransmission(positionWS, sun.direction);
        return KSP2_ShadeLight(sun, normalWS, viewDirWS, surface);
    }

    float3 direct = KSP2_DirectLighting(positionWS, positionCS, normalWS, viewDirWS, surface,
        normalizedScreenSpaceUV);
    [branch] if (_KSP2EclipseMapActive > 0.5)
        direct *= KSP2_CelestialBodySolarVisibility(positionWS, horizonNormalWS, GetMainLight().direction, useMapNightRamp);
    return direct;
}

// Only a bound receiver owns these probe values. Unbound views must not inherit the observer's global GI.
float3 KSP2_CelestialBodyPlanetshine(float3 normalWS)
{
    [branch] if (_KSP2BodyGI < 0.5)
        return 0.0;
    return max(0.0, SampleSH(normalWS)) + KSP2_SphericalGaussianAmbient(normalWS);
}

// The stock solid surface stored galaxy-cube diffuse and specular in its G-buffer. This also gives gas giants
// an environment independent of the host camera's probes and avoids reading the observer cube while drawing it.
float3 KSP2_CelestialBodyGalaxyLighting(float3 normalWS, float3 viewDirWS, KSP2Surface surface)
{
    float3 diffuse = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, normalWS, _DiffuseCubemapMip),
        _GalaxyCubemapTexture_HDR);
    diffuse = (diffuse + KSP2_CelestialBodyPlanetshine(normalWS)) * surface.occlusion;
    float3 specular = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture,
            reflect(-viewDirWS, normalWS), 8.0),
        _GalaxyCubemapTexture_HDR) * _ReflectionIntensityMultiplier * surface.occlusion;
    return surface.diffuseAlbedo * diffuse + specular * KSP2_EnvironmentReflectionWeight(normalWS, viewDirWS,
        surface.smoothness, 0.96 - surface.metallic * 0.96, surface.F0, 1.0);
}

#endif // KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED
