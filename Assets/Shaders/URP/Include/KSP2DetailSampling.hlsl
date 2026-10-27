// KSP2DetailSampling.hlsl
//
// The PQS terrain's surface sampling helpers, shared by CelestialBody_Local and anything that draws ground meant to
// match it, such as the Redux space center ground:
//   - the small detail layers: hex-grid stochastic sampling under ANTI_TILE_QUALITY_ON, the stock colour grade, the
//     metallic and gloss strength decode, and the distance-resample cascade scales and opacities.
//   - the macro normal: the vertex frame, the DXT5nm decode, the Mid (triplanar) and Large (planar) normal layers, and
//     the biome-weighted fold of each family into the running normal.
//   - the terrain's SH ambient.
//   - the distance ramp every fade uses.
// Nothing here reads a terrain uniform: the terrain passes its own, and other users pass theirs.
//
// Needs URP's Core.hlsl, for the texture macros, included first.

#ifndef KSP2_DETAIL_SAMPLING_INCLUDED
#define KSP2_DETAIL_SAMPLING_INCLUDED

// SHEvalLinearL0L1 for CBL_AmbientSH. Its own guard keeps a second include harmless.
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl"

// Linear ramp from p.z to p.w over [p.x, p.x + p.y].
float CBL_RampFade(float t, float4 p)
{
    return lerp(p.z, p.w, saturate((t - p.x) / max(p.y, 0.01)));
}

// The two strongest of a hex cell's three taps.
struct CBLTileHash
{
    float2 uv0;
    float2 uv1;
    float2 weights;
};

// A hex cell's random UV offset.
float2 CBL_CellOffset(float2 cell)
{
    return float2(frac(sin(dot(float2(127.1, 311.7), cell)) * 43758.5468), frac(sin(dot(float2(269.5, 183.3), cell)) * 43758.5468));
}

// stochasticScale is the terrain material's _StochasticScale.
CBLTileHash CBL_TileHash(float2 baseUV, float stochasticScale)
{
    CBLTileHash r;
#if defined(ANTI_TILE_QUALITY_ON)
    float2 scaled = (stochasticScale * 3.464) * baseUV;
    float gridY = dot(float2(-0.57735026, 1.15470052), scaled);
    // Stock took the cell indices through int and uint and back to float. Floor and +1 are exact in float below 2^24,
    // so the cells are the same, without the conversions.
    float cellX = floor(scaled.x);
    float cellY = floor(gridY);
    float fracX = scaled.x - cellX;
    float fracY = gridY - cellY;
    float triTest = 1.0 - fracX - fracY;
    bool inUpper = triTest >= 0.0;
    float2 c0 = inUpper ? float2(cellX, cellY) : float2(cellX + 1.0, cellY + 1.0);
    float2 c1 = inUpper ? float2(cellX, cellY + 1.0) : float2(cellX + 1.0, cellY);
    float2 c2 = inUpper ? float2(cellX + 1.0, cellY) : float2(cellX, cellY + 1.0);
    float3 w = max(float3(inUpper ? triTest : -triTest, inUpper ? fracY : 1.0 - fracY, inUpper ? fracX : 1.0 - fracX), 0.0);
    // Redux: stock blends all three taps. Keeping the two strongest saves a fetch per detail sample, and sharpening
    // the weights to the 4th power first keeps the dropped tap's share small. The taps are chosen before hashing, so
    // the dropped cell is never hashed.
    w *= w;
    w *= w;
    if (w.x < w.y && w.x < w.z)
    {
        c0 = c2;
        w.x = w.z;
    }
    else if (w.y < w.z)
    {
        c1 = c2;
        w.y = w.z;
    }
    r.uv0 = CBL_CellOffset(c0) + baseUV;
    r.uv1 = CBL_CellOffset(c1) + baseUV;
    r.weights = w.xy / max(w.x + w.y, 1e-6);
#else
    r.uv0 = baseUV;
    r.uv1 = baseUV;
    r.weights = float2(1.0, 0.0);
#endif
    return r;
}

// Detail array sample with explicit gradients (the un-hashed UV's), so it is valid inside the
// per-triangle and per-layer branches.
float4 CBL_SampleDetail(TEXTURE2D_ARRAY_PARAM(tex, samp), CBLTileHash h, float index, float2 uvDdx, float2 uvDdy)
{
#if defined(ANTI_TILE_QUALITY_ON)
    // Both taps are fetched unconditionally so their latency overlaps. Stock's per-tap branches serialized it, which
    // cost more than the fetches themselves.
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, samp, h.uv0, index, uvDdx, uvDdy) * h.weights.x
         + SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, samp, h.uv1, index, uvDdx, uvDdy) * h.weights.y;
#else
    return SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, samp, h.uv0, index, uvDdx, uvDdy);
#endif
}

