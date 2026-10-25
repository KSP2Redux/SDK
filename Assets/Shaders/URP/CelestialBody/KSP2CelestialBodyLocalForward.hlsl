// KSP2CelestialBodyLocalForward.hlsl
//
// ForwardLit pass of CelestialBody_Local. The stock shader filled the deferred GBuffer in up to
// three draws per triangle: a base pass (stock passes 1-3, picked per triangle by its decal zone
// bit) and one additive pass per biome channel the triangle touches (passes 4-7 single-axis
// triplanar on flat triangles, passes 8-11 3-axis triplanar on the rest), blended as
//   RT0 diffuse + occlusion    One One          (UNORM, clamped per blend)
//   RT1 specular + smoothness  Zero One, One One (specular stays the base pass's)
//   RT2 normal + projRatio     SrcAlpha OneMinusSrcAlpha, alpha = the biome's projRatio
//   RT3 ambient + emission     One One
// Unity's deferred passes then added the direct lights (BRDF1) and the reflection probe specular.
// This pass runs the base and the biome contributions in the same order, accumulates the same
// values with the same blend arithmetic, and lights the result once with the KSP2 BRDF helpers:
// KSP2_DirectLighting for the URP main and Forward+ lights, the specular half of
// KSP2_IndirectLighting for the reflection, and the accumulated RT3 for ambient and emission
// (the terrain bakes its own SH and spherical-gaussian ambient there, so KSP2_Shade's ambient
// term is not used).

#ifndef KSP2_CELESTIALBODY_LOCAL_FORWARD_INCLUDED
#define KSP2_CELESTIALBODY_LOCAL_FORWARD_INCLUDED

#include "../Include/KSP2Lighting.hlsl"
#include "../Include/KSP2DetailSampling.hlsl"
#if defined(KSP2_GBUFFER_PASS)
    #include "../Include/KSP2GBuffer.hlsl"
#endif

struct CBLForwardVaryings
{
    float4 positionCS   : SV_POSITION;
    float  clipDist     : SV_ClipDistance0;
    float2 uv           : TEXCOORD0;
    float3 positionWS   : TEXCOORD1;
    float3 normalWS     : TEXCOORD2;    // geometric normal
    float3 normalOS     : TEXCOORD3;    // PQS-space normal, drives the triplanar weights
    float3 shL2         : TEXCOORD4;    // SH L2 band at the geometric normal, as the stock vertex stage did
    float3 screenPos    : TEXCOORD5;    // prepass target UV = xy / z
    nointerpolation uint2 triangleInfo : TEXCOORD6;  // x bucketing flags, y packed decal slots
};

CBLForwardVaryings CBL_ForwardVertex(uint vertexID : SV_VertexID)
{
    QuadMeshData q = CBL_FetchVisibleVertex(vertexID);
    uint flags = CBL_TriangleFlags(vertexID);
    CBLFrame frame = CBL_BuildFrame(q);

    CBLForwardVaryings o;
    o.positionCS = TransformWorldToHClip(q.position);
    o.screenPos = CBL_PrepassScreenPos(o.positionCS);
    o.clipDist = CBL_ClipDistance(flags, CBL_ZONE_MASK);
    o.uv = q.uv;
    o.positionWS = q.position;
    o.normalWS = frame.normalWS;
    o.normalOS = q.normal;
    o.shL2 = SHEvalLinearL2(frame.normalWS, unity_SHBr, unity_SHBg, unity_SHBb, unity_SHC);
    o.triangleInfo = uint2(flags, BucketedQuadInfoDecalIndices[vertexID / 3u]);
    return o;
}

//
// GBuffer emulation
//
struct CBLGBuffer
{
    float3 diffuse;
    float  occlusion;
    float3 specular;
    float  smoothness;
    float3 normalEncoded;   // n * 0.5 + 0.5, as stored in RT2
    float3 emission;        // RT3: ambient + emission
};

//
// Base pass (stock passes 1-3): scaled-space maps, prepass normal, decal albedo
//
// Stock BlendDecal: lerps the tinted decal albedo into the running albedo in passes 2 and 3 only.
// The Redux deferred port left this step out.
float3 CBL_BlendDecalAlbedo(uint slot, bool checkActive, float3 localPos, float3 localPosDdx, float3 localPosDdy,
                            float3 albedo, float decalFade)
{
    DecalInstance decal = decalInstances[slot];
    if (checkActive && decal.useDecalTexturing != 1)
        return albedo;
    float2 decalLocal = CBL_DecalLocal(decal, float4(localPos, 1.0));
    if (length(localPos - decal.position) >= SphereRadius || abs(decalLocal.x) >= 0.5 || abs(decalLocal.y) >= 0.5)
        return albedo;

    float2 decalUV = saturate(decalLocal + 0.5);
    float decalIndex = (float)decal.index;
    float2 uvDdx, uvDdy;
    CBL_DecalUVGradients(decal, localPosDdx, localPosDdy, uvDdx, uvDdy);
    float4 decalColor = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DecalAlbedo, sampler_LinearRepeat, decalUV, decalIndex, uvDdx, uvDdy);
    float alphaMask = (decal.UseTextureAlphaMask != 0)
        ? SAMPLE_TEXTURE2D_ARRAY_GRAD(_DecalAlphaMask, sampler_LinearRepeat, decalUV, decalIndex, uvDdx, uvDdy).x
        : 1.0;
    float alpha = (decal.UseTextureHeightmapFade != 0)
        ? alphaMask * min(2.0 * CBL_DecalEdgeFade(decal, decalUV), 1.0)
        : alphaMask;
    return lerp(albedo, decalColor.rgb * decal.TintColor.rgb, decalFade * decal.AlbedoOpacity * alpha);
}

