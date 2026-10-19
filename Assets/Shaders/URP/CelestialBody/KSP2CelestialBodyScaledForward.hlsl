// KSP2CelestialBodyScaledForward.hlsl
//
// ForwardLit pass of KSP2/Environment/CelestialBody/CelestialBody_Scaled. Material math from
// ksp2-shaders/CelestialBody_Scaled/final. Stock cameras rendered deferred, so this does what the
// stock Deferred pass and Unity's deferred lighting did together: the galaxy-cube IBL and emission
// the G-buffer carried, plus the URP main light and Forward+ lights through the KSP2 Standard BRDF.
// Include after KSP2CelestialBodyScaledCommon.hlsl.

#ifndef KSP2_CELESTIALBODY_SCALED_FORWARD_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_FORWARD_INCLUDED

// Scaled-space positions are not in the local scene's space the terrain shadow cascades cover.
#define KSP2_NO_TERRAIN_SHADOW
#include "../Include/KSP2Lighting.hlsl"

TEXTURE2D(_MainTex);            SAMPLER(sampler_MainTex);
TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
TEXTURE2D(_PackedMap);          SAMPLER(sampler_PackedMap);
TEXTURE2D(_EmissionTex);        SAMPLER(sampler_EmissionTex);
TEXTURE2D(_NorthPoleDiffuse);   SAMPLER(sampler_NorthPoleDiffuse);
TEXTURE2D(_NorthPoleNormal);    SAMPLER(sampler_NorthPoleNormal);
TEXTURE2D(_SouthPoleDiffuse);   SAMPLER(sampler_SouthPoleDiffuse);
TEXTURE2D(_SouthPoleNormal);    SAMPLER(sampler_SouthPoleNormal);

struct CelestialBodyScaledAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 uv0        : TEXCOORD0;
};

struct CelestialBodyScaledVaryings
{
    float4 positionCS  : SV_POSITION;
    float2 uv          : TEXCOORD0;
    float3 positionWS  : TEXCOORD1;
    float3 tangentWS   : TEXCOORD2;
    float3 bitangentWS : TEXCOORD3;
    float3 normalWS    : TEXCOORD4;
    float4 screenPos   : TEXCOORD5;
};

CelestialBodyScaledVaryings CelestialBodyScaled_ForwardVertex(CelestialBodyScaledAttributes input)
{
    CelestialBodyScaledVaryings output;
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.uv = input.uv0 * _MainTex_ST.xy + _MainTex_ST.zw;

    // Stock tangent frame: normalized normal and tangent, signed cross product, per vertex.
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
    output.normalWS = normalWS;
    output.tangentWS = tangentWS;
    output.bitangentWS = cross(normalWS, tangentWS) * (input.tangentOS.w * GetOddNegativeScale());
    output.screenPos = CelestialBodyScaled_ScreenPos(output.positionCS);
    return output;
}

// Top-down planar projection of the pole textures in planet object space.
float2 CelestialBodyScaled_PoleUV(float3 positionWS, float2 offset, float poleScale)
{
    float3 positionOS = TransformWorldToObject(positionWS);
    return (positionOS.xz + offset) / _PlanetScale / poleScale;
}

