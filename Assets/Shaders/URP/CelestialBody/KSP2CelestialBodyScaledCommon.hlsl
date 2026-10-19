// KSP2CelestialBodyScaledCommon.hlsl
//
// Code shared by every pass of KSP2/Environment/CelestialBody/CelestialBody_Scaled: the scaled-space
// sun globals, the scaled/local LOD transition dither and the DepthOnly / DepthNormals functions.
// Also included by the SDK stand-in KSP2/Planets/Scaled
// (Packages/ksp2redux.sdk/Assets/Shaders/KSP2/Planets/Scaled.shader) and by
// Redux/Dummy/CelestialBody_Scaled, so scaled materials authored on them look like the port.
//
// Included from the SubShader HLSLINCLUDE after the UnityPerMaterial block, which holds the body's eclipse
// occluders as _KSP2EclipseOccluderIndices0 and 1 (set by Redux.Rendering.Eclipses.EclipseOccluderTable).

#ifndef KSP2_CELESTIALBODY_SCALED_COMMON_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_COMMON_INCLUDED

#include "../Include/KSP2Eclipse.hlsl"

// Scaled-space sun, set globally by LightingSystem.UpdateLightingSystem (pass 0 only).
float4 _ScaledSpaceLightPos;
float4 _ScaledSpaceLightColor;

// Set by Redux.Rendering.ScaledSpaceViewPass while it draws scaled space inside the flight camera, where the
// scaled sun is not one of the camera's URP lights. Zero everywhere else (map view, main menu, KSC far LOD),
// where ForwardLit keeps the URP main light. The colour is linear, with the Built-in intensity formula.
float _KSP2ScaledViewActive;
float4 _KSP2ScaledSunDirection;
float4 _KSP2ScaledSunColor;

// 4x4 Bayer matrix of the stock LOD dither, indexed [x & 3][y & 3].
static const float CelestialBodyScaled_Dither[16] =
{
     1.0 / 17.0,  9.0 / 17.0,  3.0 / 17.0, 11.0 / 17.0,
    13.0 / 17.0,  5.0 / 17.0, 15.0 / 17.0,  7.0 / 17.0,
     4.0 / 17.0, 12.0 / 17.0,  2.0 / 17.0, 10.0 / 17.0,
    16.0 / 17.0,  8.0 / 17.0, 14.0 / 17.0,  6.0 / 17.0
};

// Stock screen position: ComputeScreenPos of the clip position.
float4 CelestialBodyScaled_ScreenPos(float4 positionCS)
{
    return float4((positionCS.xy * float2(1.0, _ProjectionParams.x) + positionCS.w) * 0.5, positionCS.zw);
}

// Scaled/local LOD transition stipple: discards where _Transition is below the Bayer threshold.
void CelestialBodyScaled_DitherClip(float4 screenPos)
{
    uint2 cell = uint2((screenPos.xy / screenPos.w) * _ScreenParams.xy) & 3u;
    clip(_Transition - CelestialBodyScaled_Dither[cell.x * 4u + cell.y]);
}

// DepthOnly / DepthNormals with the same dither as the colour pass.
struct CelestialBodyScaledDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
};

struct CelestialBodyScaledDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float4 screenPos  : TEXCOORD0;
    float3 normalWS   : TEXCOORD1;
};

CelestialBodyScaledDepthVaryings CelestialBodyScaled_DepthVertex(CelestialBodyScaledDepthAttributes input)
{
    CelestialBodyScaledDepthVaryings output;
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.screenPos = CelestialBodyScaled_ScreenPos(output.positionCS);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half4 CelestialBodyScaled_DepthFragment(CelestialBodyScaledDepthVaryings input) : SV_Target
{
    CelestialBodyScaled_DitherClip(input.screenPos);
    return input.positionCS.z;
}

half4 CelestialBodyScaled_DepthNormalsFragment(CelestialBodyScaledDepthVaryings input) : SV_Target
{
    CelestialBodyScaled_DitherClip(input.screenPos);
    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
}

#endif // KSP2_CELESTIALBODY_SCALED_COMMON_INCLUDED