CBLGBuffer CBL_BasePass(CBLForwardVaryings i, float2 prepassUV, float cameraDist, float3 localPos, float4 biomeMask)
{
    float2 uv = i.uv;
    uint flags = i.triangleInfo.x;

    float3 normalSample = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex4, sampler_LinearClamp, prepassUV, 0).xyz;
    float4 albedoSample = SAMPLE_TEXTURE2D(_AlbedoScaledTex, sampler_LinearRepeat, uv);
    float4 packedSample = SAMPLE_TEXTURE2D(_PackedScaledTex, sampler_LinearRepeat, uv);
    float4 emissionSample = SAMPLE_TEXTURE2D(_EmissionScaledTex, sampler_LinearRepeat, uv);

    // Redux fix: some stock packed maps ship without mips, so average their smoothness over the
    // pixel footprint to keep distant highlights from aliasing.
    uint packedWidth, packedHeight, packedMipCount;
    _PackedScaledTex.GetDimensions(0, packedWidth, packedHeight, packedMipCount);
    float scaledSmoothness = packedSample.w;
    float2 uvDx = ddx(uv);
    float2 uvDy = ddy(uv);
    [branch]
    if (packedMipCount <= 1)
    {
        scaledSmoothness = 0.0;
        [unroll]
        for (int y = 0; y < 4; ++y)
        {
            [unroll]
            for (int x = 0; x < 4; ++x)
            {
                float2 footprintOffset = uvDx * ((x + 0.5) * 0.25 - 0.5) + uvDy * ((y + 0.5) * 0.25 - 0.5);
                scaledSmoothness += SAMPLE_TEXTURE2D_GRAD(_PackedScaledTex, sampler_LinearRepeat, uv + footprintOffset, uvDx, uvDy).w;
            }
        }
        scaledSmoothness *= 0.0625;
    }

    float albedoFade = CBL_RampFade(cameraDist, _AlbedoScaledFadeParams);
    float packedFade = CBL_RampFade(cameraDist, _PackedScaledFadeParams);
    float emissionFade = CBL_RampFade(cameraDist, _EmissionScaledFadeParams);

    float2 resampleOp = CBL_ResampleOpacity(min(cameraDist * 2e-5, 1.0), _DistanceResampleFadeRangesNeg, _DistanceResampleFadeRangesPos,
        _DistanceResampleAlbedoOpacity, _DistanceResampleNormalOpacity);
    float albedoResample = resampleOp.x;
    float normalResample = resampleOp.y;

    float3 scaledAlbedo = albedoFade * albedoSample.xyz;
    float3 albedo = lerp(scaledAlbedo, 0.0, albedoResample * (1.0 - albedoFade));

    // Prepass normal, shortened toward the geometry at range (direction unchanged).
    float normalFade = normalResample * (1.0 - CBL_RampFade(cameraDist, _NormalScaledFadeParams));
    float3 worldNormal = normalSample - 0.5 * normalFade * normalSample;

    // Packed map: R metallic, G occlusion, B emission, A smoothness.
    float packedResample = normalResample * (1.0 - packedFade);
    float packedScale = packedFade * (1.0 - packedResample);
    float metallic = packedScale * packedSample.x;
    float occlusion = packedScale * packedSample.y;
    float smoothness = packedScale * scaledSmoothness;

    // Decal albedo on triangles bucketed with one to four decals (packed slots) or more (full list).
    float3 localPosDdx = ddx(localPos);
    float3 localPosDdy = ddy(localPos);
    [branch]
    if ((flags & (CBL_ZONE_4_DECALS | CBL_ZONE_INF_DECALS)) != 0u)
    {
        float decalFade = CBL_DecalFade(cameraDist);
        if ((flags & CBL_ZONE_4_DECALS) != 0u)
        {
            uint triangleData = i.triangleInfo.y;
            [unroll]
            for (uint d = 0u; d < 4u; d++)
            {
                uint slot = (triangleData >> (d * 8u)) & 0xFFu;
                [branch]
                if (slot != 0xFFu)
                    albedo = CBL_BlendDecalAlbedo(slot, false, localPos, localPosDdx, localPosDdy, albedo, decalFade);
            }
        }
        else
        {
            uint count = (uint)max(decalInstanceCount, 0.0);
            [loop]
            for (uint d = 0u; d < count; d++)
                albedo = CBL_BlendDecalAlbedo(d, true, localPos, localPosDdx, localPosDdy, albedo, decalFade);
        }
    }

    // Ambient: SH at the prepass normal plus the vertex L2 term, plus the atmosphere SG lobes.
    float projRatio = biomeMask.x / max(dot(biomeMask, 1.0), 0.001);
    float3 shLight = CBL_AmbientSH(worldNormal, i.shL2);
    float3 atmosSpec = KSP2_SphericalGaussianAmbient(worldNormal);
    float3 emissionContrib = (emissionFade * normalResample) * (emissionFade * (emissionSample.xyz * _EmissionScale));
    float3 lighting = emissionContrib * (packedFade * packedSample.z)
                    + (shLight * (occlusion + (1.0 - projRatio) * occlusion) + atmosSpec) * albedo;

    CBLGBuffer gb;
    gb.diffuse = saturate(albedo * (0.96 - 0.96 * metallic));
    gb.occlusion = saturate(occlusion);
    gb.specular = saturate(lerp(0.04, albedo, metallic));
    gb.smoothness = saturate(smoothness);
    gb.normalEncoded = saturate(worldNormal * 0.5 + 0.5);
    gb.emission = lighting;
    return gb;
}

//
// Biome material: one channel's _Small* uniforms, with the subzone-weighted tint, brightness and
// height weight already resolved for this pixel.
//
struct CBLBiomeMaterial
{
    float4 biome;               // per-layer detail array slice, -1 = unused
    float4 enable;
    float4 heightWeight;        // gate and height-blend weight (enable folded in)
    float4 weightSoftness;
    float4 uvScale;
    float4 uvOffset;
    float4 tint[4];
    float4 brightness;
    float4 contrast;
    float4 saturation;
    float4 normalStrength;
    float4 metallicStrength;
    float4 glossStrength;
    float4 aoStrength;
    float4 distanceResampleMax;
};