// Grading around mid grey: brightness, contrast, then saturation.
float3 CBL_GradeColor(float3 albedo, float4 tint, float brightness, float contrast, float saturation)
{
    float3 graded = (albedo * tint.xyz + brightness - 0.21763764) * contrast + 0.21763764;
    float lum = dot(graded, float3(0.2126729, 0.7151522, 0.072175));
    return saturation * (graded - lum) + lum;
}

void CBL_LayerStrengths(float metallicStrength, float glossStrength, out float2 metScaleOffset, out float2 glossScaleOffset)
{
    // A strength of 15 or more switches from multiply to override: value = (strength - 15) * 0.2.
    bool metOver = metallicStrength >= 15.0;
    metScaleOffset = metOver ? float2(1.0, (metallicStrength - 15.0) * 0.2) : float2(metallicStrength, 0.0);
    bool glossOver = glossStrength >= 15.0;
    glossScaleOffset = glossOver ? float2(1.0, (glossStrength - 15.0) * 0.2) : float2(glossStrength, 0.0);
}

// The pixel's distance-resample band as near and far scale indexes for CBL_CascadeScale: the last band whose cascadeT
// is past 0.001 picks the near scale, and the far scale is the band below it until the fade completes.
void CBL_CascadeBand(float4 cascadeT, out uint nearIndex, out uint farIndex)
{
    nearIndex = 0u;
    farIndex = 0u;
    [unroll]
    for (uint j = 0u; j < 4u; j++)
    {
        if (cascadeT[j] > 0.001)
        {
            nearIndex = j + 1u;
            farIndex = (cascadeT[j] >= 1.0) ? j + 1u : j;
        }
    }
}

// The layer's distance-resample tier: the highest band index with a scale of its own.
uint CBL_CascadeTier(float distResampleMax)
{
    return (3.0 < distResampleMax) ? 4u : (2.0 < distResampleMax) ? 3u : (1.0 < distResampleMax) ? 2u
         : (0.0 < distResampleMax) ? 1u : 0u;
}

// The cascade UV scale at a band index for the layer's distance-resample tier. Index 0 is unscaled, and index i is
// _DistanceResampleUVScales[i - 1], clamped to the tier's last scale. A tier of 0 leaves every index unscaled.
//
// The scale is picked with a dot product, so the compiler loads all four scales at once into uniform registers.
// Selected with conditionals, it loaded the scale with a chain of predicated constant loads at every use.
float CBL_CascadeScale(float distResampleMax, float4 resampleUVScales, uint index)
{
    uint i = min(index, CBL_CascadeTier(distResampleMax));
    float4 pick = float4(i == 1u, i == 2u, i == 3u, i == 4u);
    return (i == 0u) ? 1.0 : dot(pick, resampleUVScales);
}

// Cascade UV scales for the layer's distance-resample tier, and the near / far scale for this pixel.
// resampleUVScales is the terrain material's _DistanceResampleUVScales.
void CBL_CascadeScales(float distResampleMax, float4 cascadeT, float4 resampleUVScales, out float nearScale,
                       out float farScale)
{
    uint nearIndex, farIndex;
    CBL_CascadeBand(cascadeT, nearIndex, farIndex);
    nearScale = CBL_CascadeScale(distResampleMax, resampleUVScales, nearIndex);
    farScale = CBL_CascadeScale(distResampleMax, resampleUVScales, farIndex);
}


// World-space frame of a quad-mesh vertex. The Redux port calls the geometric normal
// "worldTangent" and the tangent "worldNormal"; the names here follow what the vectors are.
struct CBLFrame
{
    float3 tangentWS;       // _PQSToWorld * tangent.xyz
    float3 bitangentWS;
    float3 normalWS;        // _PQSToWorld * normal, the geometric surface normal
};

// DXT5nm / BC5 normal decode (red times alpha), matching UnpackNormalmapRGorAG.
float3 CBL_DecodeDxt5nm(float4 s)
{
    float2 xy = float2(s.w * s.x, s.y) * 2.0 - 1.0;
    return float3(xy, sqrt(saturate(1.0 - dot(xy, xy))));
}

struct CBLNormalLayer
{
    float3 normal;
    float4 fade;
};

// Planar (Large / subzone) sampling context: cube-face UV and pole-projected local UV with their
// derivatives, plus the polar blend.
struct CBLNormalSampleCtx
{
    float2 faceUV;
    float2 faceDdx;
    float2 faceDdy;
    float2 localUV;
    float2 localDdx;
    float2 localDdy;
    float3 normLocalPos;
    float  triSignYNorm;
    float  normYBlend;
};

