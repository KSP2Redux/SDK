// Scaled bodies use their own receiver-to-star lighting in every drawing context. The menu and SDK previews
// have no simulation receiver, so they keep their scene lights. Include after the material's body-sun fields.
#ifndef KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED

// Scaled coordinates are outside the local terrain shadow cascades.
#define KSP2_NO_TERRAIN_SHADOW
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

float3 KSP2_CelestialBodyDirectLighting(float3 positionWS, float4 positionCS, float3 normalWS,
    float3 viewDirWS, KSP2Surface surface, float2 normalizedScreenSpaceUV)
{
    [branch] if (KSP2_CelestialBodyUsesScaledLighting())
    {
        bool hasReceiver = _KSP2BodySunDirection.w > 0.5;
        Light sun = (Light)0;
        sun.direction = hasReceiver ? _KSP2BodySunDirection.xyz : _KSP2ScaledSunDirection.xyz;
        sun.color = hasReceiver ? _KSP2BodySunColor.rgb : _KSP2ScaledSunColor.rgb;
        sun.distanceAttenuation = 1.0;
        sun.shadowAttenuation = KSP2_EclipseVisibility(positionWS, _KSP2EclipseOccluderIndices0,
            _KSP2EclipseOccluderIndices1);
        return KSP2_ShadeLight(sun, normalWS, viewDirWS, surface);
    }

    float3 direct = KSP2_DirectLighting(positionWS, positionCS, normalWS, viewDirWS, surface,
        normalizedScreenSpaceUV);
    [branch] if (_KSP2EclipseMapActive > 0.5)
        direct *= KSP2_EclipseVisibility(positionWS, _KSP2EclipseOccluderIndices0, _KSP2EclipseOccluderIndices1);
    return direct;
}

// The stock solid surface stored galaxy-cube diffuse and specular in its G-buffer. This also gives gas giants
// an environment independent of the host camera's probes and avoids reading the observer cube while drawing it.
float3 KSP2_CelestialBodyGalaxyLighting(float3 normalWS, float3 viewDirWS, KSP2Surface surface)
{
    float3 diffuse = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, normalWS, _DiffuseCubemapMip),
        _GalaxyCubemapTexture_HDR) * surface.occlusion;
    float3 specular = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture,
            reflect(-viewDirWS, normalWS), 8.0),
        _GalaxyCubemapTexture_HDR) * _ReflectionIntensityMultiplier * surface.occlusion;
    return surface.diffuseAlbedo * diffuse + specular * KSP2_EnvironmentReflectionWeight(normalWS, viewDirWS,
        surface.smoothness, 0.96 - surface.metallic * 0.96, surface.F0, 1.0);
}

#endif // KSP2_CELESTIALBODY_SCALED_LIGHTING_INCLUDED