#if defined(SUB_ZONES_ENABLED)
    // Subzone mode replaces the scalar tint, brightness and height weight with blends weighted by
    // the subzone mask and drops the tint alpha factor on the height alpha.
    #define CBL_BIOME_TINT_BRIGHTNESS(CH) \
        float4 wm1 = subzoneMask * _SmallSubzoneWeight##CH##1; \
        float4 wm2 = subzoneMask * _SmallSubzoneWeight##CH##2; \
        float4 wm3 = subzoneMask * _SmallSubzoneWeight##CH##3; \
        float4 wm4 = subzoneMask * _SmallSubzoneWeight##CH##4; \
        m.tint[0] = float4((wm1.x * _SmallSubzoneTint##CH##1_R + wm1.y * _SmallSubzoneTint##CH##1_G + wm1.z * _SmallSubzoneTint##CH##1_B + wm1.w * _SmallSubzoneTint##CH##1_A).rgb, 1.0); \
        m.tint[1] = float4((wm2.x * _SmallSubzoneTint##CH##2_R + wm2.y * _SmallSubzoneTint##CH##2_G + wm2.z * _SmallSubzoneTint##CH##2_B + wm2.w * _SmallSubzoneTint##CH##2_A).rgb, 1.0); \
        m.tint[2] = float4((wm3.x * _SmallSubzoneTint##CH##3_R + wm3.y * _SmallSubzoneTint##CH##3_G + wm3.z * _SmallSubzoneTint##CH##3_B + wm3.w * _SmallSubzoneTint##CH##3_A).rgb, 1.0); \
        m.tint[3] = float4((wm4.x * _SmallSubzoneTint##CH##4_R + wm4.y * _SmallSubzoneTint##CH##4_G + wm4.z * _SmallSubzoneTint##CH##4_B + wm4.w * _SmallSubzoneTint##CH##4_A).rgb, 1.0); \
        m.brightness = float4(dot(_SmallSubzoneBrightness##CH##1, wm1), dot(_SmallSubzoneBrightness##CH##2, wm2), \
                              dot(_SmallSubzoneBrightness##CH##3, wm3), dot(_SmallSubzoneBrightness##CH##4, wm4)); \
        m.heightWeight = _SmallEnable##CH * float4(dot(_SmallSubzoneWeight##CH##1, subzoneMask), dot(_SmallSubzoneWeight##CH##2, subzoneMask), \
                                                  dot(_SmallSubzoneWeight##CH##3, subzoneMask), dot(_SmallSubzoneWeight##CH##4, subzoneMask));
#else
    #define CBL_BIOME_TINT_BRIGHTNESS(CH) \
        m.tint[0] = _SmallTint##CH##1; \
        m.tint[1] = _SmallTint##CH##2; \
        m.tint[2] = _SmallTint##CH##3; \
        m.tint[3] = _SmallTint##CH##4; \
        m.brightness = _SmallBrightness##CH; \
        m.heightWeight = _SmallHeightWeight##CH * _SmallEnable##CH;
#endif

#define CBL_BIOME_MATERIAL_FN(CH) \
    CBLBiomeMaterial CBL_BiomeMaterial##CH(float4 subzoneMask) \
    { \
        CBLBiomeMaterial m; \
        m.biome = _SmallBiome##CH; \
        m.enable = _SmallEnable##CH; \
        m.weightSoftness = _SmallWeightSoftness##CH; \
        m.uvScale = _SmallUVScale##CH; \
        m.uvOffset = _SmallUVOffset##CH; \
        m.contrast = _SmallContrast##CH; \
        m.saturation = _SmallSaturation##CH; \
        m.normalStrength = _SmallNormalStrength##CH; \
        m.metallicStrength = _SmallMetallicStrength##CH; \
        m.glossStrength = _SmallGlossStrength##CH; \
        m.aoStrength = _SmallAOStrength##CH; \
        m.distanceResampleMax = _SmallDistanceResampleMax##CH; \
        CBL_BIOME_TINT_BRIGHTNESS(CH) \
        return m; \
    }

CBL_BIOME_MATERIAL_FN(R)
CBL_BIOME_MATERIAL_FN(G)
CBL_BIOME_MATERIAL_FN(B)
CBL_BIOME_MATERIAL_FN(A)

CBLBiomeMaterial CBL_BiomeMaterial(uint channel, float4 subzoneMask)
{
    CBLBiomeMaterial m;
    [branch]
    if (channel == 0u)
        m = CBL_BiomeMaterialR(subzoneMask);
    else if (channel == 1u)
        m = CBL_BiomeMaterialG(subzoneMask);
    else if (channel == 2u)
        m = CBL_BiomeMaterialB(subzoneMask);
    else
        m = CBL_BiomeMaterialA(subzoneMask);
    return m;
}

//
// Per-pixel inputs shared by every biome channel
//
struct CBLBiomePixel
{
    float3 triN;
    float3 triWeight;
    float3 triSign;
    bool   triNzPositive;
    float2 baseUV[3];           // planes X (zy), Y (xz), Z (xy). The single-axis path uses Y
    float2 baseUVDdx[3];
    float2 baseUVDdy[3];
    float4 cascadeT;
    float  alphaToHeightFade;
    float  uvResampleOp;
    float  albedoFade;
    float  normalFade;
    float  packedFade;
    float3 prepassNormal;
    float3 geomNormalWS;
    float4 subzoneMask;
    float3 shL2;
};

// One layer's samples. "near" and "far" follow the Redux port: near* values come from the far
// cascade UV and pair with the far height-blend weights.
struct CBLLayerResult
{
    float3 nearColor;
    float3 farColor;
    float3 nearNormal;
    float3 farNormal;
    float  nearHeightAlpha;
    float  farHeightAlpha;
    float  nearMetallic;
    float  farMetallic;
    float  nearGloss;
    float  farGloss;
    float  nearAO;
    float  farAO;
    bool   cascadesDiffer;  // false when both cascades sampled the same texels, so near* equals far*
};

// Single-axis (Y plane) triplanar layer, stock passes 4-7.
float3 CBL_ReorientSingleAxis(float4 packed, float3 triN, float3 triWeight, float3 triSign, bool triNzPositive)
{
    float tsX = packed.w * 2.0 - 1.0;
    float tsY = packed.y * 2.0 - 1.0;
    float tsZ = sqrt(1.0 - min(tsX * tsX + tsY * tsY, 1.0));
    float3 absN1 = abs(triN) + 1.0;

    float dotZY = triN.z + triN.y;
    float3 xBlend = triWeight.x * float3(triSign.x * dotZY, ((dotZY * triN.y) / absN1.x) - 1.0, ((dotZY * triN.z) / absN1.x) - 1.0);
    float yDot = dot(float3(triN.x, triN.z, absN1.y), float3(-tsX, -tsY, tsZ));
    float dotXY = triN.x + triN.y;
    float3 zBlend = float3(((dotXY * triN.x) / absN1.z) - 1.0, ((dotXY * triN.y) / absN1.z) - 1.0, (triNzPositive ? 1.0 : -1.0) * dotXY);
    float3 yBlend = float3(tsX + (yDot * triN.x) / absN1.y, triSign.y * (yDot - tsZ), tsY + (yDot * triN.z) / absN1.y);
    return normalize(triN + triWeight.z * zBlend + triWeight.y * yBlend + xBlend);
}

CBLLayerResult CBL_ProcessLayerSingleAxis(CBLBiomeMaterial mat, uint layer, CBLBiomePixel px)
{
    float nearScale, farScale;
    CBL_CascadeScales(mat.distanceResampleMax[layer], px.cascadeT, _DistanceResampleUVScales, nearScale, farScale);
    float uvScale = mat.uvScale[layer];
    float uvOffset = mat.uvOffset[layer];
    float index = mat.biome[layer];
    float4 tint = mat.tint[layer];
    float2 metSO, glossSO;
    CBL_LayerStrengths(mat.metallicStrength[layer], mat.glossStrength[layer], metSO, glossSO);
    // A zero metallic scale multiplies the metal texel away, so the layer skips its fetch.
    bool sampleMetal = metSO.x != 0.0;

    float2 nearUV = px.baseUV[1] * (nearScale * uvScale) + uvOffset;
    float2 nearDdx = px.baseUVDdx[1] * (nearScale * uvScale), nearDdy = px.baseUVDdy[1] * (nearScale * uvScale);
    CBLTileHash nearHash = CBL_TileHash(nearUV, _StochasticScale);
    float4 nearAlbedo = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallAlbedoArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy);
    float4 nearPacked = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallNormalArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy);
    float nearMetal = 0.0;
    [branch]
    if (sampleMetal)
        nearMetal = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallMetalArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy).x;

    float wy = px.triWeight.y;
    float brightness = mat.brightness[layer];
    float contrast = mat.contrast[layer];
    float saturation = mat.saturation[layer];
    float normalStrength = mat.normalStrength[layer];
    float aoStrength = mat.aoStrength[layer];

    CBLLayerResult r;
    r.farColor = CBL_GradeColor(wy * nearAlbedo.xyz, tint, brightness, contrast, saturation);
    r.farNormal = mul((float3x3)TriplanarToWorld, CBL_ReorientSingleAxis(nearPacked, px.triN, px.triWeight, px.triSign, px.triNzPositive)) * normalStrength;
    r.farHeightAlpha = (wy * nearAlbedo.w) * tint.w;
    r.farMetallic = (wy * nearMetal) * metSO.x + metSO.y;
    r.farGloss = (wy * nearPacked.x) * glossSO.x + glossSO.y;
    // FLT_MIN, not 0: pow(0, 0) is exp2(0 * -inf) = NaN on AMD, and a zero AO strength must leave the layer unoccluded.
    r.farAO = pow(max(wy * nearPacked.z, FLT_MIN), aoStrength);

    // Outside a cascade transition both cascades use the same scale, so the far cascade would fetch the same
    // texels and shade them to the same values. Most terrain from altitude is outside one.
    r.cascadesDiffer = farScale != nearScale;
    r.nearColor = r.farColor;
    r.nearNormal = r.farNormal;
    r.nearHeightAlpha = r.farHeightAlpha;
    r.nearMetallic = r.farMetallic;
    r.nearGloss = r.farGloss;
    r.nearAO = r.farAO;
    [branch]
    if (r.cascadesDiffer)
    {
        float2 farUV = px.baseUV[1] * (farScale * uvScale) + uvOffset;
        float2 farDdx = px.baseUVDdx[1] * (farScale * uvScale), farDdy = px.baseUVDdy[1] * (farScale * uvScale);
        CBLTileHash farHash = CBL_TileHash(farUV, _StochasticScale);
        float4 farAlbedo = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallAlbedoArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy);
        float4 farPacked = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallNormalArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy);
        float farMetal = 0.0;
        [branch]
        if (sampleMetal)
            farMetal = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallMetalArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy).x;
        r.nearColor = CBL_GradeColor(wy * farAlbedo.xyz, tint, brightness, contrast, saturation);
        r.nearNormal = mul((float3x3)TriplanarToWorld, CBL_ReorientSingleAxis(farPacked, px.triN, px.triWeight, px.triSign, px.triNzPositive)) * normalStrength;
        r.nearHeightAlpha = (wy * farAlbedo.w) * tint.w;
        r.nearMetallic = (wy * farMetal) * metSO.x + metSO.y;
        r.nearGloss = (wy * farPacked.x) * glossSO.x + glossSO.y;
        r.nearAO = pow(max(wy * farPacked.z, FLT_MIN), aoStrength);
    }
    return r;
}

// 3-axis triplanar layer, stock passes 8-11.
float3 CBL_ReorientThreeAxis(float2 wyX, float2 wyY, float2 wyZ, float3 triN, float3 triWeight, float3 triSign, bool triNzPositive)
{
    float3 tsX, tsY, tsZ;
    tsX.xy = wyX * 2.0 - 1.0;
    tsX.z = sqrt(1.0 - min(dot(tsX.xy, tsX.xy), 1.0));
    tsY.xy = wyY * 2.0 - 1.0;
    tsY.z = sqrt(1.0 - min(dot(tsY.xy, tsY.xy), 1.0));
    tsZ.xy = wyZ * 2.0 - 1.0;
    tsZ.z = sqrt(1.0 - min(dot(tsZ.xy, tsZ.xy), 1.0));

    float3 absN = abs(triN) + 1.0;
    float xDot = dot(float3(triN.z, triN.y, absN.x), float3(-tsX.x, -tsX.y, tsX.z));
    float yDot = dot(float3(triN.x, triN.z, absN.y), float3(-tsY.x, -tsY.y, tsY.z));
    float zDot = dot(float3(triN.x, triN.y, absN.z), float3(-tsZ.x, -tsZ.y, tsZ.z));
    float zSign = triNzPositive ? 1.0 : -1.0;

    float3 blended;
    blended.x = triN.x + triWeight.z * (tsZ.x + (zDot * triN.x / absN.z)) + triWeight.x * triSign.x * (-tsX.z + xDot) + triWeight.y * (tsY.x + (yDot * triN.x / absN.y));
    blended.y = triN.y + triWeight.z * (tsZ.y + (zDot * triN.y / absN.z)) + triWeight.x * (tsX.y + (xDot * triN.y / absN.x)) + triWeight.y * (triSign.y * (-tsY.z + yDot));
    blended.z = triN.z + triWeight.z * zSign * (-tsZ.z + zDot) + triWeight.x * (tsX.x + (xDot * triN.z / absN.x)) + triWeight.y * (tsY.y + (yDot * triN.z / absN.y));
    return normalize(blended);
}

CBLLayerResult CBL_ProcessLayerThreeAxis(CBLBiomeMaterial mat, uint layer, CBLBiomePixel px)
{
    float nearScale, farScale;
    CBL_CascadeScales(mat.distanceResampleMax[layer], px.cascadeT, _DistanceResampleUVScales, nearScale, farScale);
    float uvScale = mat.uvScale[layer];
    float uvOffset = mat.uvOffset[layer];
    float index = mat.biome[layer];
    float4 tint = mat.tint[layer];
    float2 metSO, glossSO;
    CBL_LayerStrengths(mat.metallicStrength[layer], mat.glossStrength[layer], metSO, glossSO);
    // A zero metallic scale multiplies the metal texels away, so the layer skips their fetches.
    bool sampleMetal = metSO.x != 0.0;

    float3 albRGBNear = 0, albRGBFar = 0;
    float albANear = 0, albAFar = 0;
    float nrmXNear = 0, nrmXFar = 0;
    float nrmZNear = 0, nrmZFar = 0;
    float metNear = 0, metFar = 0;
    float2 wyNear[3], wyFar[3];

    // Outside a cascade transition both cascades use the same scale, so the far cascade would fetch the same
    // texels and shade them to the same values. Most terrain from altitude is outside one.
    bool sampleFar = farScale != nearScale;
    [unroll]
    for (uint p = 0u; p < 3u; p++)
    {
        wyNear[p] = 0.0;
        wyFar[p] = 0.0;
        float w = px.triWeight[p];
        [branch]
        if (w > 0.001)
        {
            float2 nearUV = px.baseUV[p] * (nearScale * uvScale) + uvOffset;
            float2 nearDdx = px.baseUVDdx[p] * (nearScale * uvScale), nearDdy = px.baseUVDdy[p] * (nearScale * uvScale);
            CBLTileHash nearHash = CBL_TileHash(nearUV, _StochasticScale);
            float4 albN = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallAlbedoArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy);
            float4 nrmN = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallNormalArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy);
            float mN = 0.0;
            [branch]
            if (sampleMetal)
                mN = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallMetalArray, sampler_LinearRepeat), nearHash, index, nearDdx, nearDdy).x;
            albRGBNear += w * albN.xyz;
            albANear += w * albN.w;
            nrmXNear += w * nrmN.x;
            nrmZNear += w * nrmN.z;
            metNear += w * mN;
            wyNear[p] = float2(nrmN.w, nrmN.y);

            [branch]
            if (sampleFar)
            {
                float2 farUV = px.baseUV[p] * (farScale * uvScale) + uvOffset;
                float2 farDdx = px.baseUVDdx[p] * (farScale * uvScale), farDdy = px.baseUVDdy[p] * (farScale * uvScale);
                CBLTileHash farHash = CBL_TileHash(farUV, _StochasticScale);
                float4 albF = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallAlbedoArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy);
                float4 nrmF = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallNormalArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy);
                float mF = 0.0;
                [branch]
                if (sampleMetal)
                    mF = CBL_SampleDetail(TEXTURE2D_ARRAY_ARGS(_SmallMetalArray, sampler_LinearRepeat), farHash, index, farDdx, farDdy).x;
                albRGBFar += w * albF.xyz;
                albAFar += w * albF.w;
                nrmXFar += w * nrmF.x;
                nrmZFar += w * nrmF.z;
                metFar += w * mF;
                wyFar[p] = float2(nrmF.w, nrmF.y);
            }
        }
    }
    if (!sampleFar)
    {
        albRGBFar = albRGBNear;
        albAFar = albANear;
        nrmXFar = nrmXNear;
        nrmZFar = nrmZNear;
        metFar = metNear;
        wyFar[0] = wyNear[0];
        wyFar[1] = wyNear[1];
        wyFar[2] = wyNear[2];
    }

    float brightness = mat.brightness[layer];
    float contrast = mat.contrast[layer];
    float saturation = mat.saturation[layer];
    float normalStrength = mat.normalStrength[layer];
    float aoStrength = mat.aoStrength[layer];

    CBLLayerResult r;
    float3 reorientedNear = CBL_ReorientThreeAxis(wyNear[0], wyNear[1], wyNear[2], px.triN, px.triWeight, px.triSign, px.triNzPositive);
    r.farColor = CBL_GradeColor(albRGBNear, tint, brightness, contrast, saturation);
    r.farNormal = mul((float3x3)TriplanarToWorld, reorientedNear) * normalStrength;
    r.farHeightAlpha = albANear * tint.w;
    r.farMetallic = metNear * metSO.x + metSO.y;
    r.farGloss = nrmXNear * glossSO.x + glossSO.y;
    // FLT_MIN, not 0: pow(0, 0) is exp2(0 * -inf) = NaN on AMD, and a zero AO strength must leave the layer unoccluded.
    r.farAO = pow(max(nrmZNear, FLT_MIN), aoStrength);

    r.cascadesDiffer = sampleFar;
    r.nearColor = r.farColor;
    r.nearNormal = r.farNormal;
    r.nearHeightAlpha = r.farHeightAlpha;
    r.nearMetallic = r.farMetallic;
    r.nearGloss = r.farGloss;
    r.nearAO = r.farAO;
    [branch]
    if (r.cascadesDiffer)
    {
        float3 reorientedFar = CBL_ReorientThreeAxis(wyFar[0], wyFar[1], wyFar[2], px.triN, px.triWeight, px.triSign, px.triNzPositive);
        r.nearColor = CBL_GradeColor(albRGBFar, tint, brightness, contrast, saturation);
        r.nearNormal = mul((float3x3)TriplanarToWorld, reorientedFar) * normalStrength;
        r.nearHeightAlpha = albAFar * tint.w;
        r.nearMetallic = metFar * metSO.x + metSO.y;
        r.nearGloss = nrmXFar * glossSO.x + glossSO.y;
        r.nearAO = pow(max(nrmZFar, FLT_MIN), aoStrength);
    }
    return r;
}