CBLNormalLayer CBL_DecodeAndReorientNormal(float4 faceSample, float4 localSample, CBLNormalSampleCtx ctx, float4 fadeParams,
                                           CBLFrame frame, float3x3 pqsToWorld)
{
    float3 faceN = CBL_DecodeDxt5nm(faceSample);
    float3 tbnNorm = normalize(frame.tangentWS * faceN.x + frame.bitangentWS * faceN.y + frame.normalWS * faceN.z);

    float3 localN = CBL_DecodeDxt5nm(localSample);
    float absYP1 = abs(ctx.normLocalPos.y) + 1.0;
    float reorientDot = dot(float3(ctx.normLocalPos.x, ctx.normLocalPos.z, absYP1), float3(-localN.x, -localN.y, localN.z));
    float3 reorientVec = float3(
        ctx.normLocalPos.x + localN.x + (ctx.normLocalPos.x * reorientDot) / absYP1,
        ctx.normLocalPos.y + ctx.triSignYNorm * (reorientDot - localN.z),
        ctx.normLocalPos.z + localN.y + (ctx.normLocalPos.z * reorientDot) / absYP1);
    float3 reorientUnit = dot(reorientVec, reorientVec) < 0.0001 ? ctx.normLocalPos : normalize(reorientVec);
    float3 worldNorm = normalize(mul(pqsToWorld, reorientUnit));

    CBLNormalLayer r;
    r.normal = lerp(worldNorm, tbnNorm, ctx.normYBlend);
    r.fade = fadeParams;
    return r;
}

CBLNormalLayer CBL_SampleLargeNormal(TEXTURE2D_PARAM(tex, samp), float4 uvParams, float4 fadeParams,
                                     CBLNormalSampleCtx ctx, CBLFrame frame, float3x3 pqsToWorld)
{
    float4 faceSample = SAMPLE_TEXTURE2D_GRAD(tex, samp, ctx.faceUV * uvParams.xy + uvParams.zw,
                                              ctx.faceDdx * uvParams.xy, ctx.faceDdy * uvParams.xy);
    float4 localSample = SAMPLE_TEXTURE2D_GRAD(tex, samp, ctx.localUV * uvParams.xy + uvParams.zw,
                                               ctx.localDdx * uvParams.xy, ctx.localDdy * uvParams.xy);
    return CBL_DecodeAndReorientNormal(faceSample, localSample, ctx, fadeParams, frame, pqsToWorld);
}

CBLNormalLayer CBL_SampleArrayNormal(TEXTURE2D_ARRAY_PARAM(tex, samp), float4 uvParams, float4 fadeParams, float arrayIndex,
                                     CBLNormalSampleCtx ctx, CBLFrame frame, float3x3 pqsToWorld)
{
    float4 faceSample = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, samp, ctx.faceUV * uvParams.xy + uvParams.zw, arrayIndex,
                                                    ctx.faceDdx * uvParams.xy, ctx.faceDdy * uvParams.xy);
    float4 localSample = SAMPLE_TEXTURE2D_ARRAY_GRAD(tex, samp, ctx.localUV * uvParams.xy + uvParams.zw, arrayIndex,
                                                     ctx.localDdx * uvParams.xy, ctx.localDdy * uvParams.xy);
    return CBL_DecodeAndReorientNormal(faceSample, localSample, ctx, fadeParams, frame, pqsToWorld);
}

// Triplanar Mid normal with RNM-style reorientation on the three PQS-space planes.
CBLNormalLayer CBL_SampleMidNormal(TEXTURE2D_PARAM(tex, samp), float4 uvParams, float4 fadeParams,
                                   float3 objNorm, float3 objectNormal, float3 triWeight, float3 triSign,
                                   float2 triUV_YZ, float2 triUV_XZ, float2 triUV_XY, float3x3 pqsToWorld)
{
    float3 yz_n = CBL_DecodeDxt5nm(SAMPLE_TEXTURE2D(tex, samp, triUV_YZ * uvParams.xy + uvParams.zw));
    float3 xz_n = CBL_DecodeDxt5nm(SAMPLE_TEXTURE2D(tex, samp, triUV_XZ * uvParams.xy + uvParams.zw));
    float3 xy_n = CBL_DecodeDxt5nm(SAMPLE_TEXTURE2D(tex, samp, triUV_XY * uvParams.xy + uvParams.zw));
    float3 absP1 = abs(objNorm) + 1.0;
    float yz_dot = dot(float3(objNorm.z, objNorm.y, absP1.x), float3(-yz_n.x, -yz_n.y, yz_n.z));
    float xz_dot = dot(float3(objNorm.x, objNorm.z, absP1.y), float3(-xz_n.x, -xz_n.y, xz_n.z));
    float xy_dot = dot(float3(objNorm.x, objNorm.y, absP1.z), float3(-xy_n.x, -xy_n.y, xy_n.z));
    float3 reorient = float3(
        objectNormal.x + triWeight.z * (xy_n.x + (xy_dot * objNorm.x) / absP1.z) + triWeight.x * triSign.x * (yz_dot - yz_n.z) + triWeight.y * (xz_n.x + (xz_dot * objNorm.x) / absP1.y),
        objectNormal.y + triWeight.z * (xy_n.y + (xy_dot * objNorm.y) / absP1.z) + triWeight.x * (yz_n.y + (objNorm.y * yz_dot) / absP1.x) + triWeight.y * triSign.y * (xz_dot - xz_n.z),
        objectNormal.z + triWeight.z * triSign.z * (xy_dot - xy_n.z) + triWeight.x * (yz_n.x + (objNorm.z * yz_dot) / absP1.x) + triWeight.y * (xz_n.y + (xz_dot * objNorm.z) / absP1.y));
    CBLNormalLayer r;
    r.normal = mul(pqsToWorld, normalize(reorient));
    r.fade = fadeParams;
    return r;
}

