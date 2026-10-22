// Incoming sunlight at the opaque gas cloud deck. The separate atmosphere shell supplies view-ray haze.
#ifndef KSP2_CELESTIALBODY_GAS_SUN_INCLUDED
#define KSP2_CELESTIALBODY_GAS_SUN_INCLUDED

TEXTURE2D(_KSP2GasGiantTransmittance);
SAMPLER(sampler_KSP2GasGiantTransmittance);

float3 KSP2_CelestialBodyGasSunTransmission(float3 positionWS, float3 sunDirectionWS)
{
    float bottom = _KSP2GasGiantAtmosphere.x;
    float top = _KSP2GasGiantAtmosphere.y;
    [branch] if (_KSP2GasGiantSolarAttenuation <= 0.0 || bottom <= 0.0 || top <= bottom)
        return 1.0;

    float3 centerWS = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float3 radial = normalize(positionWS - centerWS);
    float muS = clamp(dot(radial, sunDirectionWS), -1.0, 1.0);
    float horizonLength = sqrt(top * top - bottom * bottom);
    float distanceToTop = max(-bottom * muS + sqrt(max(bottom * bottom * (muS * muS - 1.0) + top * top, 0.0)), 0.0);
    float x = saturate((distanceToTop - (top - bottom)) / (horizonLength - (top - bottom)));
    float2 size = _KSP2GasGiantAtmosphere.zw;
    float2 uv = 0.5 / size + float2(x, 0.0) * (1.0 - 1.0 / size);
    float3 transmission = SAMPLE_TEXTURE2D_LOD(_KSP2GasGiantTransmittance,
        sampler_KSP2GasGiantTransmittance, uv, 0.0).rgb;
    float sunRadius = max(_KSP2GasGiantSunAngularRadius, 1e-6);
    transmission *= smoothstep(-sunRadius, sunRadius, muS);
    return lerp(1.0, transmission, saturate(_KSP2GasGiantSolarAttenuation));
}

#define KSP2_CUSTOM_MAIN_LIGHT_COLOR(positionWS, light) \
    light.color *= KSP2_CelestialBodyGasSunTransmission(positionWS, light.direction)

#endif