// One biome channel's additive contribution (one stock additive pass), blended into gb.
void CBL_AccumulateBiome(uint channel, bool threeAxis, float4 layerWeight, float projRatio, CBLBiomePixel px,
                         inout CBLGBuffer gb)
{
    CBLBiomeMaterial mat = CBL_BiomeMaterial(channel, px.subzoneMask);
    bool biomeActive = projRatio > 0.0;

    // Unrolled so layers[] and the per-layer uniforms index statically. As a dynamic loop FXC kept layers[] in an
    // indexable temp array.
    CBLLayerResult layers[4];
    [unroll]
    for (uint l = 0u; l < 4u; l++)
    {
        CBLLayerResult r = (CBLLayerResult)0;
        [branch]
        if (mat.biome[l] >= 0.0 && biomeActive && mat.heightWeight[l] > 0.0)
        {
            [branch]
            if (threeAxis)
                r = CBL_ProcessLayerThreeAxis(mat, l, px);
            else
                r = CBL_ProcessLayerSingleAxis(mat, l, px);
        }
        layers[l] = r;
    }

    // Height blend between the four layers.
    float4 heightWeight = mat.heightWeight;
    float4 enabledWeight = layerWeight * mat.enable;
    float4 blendInput = layerWeight * heightWeight;
    float4 normalizedBlend = blendInput / max(dot(blendInput, 1.0), 0.001);
    float4 soft = max(mat.weightSoftness, 0.001);
    float4 farHeightAlpha = float4(layers[0].farHeightAlpha, layers[1].farHeightAlpha, layers[2].farHeightAlpha, layers[3].farHeightAlpha);
    float4 heightBlendNear = saturate(pow(max(farHeightAlpha * enabledWeight + heightWeight - 0.5, 0.0), soft) - 0.5);
    float4 hbNear = heightWeight * heightBlendNear;
    float thresholdNear = _HeightblendFactor.x - max(max(hbNear.x, hbNear.y), max(hbNear.z, hbNear.w));
    float4 blendNear = max(heightBlendNear * heightWeight + thresholdNear, 0.0);
    float4 wNear = blendNear / max(dot(blendNear, 1.0), 0.001);

    float3 blendedNearColor = 0, nearNormalized = 0, blendedNearNormal = 0;
    float blendedNearGloss = 0, blendedNearAO = 0, blendedNearMet = 0;
    [unroll]
    for (uint k = 0u; k < 4u; k++)
    {
        blendedNearColor += layers[k].farColor * wNear[k];
        nearNormalized += layers[k].farColor * normalizedBlend[k];
        blendedNearNormal += layers[k].farNormal * wNear[k];
        blendedNearGloss += layers[k].farGloss * wNear[k];
        blendedNearAO += layers[k].farAO * wNear[k];
        blendedNearMet += layers[k].farMetallic * wNear[k];
    }

    // The far cascade's blend only differs when a layer sampled the cascades apart.
    float3 blendedFarColor = blendedNearColor, farNormalized = nearNormalized, blendedFarNormal = blendedNearNormal;
    float blendedFarGloss = blendedNearGloss, blendedFarAO = blendedNearAO, blendedFarMet = blendedNearMet;
    [branch]
    if (layers[0].cascadesDiffer || layers[1].cascadesDiffer || layers[2].cascadesDiffer || layers[3].cascadesDiffer)
    {
        float4 nearHeightAlpha = float4(layers[0].nearHeightAlpha, layers[1].nearHeightAlpha, layers[2].nearHeightAlpha, layers[3].nearHeightAlpha);
        float4 heightBlendFar = saturate(pow(max(nearHeightAlpha * enabledWeight + heightWeight - 0.5, 0.0), soft) - 0.5);
        float4 hbFar = heightWeight * heightBlendFar;
        float thresholdFar = _HeightblendFactor.x - max(max(hbFar.x, hbFar.y), max(hbFar.z, hbFar.w));
        float4 blendFar = max(heightBlendFar * heightWeight + thresholdFar, 0.0);
        float4 wFar = blendFar / max(dot(blendFar, 1.0), 0.001);

        blendedFarColor = 0;
        farNormalized = 0;
        blendedFarNormal = 0;
        blendedFarGloss = 0;
        blendedFarAO = 0;
        blendedFarMet = 0;
        [unroll]
        for (uint j = 0u; j < 4u; j++)
        {
            blendedFarColor += layers[j].nearColor * wFar[j];
            farNormalized += layers[j].nearColor * normalizedBlend[j];
            blendedFarNormal += layers[j].nearNormal * wFar[j];
            blendedFarGloss += layers[j].nearGloss * wFar[j];
            blendedFarAO += layers[j].nearAO * wFar[j];
            blendedFarMet += layers[j].nearMetallic * wFar[j];
        }
    }

    // Alpha-to-height fade between the height blend and the plain weight blend, then the
    // distance-resample lerp between the far and near cascade.
    float3 farComposed = lerp(blendedFarColor, farNormalized, px.alphaToHeightFade);
    float3 nearComposed = lerp(blendedNearColor, nearNormalized, px.alphaToHeightFade);
    float3 finalColor = projRatio * lerp(farComposed, nearComposed, px.uvResampleOp);
    float3 blendedNormal = projRatio * lerp(blendedFarNormal, blendedNearNormal, px.uvResampleOp);

    // Shortest-arc rotation from the geometric normal to the blended layer normal, applied to the prepass normal.
    float3 rotated = CBL_RotateByHalfArcQuat(px.geomNormalWS, blendedNormal, px.prepassNormal);
    float3 finalNormal = lerp(px.prepassNormal, rotated, px.normalFade);

    float finalSmoothness = projRatio * lerp(blendedFarGloss, blendedNearGloss, px.uvResampleOp) * px.packedFade;
    float finalOcclusion = projRatio * lerp(blendedFarAO, blendedNearAO, px.uvResampleOp) * px.packedFade;
    float finalMetallic = projRatio * lerp(blendedFarMet, blendedNearMet, px.uvResampleOp) * px.packedFade;

    float3 albedoOut = finalColor * px.albedoFade;
    float3 diffuseOut = (0.96 - 0.96 * finalMetallic) * albedoOut;
    float occlusionTerm = finalOcclusion + (1.0 - projRatio) * finalOcclusion;
    float3 litColor = (CBL_AmbientSH(finalNormal, px.shL2) * occlusionTerm + KSP2_SphericalGaussianAmbient(finalNormal)) * albedoOut;

    // The stock blend state, with the UNORM targets clamping each step.
    float alpha = saturate(projRatio);
    gb.diffuse = saturate(gb.diffuse + saturate(diffuseOut));
    gb.occlusion = saturate(gb.occlusion + saturate(finalOcclusion));
    gb.smoothness = saturate(gb.smoothness + saturate(finalSmoothness));
    gb.normalEncoded = saturate(saturate(finalNormal * 0.5 + 0.5) * alpha + gb.normalEncoded * (1.0 - alpha));
    gb.emission += litColor;
}

