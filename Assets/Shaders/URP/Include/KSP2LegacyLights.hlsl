// KSP2LegacyLights.hlsl
//
// Built-in pipeline falloff for URP's additional point and spot lights. Every KSP2 light (stock,
// Redux and mod) was authored for the Built-in attenuation, which stays finite at the light. URP's
// physical 1/d^2 is hundreds of times brighter a few centimetres from a short-range light (the
// kerbal face fill lights sit 8 to 12 cm from the face with a 0.2 m range) and much darker beyond
// about a fifth of the range.
//
// Built-in (Internal-DeferredShading, ForwardAdd, AutoLight.cginc):
//   range: _LightTextureB0 at (d / range)^2 = 1 / (1 + 25 (d / range)^2), faded linearly in
//          (d / range)^2 from 1 at 0.64 to 0 at 1
//   cone:  the default spot cookie ("Soft" in unity default resources) projected over the spot
//          angle: 1 up to 65 % of tan(spotAngle / 2), cosine roll-off to 0 at the cone edge
//          (within 0.02 of the texture). A light with its own cookie gets only the cookie.
//
// URP light data read here: position.w is 0 for directional lights, attenuation.x = 1 / range^2,
// spot lights store attenuation.z = 1 / (cosInner - cosOuter) and attenuation.w = -cosOuter * z
// (point lights z = 0, w = 1), spotDirection = -forward.
//
// Include after URP's Lighting.hlsl and fetch additional lights with KSP2_GetAdditionalLight.

#ifndef KSP2_LEGACY_LIGHTS_INCLUDED
#define KSP2_LEGACY_LIGHTS_INCLUDED

// Reuse each vessel's plasma capture for windward-contact falloff. Zero means an ordinary light.
float _ReentryContactEnabled;
float _ReentryContactLightSlots[MAX_VISIBLE_LIGHTS];
float4x4 _ReentryContactWorldToFlow[4];
float4 _ReentryContactRects[4];
float4 _ReentryContactParams[4];
TEXTURE2D(_ReentryContactSurface0); TEXTURE2D(_ReentryContactEdge0);
TEXTURE2D(_ReentryContactSurface1); TEXTURE2D(_ReentryContactEdge1);
TEXTURE2D(_ReentryContactSurface2); TEXTURE2D(_ReentryContactEdge2);
TEXTURE2D(_ReentryContactSurface3); TEXTURE2D(_ReentryContactEdge3);

float KSP2_ReentryContactProximity(TEXTURE2D_PARAM(surfaceTexture, surfaceSampler),
    TEXTURE2D_PARAM(edgeTexture, edgeSampler), float2 uv, float3 position, float4 rect, float invRangeSqr)
{
    float4 surface = SAMPLE_TEXTURE2D_LOD(surfaceTexture, surfaceSampler, uv, 0);
    float distanceSqr;
    UNITY_BRANCH
    if (surface.z >= 0.5)
    {
        // Inside the silhouette, only the separation from the windward depth is needed.
        float separation = position.z - surface.w / surface.z;
        distanceSqr = separation * separation;
    }
    else
    {
        // Only fragments outside the silhouette need the nearest projected edge seed.
        // This is a windward projection approximation, not an exact 3D nearest-point query.
        float4 edge = SAMPLE_TEXTURE2D_LOD(edgeTexture, edgeSampler, uv, 0);
        if (edge.w <= 0)
            return 0;
        float3 contact = float3(rect.xy + edge.xy * rect.zw, edge.z);
        float3 delta = position - contact;
        distanceSqr = dot(delta, delta);
    }
    return rcp(1 + distanceSqr * invRangeSqr);
}