// Rotates v by the shortest-arc rotation that maps "from" onto "to".
float3 CBL_RotateByHalfArcQuat(float3 from, float3 to, float3 v)
{
    // Degenerate when "to" is opposite "from": leave v unrotated instead of dividing by zero.
    float quatDot = dot(from, to) + 1.0;
    float quatLen = sqrt(max(2.0 * quatDot, 1e-8));
    float3 qXYZ = cross(from, to) / quatLen;
    float qW = quatDot / quatLen;
    float diag = qW * qW - dot(qXYZ, qXYZ);
    float3 rotated = v * diag + 2.0 * qW * cross(qXYZ, v) + 2.0 * dot(qXYZ, v) * qXYZ;
    return quatDot < 0.0001 ? v : rotated;
}

// Folds four biome-channel normal layers into prevNorm: shortest-arc rotation from the geometric
// normal to the fade-weighted channel sum, scaled by (1 - normalScaleFade) and the summed weight.
float3 CBL_BlendLayerNormal(float3 prevNorm, CBLNormalLayer r, CBLNormalLayer g, CBLNormalLayer b, CBLNormalLayer a,
                            float4 biomeWeightNorm, float3 geomNormalWS, float normalScaleFade, float cameraDist)
{
    float4 fades = float4(CBL_RampFade(cameraDist, r.fade), CBL_RampFade(cameraDist, g.fade),
                          CBL_RampFade(cameraDist, b.fade), CBL_RampFade(cameraDist, a.fade));
    float4 w = biomeWeightNorm * fades;
    float blendWeight = saturate(w.x + w.y + w.z + w.w);
    float3 blendNorm = w.x * r.normal + w.y * g.normal + w.z * b.normal + w.w * a.normal;
    // No change when the layers carry no weight or their sum collapses to zero.
    float3 result = prevNorm;
    if (0.001 < blendWeight && dot(blendNorm, blendNorm) >= 0.0001)
    {
        float3 rotated = CBL_RotateByHalfArcQuat(geomNormalWS, blendNorm, prevNorm);
        result = lerp(prevNorm, rotated, (1.0 - normalScaleFade) * blendWeight);
    }
    return result;
}

// The albedo and normal opacity of the distance-resample cascade at a normalized distance. rangesNeg, rangesPos,
// albedoOpacity and normalOpacity are the terrain material's _DistanceResample* vectors of the same names.
float2 CBL_ResampleOpacity(float normDist, float4 rangesNeg, float4 rangesPos, float4 albedoOpacity, float4 normalOpacity)
{
    float4 fadeT = (normDist - rangesNeg.wzyx) / (rangesPos.wzyx - rangesNeg.wzyx);
    if (0.001 < fadeT.x)
    {
        float t = saturate(fadeT.x);
        return float2(lerp(albedoOpacity.z, albedoOpacity.w, t), lerp(normalOpacity.z, normalOpacity.w, t));
    }
    if (0.001 < fadeT.y)
    {
        float t = saturate(fadeT.y);
        return float2(lerp(albedoOpacity.y, albedoOpacity.z, t), lerp(normalOpacity.y, normalOpacity.z, t));
    }
    if (0.001 < fadeT.z)
    {
        float t = saturate(fadeT.z);
        return float2(lerp(albedoOpacity.x, albedoOpacity.y, t), lerp(normalOpacity.x, normalOpacity.y, t));
    }
    if (0.001 < fadeT.w)
    {
        float t = saturate(fadeT.w);
        return float2(lerp(1.0, albedoOpacity.x, t), lerp(1.0, normalOpacity.x, t));
    }
    return float2(1.0, 1.0);
}

// SH L0 + L1 at the shaded normal plus the vertex L2 term, clamped (stock EvaluateSphericalHarmonics
// without the light probe proxy volume path).
float3 CBL_AmbientSH(float3 normalWS, float3 shL2)
{
    return max(SHEvalLinearL0L1(normalWS, unity_SHAr, unity_SHAg, unity_SHAb) + shL2, 0.0);
}

#endif