//
// Fragment
//
KSP2Surface CBL_Surface(CBLForwardVaryings i, out float3 normalWS, out float3 viewDirWS)
{
    CBL_DitherAlphaTest(i.positionCS.xy);

    float4 biomeMask = SAMPLE_TEXTURE2D(_BiomeMaskTex, sampler_LinearRepeat, i.uv);

#if defined(DEBUG_OUTPUT_BIOME_COLOR)
    // Unlit biome mask view: R red, G green, B blue, A yellow.
    float4 maskNorm = biomeMask / max(dot(biomeMask, 1.0), 0.001);
    float3 debugColor = maskNorm.x * float3(1.0, 0.0, 0.0) + maskNorm.y * float3(0.0, 1.0, 0.0)
                      + maskNorm.z * float3(0.0, 0.0, 1.0) + maskNorm.w * float3(1.0, 1.0, 0.0);
    normalWS = normalize(i.normalWS);
    viewDirWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
    KSP2Surface surface = (KSP2Surface)0;
    surface.emission = debugColor;
    surface.alpha = 1.0;
    return surface;
#else
    float3 positionWS = i.positionWS;
    float2 prepassUV = i.screenPos.xy / i.screenPos.z;      // PQS prepass targets
    float cameraDist = distance(_WorldSpaceCameraPos, positionWS);
    float3 localPos = mul(_PQSToLocal, float4(positionWS, 1.0)).xyz;
    uint flags = i.triangleInfo.x;

    CBLGBuffer gb = CBL_BasePass(i, prepassUV, cameraDist, localPos, biomeMask);

    // Per-pixel biome inputs. The triplanar position comes from the world position through one combined matrix, as
    // stock's compiled shader took it. Through the PQS-space position first, every pixel was rounded at the body's
    // radius, 3 cm on Laythe and 6 cm on Kerbin, and the detail textures, tiled every metre or so, showed those steps as
    // blocks up close. The combined translation is rounded once for all pixels and the per-pixel terms stay small. The
    // height axis is measured from the body's radius, so the side projections on cliffs keep that precision too.
    CBLBiomePixel px;
    float4x4 worldToTriplanar = mul(TriplanarBasis, _PQSToLocal);
    worldToTriplanar._m13 -= SphereRadius;
    float3 triplanarLocalPos = mul(worldToTriplanar, float4(positionWS, 1.0)).xyz;
    px.triN = normalize(mul((float3x3)TriplanarBasis, normalize(i.normalOS)));
    float3 triWeightRaw = pow(abs(px.triN), _TriplanarContrast * TriplanarContrastBoost);
    px.triWeight = triWeightRaw / max(triWeightRaw.x + triWeightRaw.y + triWeightRaw.z, 0.001);
    px.triSign = sign(px.triN);
    px.triNzPositive = px.triN.z >= 0.0;
    float2 triUVScale = _TriplanarUVScaleOffset.xy * 0.01;
    px.baseUV[0] = triplanarLocalPos.zy * triUVScale + _TriplanarUVScaleOffset.zw;
    px.baseUV[1] = triplanarLocalPos.xz * triUVScale + _TriplanarUVScaleOffset.zw;
    px.baseUV[2] = triplanarLocalPos.xy * triUVScale + _TriplanarUVScaleOffset.zw;
    [unroll]
    for (uint p = 0u; p < 3u; p++)
    {
        px.baseUVDdx[p] = ddx(px.baseUV[p]);
        px.baseUVDdy[p] = ddy(px.baseUV[p]);
    }

    px.alphaToHeightFade = CBL_RampFade(cameraDist, _AlphaToHeightFadeParams);
    float normalizedDist = min(cameraDist * 2.0e-05, 1.0);
    px.cascadeT = (normalizedDist - _DistanceResampleFadeRangesNeg) / (_DistanceResampleFadeRangesPos - _DistanceResampleFadeRangesNeg);
    float4 albedoOpLo = float4(1.0, _DistanceResampleAlbedoOpacity.xyz);
    float4 normalOpLo = float4(1.0, _DistanceResampleNormalOpacity.xyz);
    px.uvResampleOp = 1.0;
    float resampleAlbedoOp = 1.0;
    float normalResampleOp = 1.0;
    [unroll]
    for (int k = 0; k < 4; k++)
    {
        if (px.cascadeT[k] > 0.001)
        {
            float t = saturate(px.cascadeT[k]);
            px.uvResampleOp = t;
            resampleAlbedoOp = lerp(albedoOpLo[k], _DistanceResampleAlbedoOpacity[k], t);
            normalResampleOp = lerp(normalOpLo[k], _DistanceResampleNormalOpacity[k], t);
        }
    }
    px.albedoFade = resampleAlbedoOp * (1.0 - CBL_RampFade(cameraDist, _AlbedoScaledFadeParams));
    px.normalFade = normalResampleOp * (1.0 - CBL_RampFade(cameraDist, _NormalScaledFadeParams));
    px.packedFade = normalResampleOp * (1.0 - CBL_RampFade(cameraDist, _PackedScaledFadeParams));
    px.prepassNormal = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex4, sampler_LinearClamp, prepassUV, 0).xyz;
    px.geomNormalWS = i.normalWS;
