// KSP2Reentry.hlsl
//
// Reentry heating glow on part surfaces (_REENTRYEMISSION_ON), ported from the decompiled
// KSP2 scenery shaders. ReentryMesh feeds these through a MaterialPropertyBlock, so the
// including shader declares them in its UnityPerMaterial block (the property block values
// override the material values):
//   float  _ReentryIntensity;         // heat intensity
//   float  _ReentryEmissivePow;       // emissive multiplier
//   float  _ReentryNoiseScale;        // noise scale
//   float  _ReentryNoiseIntensity;    // noise jitter on the threshold
//   float  _PartGlowNormalEdgeOffset; // heat bias
//   float4 _MetallicMinMax;           // .xy heat band for dielectric / metallic surfaces
//   float4 _ReentryColor;             // glow tint
//   float4 _ReentryHeadingWs;         // world-space velocity direction
// The BRP cbuffer packed _ReentryColor after a scalar, which is why the decompile read .yzw.

#ifndef KSP2_URP_REENTRY_INCLUDED
#define KSP2_URP_REENTRY_INCLUDED

// Rendered by FXReentryOcclusionRenderer: orthographic depth along the velocity vector (R = linear depth).
TEXTURE2D(_ReentryOcclusionMap);    SAMPLER(sampler_ReentryOcclusionMap);
// KSP2.VFX.Reentry.Shaders.ReentryOcclusionData, bound by FXReentryOcclusionRenderer as a structured
// GraphicsBuffer with one element (28 floats, tightly packed).
struct KSP2ReentryOcclusionData
{
    float4 depthVPColumns[4];
    float3 cameraPosWS;
    float4 projectionParams;
    float4 zBufferParams;
    float occlusionMapSize;
};
StructuredBuffer<KSP2ReentryOcclusionData> _OcclusionData;

float3 KSP2_snoise_mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 KSP2_snoise_mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float4 KSP2_snoise_permute(float4 x) { return KSP2_snoise_mod289(((x * 34.0) + 1.0) * x); }
float4 KSP2_snoise_taylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

// 3D simplex noise (Ashima webgl-noise), returns [-1, 1].
float KSP2_SimplexNoise(float3 v)
{
    const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
    const float4 D = float4(0.0, 0.5, 1.0, 2.0);

    float3 i = floor(v + dot(v, C.yyy));
    float3 x0 = v - i + dot(i, C.xxx);
    float3 g = step(x0.yzx, x0.xyz);
    float3 l = 1.0 - g;
    float3 i1 = min(g.xyz, l.zxy);
    float3 i2 = max(g.xyz, l.zxy);
    float3 x1 = x0 - i1 + C.xxx;
    float3 x2 = x0 - i2 + C.yyy;
    float3 x3 = x0 - D.yyy;

    i = KSP2_snoise_mod289(i);
    float4 p = KSP2_snoise_permute(KSP2_snoise_permute(KSP2_snoise_permute(
        i.z + float4(0.0, i1.z, i2.z, 1.0)) + i.y + float4(0.0, i1.y, i2.y, 1.0)) + i.x + float4(0.0, i1.x, i2.x, 1.0));

    float n_ = 1.0 / 7.0;
    float3 ns = n_ * D.wyz - D.xzx;
    float4 j = p - 49.0 * floor(p * ns.z * ns.z);
    float4 x_ = floor(j * ns.z);
    float4 y_ = floor(j - 7.0 * x_);
    float4 gx = x_ * ns.x + ns.yyyy;
    float4 gy = y_ * ns.x + ns.yyyy;
    float4 gh = 1.0 - abs(gx) - abs(gy);
    float4 b0 = float4(gx.xy, gy.xy);
    float4 b1 = float4(gx.zw, gy.zw);
    float4 s0 = floor(b0) * 2.0 + 1.0;
    float4 s1 = floor(b1) * 2.0 + 1.0;
    float4 sh = -step(gh, 0.0);
    float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
    float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
    float3 p0 = float3(a0.xy, gh.x);
    float3 p1 = float3(a0.zw, gh.y);
    float3 p2 = float3(a1.xy, gh.z);
    float3 p3 = float3(a1.zw, gh.w);
    float4 norm = KSP2_snoise_taylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
    p0 *= norm.x; p1 *= norm.y; p2 *= norm.z; p3 *= norm.w;
    float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
    m = m * m;
    return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
}

// Additive reentry glow. vtxNormal is the geometric world normal, uv the main texture UV.
float3 KSP2_ReentryEmission(float2 uv, float3 vtxNormal, float3 positionWS, float metallic)
{
    const float2 poisson[8] =
    {
        float2( 0.00278527,  0.00216140), float2(-0.00109366,  0.00079070),
        float2(-0.00232983, -0.00251180), float2(-0.00261682,  0.00130770),
        float2( 0.00098560,  0.00083960), float2(-0.00026910, -0.00265540),
        float2( 0.00270170, -0.00219685), float2(-0.00269148, -0.00114020),
    };

    float noiseOffset = KSP2_SimplexNoise(float3(uv, _SinTime.w) * _ReentryNoiseScale) * 0.5 + 0.5;
    float heat = saturate(noiseOffset * _ReentryNoiseIntensity + dot(-_ReentryHeadingWs.xyz, vtxNormal) * 0.5 + _PartGlowNormalEdgeOffset);
    float intensity = lerp(_MetallicMinMax.x, _MetallicMinMax.y, metallic) * _ReentryIntensity * heat;

    KSP2ReentryOcclusionData occlusion = _OcclusionData[0];
    float4 clip = occlusion.depthVPColumns[0] * positionWS.x + occlusion.depthVPColumns[1] * positionWS.y
        + occlusion.depthVPColumns[2] * positionWS.z + occlusion.depthVPColumns[3];
    float2 occUV = float2(clip.x, -clip.y) / clip.w * 0.5 + 0.5;
    float nearPlane = occlusion.projectionParams.y;
    float farPlane = occlusion.projectionParams.z;
    float depthRef = saturate((length(occlusion.cameraPosWS - positionWS) - nearPlane) / (farPlane - nearPlane)) - 0.001;

    float visibility = 1.0;
    [unroll] for (int t = 0; t < 8; t++)
    {
        visibility -= (SAMPLE_TEXTURE2D(_ReentryOcclusionMap, sampler_ReentryOcclusionMap, occUV + poisson[t]).x < depthRef) ? 0.125 : 0.0;
    }

    return (intensity * visibility) * _ReentryEmissivePow * _ReentryColor.rgb;
}

#endif // KSP2_URP_REENTRY_INCLUDED