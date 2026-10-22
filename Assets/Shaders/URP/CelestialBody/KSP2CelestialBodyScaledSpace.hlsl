// KSP2CelestialBodyScaledSpace.hlsl
//
// Pass 0 of KSP2/Environment/CelestialBody/CelestialBody_Scaled: the scaled-space Lambert term, or the airless
// regolith response, using the receiver's sun. Legacy globals serve previews. CubemapViewBase draws opaque
// bodies into the observer cubemap with DrawRenderer(..., pass 0). Include after
// KSP2CelestialBodyScaledCommon.hlsl.

#ifndef KSP2_CELESTIALBODY_SCALED_SPACE_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_SPACE_INCLUDED

#include "KSP2CelestialBodyScaledLighting.hlsl"

TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

struct CelestialBodyScaledSpaceAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 uv0        : TEXCOORD0;
};

struct CelestialBodyScaledSpaceVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float4 diffuse    : COLOR;
    float3 positionWS : TEXCOORD1;
    float3 normalWS   : TEXCOORD2;
    float eclipse     : TEXCOORD3;
};

CelestialBodyScaledSpaceVaryings CelestialBodyScaled_ScaledSpaceVertex(CelestialBodyScaledSpaceAttributes input)
{
    CelestialBodyScaledSpaceVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.normalWS = normalWS;
    // Darkened where the body's neighbours hide the star (KSP2Eclipse.hlsl), per vertex like the Lambert term.
    float eclipse = KSP2_EclipseVisibility(output.positionWS, _KSP2EclipseOccluderIndices0,
        _KSP2EclipseOccluderIndices1);
    bool hasReceiver = _KSP2BodySunDirection.w > 0.5;
    float3 sunDirection = hasReceiver ? _KSP2BodySunDirection.xyz : _ScaledSpaceLightPos.xyz;
    float4 sunColor = hasReceiver ? float4(_KSP2BodySunColor.rgb, 1.0) : _ScaledSpaceLightColor;
    output.diffuse = max(dot(normalWS, sunDirection), 0.0) * sunColor * eclipse;
    output.eclipse = eclipse;
    return output;
}

float4 CelestialBodyScaled_ScaledSpaceFragment(CelestialBodyScaledSpaceVaryings input) : SV_Target
{
    float4 diffuse = input.diffuse;
    bool hasReceiver = _KSP2BodySunDirection.w > 0.5;
    float3 sunDirection = hasReceiver ? _KSP2BodySunDirection.xyz : _ScaledSpaceLightPos.xyz;
    float weight = KSP2_CelestialBodyRegolithWeight();
    [branch] if (weight > 0.0)
    {
        float3 sunColor = hasReceiver ? _KSP2BodySunColor.rgb : _ScaledSpaceLightColor.rgb;
        float3 viewDirection = normalize(_WorldSpaceCameraPos - input.positionWS);
        float regolith = KSP2_CelestialBodyRegolithDiffuse(normalize(input.normalWS), viewDirection, sunDirection);
        diffuse.rgb = lerp(diffuse.rgb, sunColor * input.eclipse * regolith, weight);
    }
    diffuse.rgb *= KSP2_CelestialBodyGasSunTransmission(input.positionWS, sunDirection);
    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color * diffuse;
}

#endif // KSP2_CELESTIALBODY_SCALED_SPACE_INCLUDED