#if defined(SUB_ZONES_ENABLED)
    px.subzoneMask = SAMPLE_TEXTURE2D(_SubzoneMaskTex, sampler_LinearRepeat, i.uv);
#else
    px.subzoneMask = 0.0;
#endif
    px.shL2 = i.shL2;

    // Additive biome passes in stock pass order: flat R G B A (single-axis), then non-flat R G B A
    // (3-axis). A pass runs on a triangle in its zone and bucket that touches its biome channel.
    uint biomeBits = ((flags & CBL_ZONE_MASK) != 0u) ? (flags & CBL_DebugBiomeMask() & CBL_BIOME_BITS) : 0u;
    uint bucket = flags & CBL_DebugTriplanarBucketing();
    float projSum = max(dot(biomeMask, 1.0), 0.001);
    [loop]
    for (uint passIndex = 0u; passIndex < 8u; passIndex++)
    {
        uint channel = passIndex & 3u;
        bool threeAxis = passIndex >= 4u;
        uint bucketBit = threeAxis ? CBL_BUCKET_NON_FLAT : CBL_BUCKET_FLAT;
        [branch]
        if ((biomeBits & (1u << channel)) != 0u && (bucket & bucketBit) != 0u)
        {
            float4 layerWeight;
            float projRatio;
            [branch]
            if (channel == 0u)
            {
                layerWeight = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex0, sampler_LinearClamp, prepassUV, 0);
                projRatio = biomeMask.x / projSum;
            }
            else if (channel == 1u)
            {
                layerWeight = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex1, sampler_LinearClamp, prepassUV, 0);
                projRatio = biomeMask.y / projSum;
            }
            else if (channel == 2u)
            {
                layerWeight = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex2, sampler_LinearClamp, prepassUV, 0);
                projRatio = biomeMask.z / projSum;
            }
            else
            {
                layerWeight = SAMPLE_TEXTURE2D_LOD(_LocalSpacePrepassTex3, sampler_LinearClamp, prepassUV, 0);
                projRatio = biomeMask.w / projSum;
            }
            CBL_AccumulateBiome(channel, threeAxis, layerWeight, projRatio, px, gb);
        }
    }

    // Light the accumulated GBuffer: direct lights + reflection specular + RT3 (ambient, emission).
    normalWS = normalize(gb.normalEncoded * 2.0 - 1.0);
    viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    float specularStrength = max(max(gb.specular.r, gb.specular.g), gb.specular.b);

    KSP2Surface surface;
    surface.albedo = gb.diffuse;
    surface.diffuseAlbedo = gb.diffuse;
    surface.F0 = gb.specular;
    surface.metallic = (specularStrength - 0.04) / 0.96;    // gives oneMinusReflectivity = 1 - max(F0), as the deferred reflections did
    surface.smoothness = gb.smoothness;
    surface.occlusion = gb.occlusion;
    surface.emission = gb.emission;
    surface.alpha = 1.0;

    return surface;