float KSP2_ReentryContactAttenuation(int lightIndex, float3 positionWS, float attenuation)
{
    UNITY_BRANCH
    if (_ReentryContactEnabled < 0.5 || attenuation <= 0 || lightIndex < 0 || lightIndex >= MAX_VISIBLE_LIGHTS)
        return attenuation;
    int slot = (int)_ReentryContactLightSlots[lightIndex] - 1;
    if (slot < 0 || slot >= 4)
        return attenuation;

    float3 position = mul(_ReentryContactWorldToFlow[slot], float4(positionWS, 1)).xyz;
    float4 rect = _ReentryContactRects[slot];
    float2 uv = (position.xy - rect.xy) / rect.zw;
    float2 falloff = _ReentryContactParams[slot].xy;
    if (any(uv < 0) || any(uv > 1))
        return attenuation * falloff.y;

    #define REENTRY_CONTACT_PROXIMITY(index) KSP2_ReentryContactProximity( \
        TEXTURE2D_ARGS(_ReentryContactSurface##index, sampler_LinearClamp), \
        TEXTURE2D_ARGS(_ReentryContactEdge##index, sampler_PointClamp), uv, position, rect, falloff.x)
    float proximity;
    UNITY_BRANCH
    if (slot == 0) { proximity = REENTRY_CONTACT_PROXIMITY(0); }
    else if (slot == 1) { proximity = REENTRY_CONTACT_PROXIMITY(1); }
    else if (slot == 2) { proximity = REENTRY_CONTACT_PROXIMITY(2); }
    else { proximity = REENTRY_CONTACT_PROXIMITY(3); }
    #undef REENTRY_CONTACT_PROXIMITY
    return attenuation * lerp(falloff.y, 1, proximity);
}

// _LightTextureB0: the Built-in range falloff.
float KSP2_LegacyRangeAttenuation(float distanceSqr, float invRangeSqr)
{
    float r2 = distanceSqr * invRangeSqr;
    return saturate((1.0 - r2) * (1.0 / 0.36)) / (1.0 + 25.0 * r2);
}

// The default spot cookie. cosAngle is the cosine between the spot axis and the light-to-surface
// direction, cosOuter the cosine of half the spot angle.
float KSP2_LegacySpotCookie(float cosAngle, float cosOuter)
{
    float tanHalf = sqrt(saturate(1.0 - cosOuter * cosOuter)) / max(cosOuter, 1e-4);
    // r = tan(angle) / tan(spotAngle / 2), without dividing by cosAngle
    float r = sqrt(saturate(1.0 - cosAngle * cosAngle)) / max(cosAngle * tanHalf, 1e-6);
    float t = saturate((r - 0.65) * (1.0 / 0.35));
    return cosAngle > 0.0 ? 0.5 + 0.5 * cos(PI * t) : 0.0;
}

// Built-in distance and cone attenuation of the additional light at light buffer index
// lightIndex, for a surface at positionWS. 1 for directional lights.
float KSP2_LegacyAdditionalLightAttenuation(int lightIndex, float3 positionWS)
{
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    float4 lightPositionWS = _AdditionalLightsBuffer[lightIndex].position;
    float4 attenuation = _AdditionalLightsBuffer[lightIndex].attenuation;
    float3 spotDirection = _AdditionalLightsBuffer[lightIndex].spotDirection.xyz;
#else
    float4 lightPositionWS = _AdditionalLightsPosition[lightIndex];
    float4 attenuation = _AdditionalLightsAttenuation[lightIndex];
    float3 spotDirection = _AdditionalLightsSpotDir[lightIndex].xyz;
#endif
    if (lightPositionWS.w == 0.0)
        return 1.0;

    float3 lightVector = lightPositionWS.xyz - positionWS;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);
    float atten = KSP2_LegacyRangeAttenuation(distanceSqr, attenuation.x);
    if (attenuation.z > 0.0)
    {
        float cosAngle = dot(spotDirection, lightVector * rsqrt(distanceSqr));
    #if defined(_LIGHT_COOKIES)
        // URP already multiplied the cookie into light.color. Built-in used the cookie as the cone.
        if (IsLightCookieEnabled(lightIndex))
            return cosAngle > 0.0 ? atten : 0.0;
    #endif
        atten *= KSP2_LegacySpotCookie(cosAngle, -attenuation.w / attenuation.z);
    }
    return atten;
}

// GetAdditionalLight's loop index to the light buffer index.
int KSP2_AdditionalLightBufferIndex(uint i)
{
#if USE_CLUSTER_LIGHT_LOOP
    return (int)i;
#else
    return GetPerObjectLightIndex(i);
#endif
}

// GetAdditionalLight with the Built-in falloff in place of URP's distance and angle attenuation.
// Colour, direction, shadows, cookie and layer mask are URP's.
Light KSP2_GetAdditionalLight(uint i, InputData inputData, half4 shadowMask, AmbientOcclusionFactor aoFactor)
{
    Light light = GetAdditionalLight(i, inputData, shadowMask, aoFactor);
    int index = KSP2_AdditionalLightBufferIndex(i);
    light.distanceAttenuation = KSP2_ReentryContactAttenuation(index, inputData.positionWS,
        KSP2_LegacyAdditionalLightAttenuation(index, inputData.positionWS));
    return light;
}

Light KSP2_GetAdditionalLight(uint i, float3 positionWS, half4 shadowMask)
{
    Light light = GetAdditionalLight(i, positionWS, shadowMask);
    int index = KSP2_AdditionalLightBufferIndex(i);
    light.distanceAttenuation = KSP2_ReentryContactAttenuation(index, positionWS,
        KSP2_LegacyAdditionalLightAttenuation(index, positionWS));
    return light;
}

Light KSP2_GetAdditionalLight(uint i, float3 positionWS)
{
    Light light = GetAdditionalLight(i, positionWS);
    int index = KSP2_AdditionalLightBufferIndex(i);
    light.distanceAttenuation = KSP2_ReentryContactAttenuation(index, positionWS,
        KSP2_LegacyAdditionalLightAttenuation(index, positionWS));
    return light;
}

#endif // KSP2_LEGACY_LIGHTS_INCLUDED