float4 CelestialBodyScaled_ForwardFragment(CelestialBodyScaledVaryings input) : SV_Target
{
    CelestialBodyScaled_DitherClip(input.screenPos);

    float2 uv = input.uv;
    float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);

    float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
    float3 tangentNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), _NormalScale);
    float4 packed = SAMPLE_TEXTURE2D(_PackedMap, sampler_PackedMap, uv);
    float metallic = packed.r;
    float occlusion = packed.g * _AOScale + (1.0 - _AOScale);
    float smoothness = packed.a;
    float3 emission = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, uv).rgb * _EmissionScale;

    // Pole overlays: albedo and tangent normal blend toward the projected pole textures.
    // Derivatives come from outside the branches so the samples stay valid inside them.
    float2 northUV = CelestialBodyScaled_PoleUV(input.positionWS, _NorthPoleOffset.xy, _NorthPoleScale);
    float2 southUV = CelestialBodyScaled_PoleUV(input.positionWS, _SouthPoleOffset.xy, _SouthPoleScale);
    float2 northDx = ddx(northUV), northDy = ddy(northUV);
    float2 southDx = ddx(southUV), southDy = ddy(southUV);

    [branch] if (_NorthPoleBlendStart < uv.y)
    {
        float blend = saturate((uv.y - _NorthPoleBlendStart) / (1.0 - _NorthPoleBlendStart) * _NorthPoleBlend);
        float3 poleAlbedo = SAMPLE_TEXTURE2D_GRAD(_NorthPoleDiffuse, sampler_NorthPoleDiffuse, northUV, northDx, northDy).rgb;
        float3 poleNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D_GRAD(_NorthPoleNormal, sampler_NorthPoleNormal, northUV, northDx, northDy), _NormalScale);
        albedo = lerp(albedo, poleAlbedo, blend);
        tangentNormal = lerp(tangentNormal, poleNormal, blend);
    }

    [branch] if (uv.y < _SouthPoleBlendStart)
    {
        float blend = saturate((1.0 - uv.y / _SouthPoleBlendStart) * _SouthPoleBlend);
        float3 poleAlbedo = SAMPLE_TEXTURE2D_GRAD(_SouthPoleDiffuse, sampler_SouthPoleDiffuse, southUV, southDx, southDy).rgb;
        float3 poleNormal = KSP2_DecodeNormal(SAMPLE_TEXTURE2D_GRAD(_SouthPoleNormal, sampler_SouthPoleNormal, southUV, southDx, southDy), _NormalScale);
        albedo = lerp(albedo, poleAlbedo, blend);
        tangentNormal = lerp(tangentNormal, poleNormal, blend);
    }

    float3 normalWS = normalize(tangentNormal.x * input.tangentWS + tangentNormal.y * input.bitangentWS + tangentNormal.z * input.normalWS);

    KSP2Surface surface = KSP2_MakeMetallicSurface(albedo, metallic, smoothness, occlusion, saturate(emission), 1.0);

    // Galaxy-cube IBL as the stock G-buffer emission held it: diffuse along the normal at
    // _DiffuseCubemapMip, specular along the reflection at mip 8, both occluded.
    float3 diffuseIBL = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, normalWS, _DiffuseCubemapMip),
        _GalaxyCubemapTexture_HDR) * occlusion;
    float3 reflectDirWS = reflect(-viewDirWS, normalWS);
    float3 specularIBL = DecodeHDREnvironment(
        SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, reflectDirWS, 8.0),
        _GalaxyCubemapTexture_HDR) * _ReflectionIntensityMultiplier * occlusion;

    // The split-sum environment BRDF (KSP2Lighting.hlsl) in place of stock's grazing fresnel, which made the
    // planets look wet at low viewing angles.
    float3 specular = KSP2_EnvironmentReflectionWeight(normalWS, viewDirWS, smoothness, 0.96 - metallic * 0.96, surface.F0, 1.0)
        * specularIBL;

    float3 color = surface.diffuseAlbedo * diffuseIBL + specular + surface.emission;

    // What Unity's deferred light passes added: the sun and any other lights, shadowed. Inside the flight
    // camera's scaled-space pass the scaled sun is the only light, shadowed by the eclipses of the body's
    // neighbours (KSP2Eclipse.hlsl), which stock did not have in scaled space.
    [branch] if (_KSP2ScaledViewActive > 0.5)
    {
        Light sun = (Light)0;
        sun.direction = _KSP2ScaledSunDirection.xyz;
        sun.color = _KSP2ScaledSunColor.rgb;
        sun.distanceAttenuation = 1.0;
        sun.shadowAttenuation = KSP2_EclipseVisibility(input.positionWS, _KSP2EclipseOccluderIndices0,
            _KSP2EclipseOccluderIndices1);
        color += KSP2_ShadeLight(sun, normalWS, viewDirWS, surface);
    }
    else
    {
        float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
        float3 direct = KSP2_DirectLighting(input.positionWS, input.positionCS, normalWS, viewDirWS, surface, normalizedScreenSpaceUV);
        // The map's only light is the star's map light, so the map table's eclipse takes all of it. The flag is set
        // for the map camera alone.
        [branch] if (_KSP2EclipseMapActive > 0.5)
            direct *= KSP2_EclipseVisibility(input.positionWS, _KSP2EclipseOccluderIndices0, _KSP2EclipseOccluderIndices1);
        color += direct;
    }
    return float4(color, 1.0);
}

#endif // KSP2_CELESTIALBODY_SCALED_FORWARD_INCLUDED