#endif
}

float3 CBL_IndirectAndEmission(CBLForwardVaryings i, float3 normalWS, float3 viewDirWS, KSP2Surface surface)
{
#if defined(DEBUG_OUTPUT_BIOME_COLOR)
    return surface.emission;
#else
    float2 urpScreenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    float3 ambientDiffuse, envSpecular;
    KSP2_IndirectLighting(i.positionWS, normalWS, viewDirWS, surface, urpScreenUV, ambientDiffuse, envSpecular);
    return envSpecular + surface.emission;
#endif
}

half4 CBL_ForwardFragment(CBLForwardVaryings i) : SV_Target
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = CBL_Surface(i, normalWS, viewDirWS);
#if defined(DEBUG_OUTPUT_BIOME_COLOR)
    return half4(CBL_IndirectAndEmission(i, normalWS, viewDirWS, surface), 1.0);
#else
    float2 urpScreenUV = GetNormalizedScreenSpaceUV(i.positionCS);
    float3 direct = KSP2_DirectLighting(i.positionWS, i.positionCS, normalWS, viewDirWS, surface, urpScreenUV);
    return half4(direct + CBL_IndirectAndEmission(i, normalWS, viewDirWS, surface), 1.0);
#endif
}

#if defined(KSP2_GBUFFER_PASS)
// The terrain's environment specular is KSP2_IndirectLighting's, so the screen-space reflections may replace it.
GBufferFragOutput CBL_GBufferFragment(CBLForwardVaryings i)
{
    float3 normalWS, viewDirWS;
    KSP2Surface surface = CBL_Surface(i, normalWS, viewDirWS);
    return KSP2_GBufferOutputWithIndirect(surface, i.positionCS, normalWS,
        CBL_IndirectAndEmission(i, normalWS, viewDirWS, surface), kKSP2MaterialFlagEnvironmentReflections);
}
#endif

#endif // KSP2_CELESTIALBODY_LOCAL_FORWARD_INCLUDED