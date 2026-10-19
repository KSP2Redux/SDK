// KSP2LocalSunTint.hlsl
//
// The atmosphere's colour on the local sun, per pixel. Stock tints the local sun once, with the transmittance toward the
// star at the camera (AtmosphereDataModelComponent.UpdateLocalSunlightColor), so from orbit the whole surface takes the
// camera's white light and mountains at the terminator miss the orange that reaches them. Redux.Rendering.LocalSunTint
// tabulates the same stock tint for the nearest atmosphere over the sun's elevation and the altitude, and each pixel
// takes its own tint in place of the camera's.
//
//   _KSP2SunTintLut       rgb the stock tint, u the sine of the sun's elevation from KSP2_SUN_TINT_MIN_ELEVATION to 1,
//                         v the altitude from the surface to the top of the atmosphere
//   _KSP2SunTintPlanet    xyz the planet's centre in the local scene, w its surface radius
//   _KSP2SunTintParams    x the atmosphere's height, y 1 while a table is set
//   _KSP2SunTintObserver  rgb the tint the local sun's colour already carries
//
// Only the local camera's pixels take it: the local eclipse table, which only that camera sees (KSP2Eclipse.hlsl), gates
// it.

#ifndef KSP2_LOCAL_SUN_TINT_INCLUDED
#define KSP2_LOCAL_SUN_TINT_INCLUDED

#include "KSP2Eclipse.hlsl"

// The same constant as LocalSunTint.MIN_ELEVATION_SINE.
#define KSP2_SUN_TINT_MIN_ELEVATION -0.25

TEXTURE2D(_KSP2SunTintLut);
float4 _KSP2SunTintPlanet;
float4 _KSP2SunTintParams;
float4 _KSP2SunTintObserver;

// The factor for the local sun's colour at a pixel: its own tint over the camera's, 1 without a table.
float3 KSP2_LocalSunTint(float3 positionWS, float3 lightDirectionWS)
{
    [branch]
    if (_KSP2SunTintParams.y < 0.5 || _KSP2EclipseLocalCount < 0.5)
        return 1.0;

    float3 fromCentre = positionWS - _KSP2SunTintPlanet.xyz;
    float radius = length(fromCentre);

    // Above the atmosphere the sun's ray takes its tint where it enters it, and a ray that misses the atmosphere or
    // leads away from it takes none, as AtmosphereModel.GetTransmittanceToSun reads it for the camera. Clamped to the
    // top, a point far out on the night side of the nearest atmosphere took the tint of a sun below its horizon, and
    // vessels around Tylo and Pol were left unlit or green under Jool's atmosphere (#2053, #2047).
    float topRadius = _KSP2SunTintPlanet.w + _KSP2SunTintParams.x;
    [branch]
    if (radius > topRadius)
    {
        float along = dot(fromCentre, lightDirectionWS);
        float discriminant = along * along - radius * radius + topRadius * topRadius;
        float entry = -along - sqrt(max(discriminant, 0.0));
        if (discriminant < 0.0 || entry <= 0.0)
            return 1.0 / max(_KSP2SunTintObserver.rgb, 1e-3);

        fromCentre += lightDirectionWS * entry;
        radius = topRadius;
    }

    float elevation = dot(fromCentre / max(radius, 1e-3), lightDirectionWS);
    float u = saturate((elevation - KSP2_SUN_TINT_MIN_ELEVATION) / (1.0 - KSP2_SUN_TINT_MIN_ELEVATION));
    float v = saturate((radius - _KSP2SunTintPlanet.w) / max(_KSP2SunTintParams.x, 1e-3));
    float3 tint = SAMPLE_TEXTURE2D_LOD(_KSP2SunTintLut, sampler_LinearClamp, float2(u, v), 0).rgb;
    return tint / max(_KSP2SunTintObserver.rgb, 1e-3);
}

#endif // KSP2_LOCAL_SUN_TINT_INCLUDED