// KSP2CelestialBodyLocalPrepass.hlsl
//
// Local-space prepass of CelestialBody_Local (stock passes 13-15, here 5-7). PqsRender
// draws into graph-owned weights and normals, which the surface draw reads back
// as _LocalSpacePrepassTex0..4 at screen UV:
//   SV_Target0..3  per-layer height/slope weights of biome R, G, B, A (LAYERS4)
//   SV_Target4     blended world normal (scaled normal, then subzone, Mid and Large layers, then decals)
// The pass defines CBL_PREPASS_ZONE (zone bit) and one of CBL_DECALS_NONE / CBL_DECALS_PACKED4 /
// CBL_DECALS_INFINITE before including this file. Math from the Redux port Prepass.cginc and Decals.cginc.

#ifndef KSP2_CELESTIALBODY_LOCAL_PREPASS_INCLUDED
#define KSP2_CELESTIALBODY_LOCAL_PREPASS_INCLUDED

struct CBLPrepassVaryings
{
    float4 positionCS   : SV_POSITION;
    float  clipDist     : SV_ClipDistance0;
    float3 uvHeight     : TEXCOORD0;    // xy planet UV, z terrain height
    float3 positionWS   : TEXCOORD1;
    float3 tangentWS    : TEXCOORD2;
    float3 bitangentWS  : TEXCOORD3;
    float3 normalWS     : TEXCOORD4;
    float3 normalOS     : TEXCOORD5;
#if defined(CBL_DECALS_PACKED4)
    nointerpolation uint triangleData : TEXCOORD6;
#endif
};

struct CBLPrepassOutput
{
    float4 tex0   : SV_Target0;
    float4 tex1   : SV_Target1;
    float4 tex2   : SV_Target2;
    float4 tex3   : SV_Target3;
    float4 normal : SV_Target4;
};

CBLPrepassVaryings CBL_PrepassVertex(uint vertexID : SV_VertexID)
{
    QuadMeshData q = CBL_FetchVisibleVertex(vertexID);
    uint flags = CBL_TriangleFlags(vertexID);
    CBLFrame frame = CBL_BuildFrame(q);

    CBLPrepassVaryings o;
    o.positionCS = TransformWorldToHClip(q.position);
    o.clipDist = CBL_ClipDistance(flags, CBL_PREPASS_ZONE);
    o.uvHeight = float3(q.uv, q.height.w);
    o.positionWS = q.position;
    o.tangentWS = frame.tangentWS;
    o.bitangentWS = frame.bitangentWS;
    o.normalWS = frame.normalWS;
    o.normalOS = q.normal;
#if defined(CBL_DECALS_PACKED4)
    o.triangleData = BucketedQuadInfoDecalIndices[vertexID / 3u];
#endif
    return o;
}

// ---------------------------------------------------------------------------
// Height-map gradients and decals
// ---------------------------------------------------------------------------
struct CBLHeightDiffs
{
    float2 large;
    float2 mid;
    float2 sz3;
    float2 sz4;
    float2 aux;
    float2 global;  // REDUX_GRADIENCE only
};

struct CBLHeightAccum
{
    float4 large;
    float4 med;
    float4 sz3;
    float4 sz4;
};

struct CBLHeightSampleCtx
{
    float2 faceUV;
    float2 localUV;
    float  hmNormYBlend;
};

float4 CBL_SampleAndBlendHeight(TEXTURE2D_PARAM(tex, samp), float uvScale, CBLHeightSampleCtx ctx)
{
    float4 f = SAMPLE_TEXTURE2D(tex, samp, ctx.faceUV * uvScale);
    float4 l = SAMPLE_TEXTURE2D(tex, samp, ctx.localUV * uvScale);
    return lerp(l, f, ctx.hmNormYBlend);
}

// One decal applied to the prepass: moves the gradient diffs toward the decal's gradient and bends
// the running normal (quaternion from the geometric normal, or the decal normal outright).
void CBL_ApplyOneDecal(uint slot, float3 localPos, float3 localPosDdx, float3 localPosDdy, float decalFade, float3 geomNormalWS,
                       inout CBLHeightDiffs diffs, inout float3 nrmAccum)
{
    DecalInstance decal = decalInstances[slot];
    float2 decalLocal = CBL_DecalLocal(decal, float4(localPos, 1.0));
    if (!(distance(localPos, decal.position) < SphereRadius && all(abs(decalLocal) < 0.5)))
        return;

    float2 decalUV = saturate(decalLocal + 0.5);
    float decalIndex = (float)decal.index;
    float2 uvDdx, uvDdy;
    CBL_DecalUVGradients(decal, localPosDdx, localPosDdy, uvDdx, uvDdy);

    float4 decalAlbedoSample = SAMPLE_TEXTURE2D_ARRAY_GRAD(_DecalAlbedo, sampler_LinearRepeat, decalUV, decalIndex, uvDdx, uvDdy);
    float decalMaskValue = (decal.UseTextureAlphaMask != 0)
        ? SAMPLE_TEXTURE2D_ARRAY_GRAD(_DecalAlphaMask, sampler_LinearRepeat, decalUV, decalIndex, uvDdx, uvDdy).x
        : 1.0;
    float decalEdgeFade = CBL_DecalEdgeFade(decal, decalUV);
    float decalMaskFinal = (decal.UseTextureHeightmapFade != 0)
        ? decalMaskValue * min(decalEdgeFade + decalEdgeFade, 1.0)
        : decalMaskValue;

    float2 decalAlbedoDiff = decalAlbedoSample.xy - decalAlbedoSample.zw;
    float decalBlendFactor = decalFade * (decal.GradientOpacity * decalMaskFinal);

    // Decal-space normal to world: TBN rows (DecalRight, DecalForward, DecalUp), then PQS to world.
    float3 decalDecNrm = CBL_DecodeDxt5nm(SAMPLE_TEXTURE2D_ARRAY_GRAD(_DecalNormalSAO, sampler_LinearRepeat, decalUV, decalIndex, uvDdx, uvDdy));
    float3 decalTbnNrm = mul(decalDecNrm, float3x3(decal.DecalRight, decal.DecalForward, decal.DecalUp));
    float3 decalWorldNrm = normalize(mul((float3x3)_PQSToWorld, decalTbnNrm));

    // Half-arc quaternion from the geometric normal to the decal normal, applied to the running normal.
    float quatDot1 = dot(geomNormalWS, decalWorldNrm) + 1.0;
    float quatLen = sqrt(max(quatDot1 + quatDot1, 1e-8));
    float3 quatCross = (geomNormalWS.yzx * decalWorldNrm.zxy - decalWorldNrm.yzx * geomNormalWS.zxy) / quatLen;
    float quatW = quatDot1 / quatLen;
    float quatDiag = quatW * quatW - dot(quatCross, quatCross);
    float quatNdotP = dot(quatCross, nrmAccum);
    float3 quatRotated = (quatW + quatW) * cross(quatCross, nrmAccum) + nrmAccum * quatDiag + (quatNdotP + quatNdotP) * quatCross;
    float3 decalDirNrm = (decal.NormalBlend != 0) ? decalWorldNrm : quatRotated;
    float3 decalBlendedNrm = decal.NormalOpacity * (decalMaskFinal * decalDirNrm) * decalFade + nrmAccum;

    diffs.aux = lerp(diffs.aux, decalAlbedoDiff, decalBlendFactor);
    diffs.large = lerp(diffs.large, decalAlbedoDiff, decalBlendFactor);
    diffs.mid = lerp(diffs.mid, decalAlbedoDiff, decalBlendFactor);
    nrmAccum = normalize(decalBlendedNrm);
}

void CBL_ApplyDecals(inout CBLHeightDiffs diffs, inout float3 nrmAccum, float3 localPos, float3 localPosDdx, float3 localPosDdy,
                     float3 geomNormalWS, float cameraDist, uint triangleData)
{
#if defined(DECALS_ENABLED) && defined(CBL_DECALS_PACKED4)
    float decalFade = CBL_DecalFade(cameraDist);
    [unroll]
    for (uint i = 0u; i < 4u; ++i)
    {
        uint slot = (triangleData >> (i * 8u)) & 0xFFu;
        [branch]
        if (slot != 0xFFu)
            CBL_ApplyOneDecal(slot, localPos, localPosDdx, localPosDdy, decalFade, geomNormalWS, diffs, nrmAccum);
    }
#elif defined(DECALS_ENABLED) && defined(CBL_DECALS_INFINITE)
    float decalFade = CBL_DecalFade(cameraDist);
    uint count = (uint)max(decalInstanceCount, 0.0);
    [loop]
    for (uint i = 0u; i < count; ++i)
        CBL_ApplyOneDecal(i, localPos, localPosDdx, localPosDdy, decalFade, geomNormalWS, diffs, nrmAccum);
#endif
}

// ---------------------------------------------------------------------------
// Per-layer weights (LAYERS4)
// ---------------------------------------------------------------------------

// Squared-ramp trapezoid window, p = (center, upRange, downRange, fadeOut).
float CBL_FadeTri(float axis, float4 p)
{
    float hi = p.x + p.y;
    float lo = p.x - p.z;
    float fi = saturate((axis - lo) / max(p.x - lo, 0.001));
    float fo = 1.0 - saturate((axis - hi) / max(p.w, 0.001));
    return step(axis, hi + p.w) * step(hi, axis) * fo * fo
         + step(p.x, axis) * step(axis, hi)
         + fi * fi * step(lo, axis) * step(axis, p.x);
}

// One layer: gate, then height window times slope window.
float CBL_LayerWeight(bool biomeActive, float biomeChan, float gateProduct, float height,
                      float4 heightParams, float heightEnable, float4 slopeParams, float slopeEnable,
                      float4 gradWeights, float tintGw, float tintRw, CBLHeightDiffs diffs)
{
    if (!(biomeActive && biomeChan >= 0.0 && gateProduct > 0.0))
        return 0.0;

    float heightW = lerp(1.0 - heightEnable, 1.0, CBL_FadeTri(height, heightParams));
    float2 grad = diffs.sz4 * tintGw
                + diffs.sz3 * tintRw
                + diffs.mid * gradWeights.z
                + diffs.aux * gradWeights.x
                + diffs.large * gradWeights.y;
#if defined(REDUX_GRADIENCE)
    // Redux encoding: true (dh/du, dh/dv), the slope is the angle of the composed gradient.
    grad += diffs.global;
    float slopeAng = degrees(atan(length(grad)));
#else
    float slopeAng = min(length(grad), 1.0) * 90.0;
#endif
    bool slopeDegenerate = slopeParams.x == 0.0 && slopeParams.y == 0.0 && slopeEnable != 0.0;
    float slopeW = slopeDegenerate ? 0.0 : lerp(1.0 - slopeEnable, 1.0, CBL_FadeTri(slopeAng, slopeParams));
    return heightW * slopeW;
}

float4 CBL_ChannelLayerWeights(bool biomeActive, float4 biomeChan, float4 gateProduct, float height,
                               float4 heightParams1, float4 heightParams2, float4 heightParams3, float4 heightParams4, float4 heightEnable,
                               float4 slopeParams1, float4 slopeParams2, float4 slopeParams3, float4 slopeParams4, float4 slopeEnable,
                               float4 gradWeights1, float4 gradWeights2, float4 gradWeights3, float4 gradWeights4,
                               float4 tintGw4, float4 tintRw4, CBLHeightDiffs diffs)
{
    return float4(
        CBL_LayerWeight(biomeActive, biomeChan.x, gateProduct.x, height, heightParams1, heightEnable.x, slopeParams1, slopeEnable.x, gradWeights1, tintGw4.x, tintRw4.x, diffs),
        CBL_LayerWeight(biomeActive, biomeChan.y, gateProduct.y, height, heightParams2, heightEnable.y, slopeParams2, slopeEnable.y, gradWeights2, tintGw4.y, tintRw4.y, diffs),
        CBL_LayerWeight(biomeActive, biomeChan.z, gateProduct.z, height, heightParams3, heightEnable.z, slopeParams3, slopeEnable.z, gradWeights3, tintGw4.z, tintRw4.z, diffs),
        CBL_LayerWeight(biomeActive, biomeChan.w, gateProduct.w, height, heightParams4, heightEnable.w, slopeParams4, slopeEnable.w, gradWeights4, tintGw4.w, tintRw4.w, diffs));
}

// CI is the channel's index into the _CBL arrays (R G B A = 0 to 3). The subzone tint alphas are subzones G (1) and R (0).
#define CBL_CH_LAYER4(A, CI) A[(CI) * 4], A[(CI) * 4 + 1], A[(CI) * 4 + 2], A[(CI) * 4 + 3]
#define CBL_CH_TINT_ALPHA(CI, S) float4(_CBLSubzoneTint[(CI) * 16 + (S)].w, _CBLSubzoneTint[(CI) * 16 + 4 + (S)].w, \
    _CBLSubzoneTint[(CI) * 16 + 8 + (S)].w, _CBLSubzoneTint[(CI) * 16 + 12 + (S)].w)
#if defined(SUB_ZONES_ENABLED)
    #define CBL_LAYERS4(CI, active) CBL_ChannelLayerWeights( \
        active, _CBLBiome[CI], \
        _CBLEnable[CI] * float4( \
            dot(subzoneWeight, _CBLSubzoneWeight[(CI) * 4]), \
            dot(subzoneWeight, _CBLSubzoneWeight[(CI) * 4 + 1]), \
            dot(subzoneWeight, _CBLSubzoneWeight[(CI) * 4 + 2]), \
            dot(subzoneWeight, _CBLSubzoneWeight[(CI) * 4 + 3])), \
        height, \
        CBL_CH_LAYER4(_CBLBiomeHeightParams, CI), \
        _CBLBiomeHeightEnable[CI], \
        CBL_CH_LAYER4(_CBLBiomeSlopeParams, CI), \
        _CBLBiomeSlopeEnable[CI], \
        CBL_CH_LAYER4(_CBLBiomeGradMapWeights, CI), \
        CBL_CH_TINT_ALPHA(CI, 1), \
        CBL_CH_TINT_ALPHA(CI, 0), \
        diffs)
#else
    #define CBL_LAYERS4(CI, active) CBL_ChannelLayerWeights( \
        active, _CBLBiome[CI], _CBLHeightWeight[CI] * _CBLEnable[CI], height, \
        CBL_CH_LAYER4(_CBLBiomeHeightParams, CI), \
        _CBLBiomeHeightEnable[CI], \
        CBL_CH_LAYER4(_CBLBiomeSlopeParams, CI), \
        _CBLBiomeSlopeEnable[CI], \
        CBL_CH_LAYER4(_CBLBiomeGradMapWeights, CI), \
        (float4)0, (float4)0, \
        diffs)
#endif

// ---------------------------------------------------------------------------
// Fragment
// ---------------------------------------------------------------------------
#if defined(CBL_NORMALS_ONLY)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferCommon.hlsl"
float _PqsNormalsOct;
half4 CBL_PrepassFragment(CBLPrepassVaryings i) : SV_Target0
#else
CBLPrepassOutput CBL_PrepassFragment(CBLPrepassVaryings i)
#endif
{
    CBL_DitherAlphaTest(i.positionCS.xy);
    float2 uv = i.uvHeight.xy;
    float3 worldPos = i.positionWS;
    float3 objectNormal = i.normalOS;
    float cameraDist = distance(_WorldSpaceCameraPos, worldPos);

    CBLFrame frame;
    frame.tangentWS = i.tangentWS;
    frame.bitangentWS = i.bitangentWS;
    frame.normalWS = i.normalWS;

    // Triplanar setup on PQS-space planes for the Mid normals.
    float3 localPos = mul(_PQSToLocal, float4(worldPos, 1.0)).xyz;
    float3 objNorm = normalize(objectNormal);
    float3 triWeightRaw = pow(abs(objNorm), abs(_TriplanarContrast) * TriplanarContrastBoost);
    float3 triWeight = triWeightRaw / max(triWeightRaw.x + triWeightRaw.y + triWeightRaw.z, 0.001);
    float3 triSign = float3(sign(objNorm.xy), objNorm.z >= 0.0 ? 1.0 : -1.0);
    float2 triUVScale = _TriplanarUVScaleOffset.xy * 0.01;
    float2 triUV_YZ = float2(localPos.z, localPos.y) * triUVScale + _TriplanarUVScaleOffset.zw;
    float2 triUV_XZ = float2(localPos.x, localPos.z) * triUVScale + _TriplanarUVScaleOffset.zw;
    float2 triUV_XY = float2(localPos.x, localPos.y) * triUVScale + _TriplanarUVScaleOffset.zw;

    // Planar (Large / subzone) context: cube-face UV and pole-projected local UV.
    float2 uvFace = float2(2.0 * uv.x, uv.y);
    float3 normLocalPos = normalize(localPos);
    float2 normLocalHalf = normLocalPos.xz * 0.5;
    float absY = abs(normLocalPos.y);

    CBLNormalSampleCtx normCtx;
    normCtx.faceUV = uvFace;
    normCtx.faceDdx = ddx_coarse(uvFace);
    normCtx.faceDdy = ddy_coarse(uvFace);
    normCtx.localUV = normLocalHalf;
    normCtx.localDdx = ddx_coarse(normLocalHalf);
    normCtx.localDdy = ddy_coarse(normLocalHalf);
    normCtx.normLocalPos = normLocalPos;
    normCtx.triSignYNorm = sign(normLocalPos.y);
    normCtx.normYBlend = 1.0 - absY * absY * absY;

    // Scaled-space normal, faded from the geometric normal by distance.
    float3 ts = CBL_DecodeDxt5nm(SAMPLE_TEXTURE2D(_NormalScaledTex, sampler_LinearRepeat, uv));
    float3 tbnNormal = frame.tangentWS * ts.x + frame.bitangentWS * ts.y + frame.normalWS * ts.z;
    float normalFade = CBL_RampFade(cameraDist, _NormalScaledFadeParams);
    float3 scaledNormal = lerp(frame.normalWS, normalize(tbnNormal), normalFade);

    float4 biomeMask = SAMPLE_TEXTURE2D(_BiomeMaskTex, sampler_LinearRepeat, uv);
    float4 biomeWeightNorm = biomeMask / max(biomeMask.x + biomeMask.y + biomeMask.z + biomeMask.w, 0.001);
    float4 subzoneWeight = SAMPLE_TEXTURE2D(_SubzoneMaskTex, sampler_LinearRepeat, uv);
    float invBiomeWeightSum = 1.0 / max(biomeWeightNorm.x + biomeWeightNorm.y + biomeWeightNorm.z + biomeWeightNorm.w, 0.001);
    float4 hmBiomeWeight = invBiomeWeightSum * biomeWeightNorm;

    // Redux: a channel with no biome weight skips its normal samples, which BlendLayerNormal weights by zero. The
    // biome mask is exactly zero over wide areas, so whole waves skip together.
    CBLNormalLayer midR = (CBLNormalLayer)0, midG = (CBLNormalLayer)0, midB = (CBLNormalLayer)0, midA = (CBLNormalLayer)0;
    CBLNormalLayer largeR = (CBLNormalLayer)0, largeG = (CBLNormalLayer)0, largeB = (CBLNormalLayer)0, largeA = (CBLNormalLayer)0;
#if defined(SUB_ZONES_ENABLED)
    CBLNormalLayer sz3R = (CBLNormalLayer)0, sz3G = (CBLNormalLayer)0, sz3B = (CBLNormalLayer)0, sz3A = (CBLNormalLayer)0;
    CBLNormalLayer sz4R = (CBLNormalLayer)0, sz4G = (CBLNormalLayer)0, sz4B = (CBLNormalLayer)0, sz4A = (CBLNormalLayer)0;
    #define CBL_SAMPLE_SUBZONE_NORMALS(C, idx) \
        sz3##C = CBL_SampleArrayNormal(TEXTURE2D_ARRAY_ARGS(_SubZonesNormalTextureArray, sampler_LinearRepeat), _Subzone3Normal##C##UVParams, _Subzone3Normal##C##FadeParams, _Subzone3NormalIndices.idx, normCtx, frame, (float3x3)_PQSToWorld); \
        sz4##C = CBL_SampleArrayNormal(TEXTURE2D_ARRAY_ARGS(_SubZonesNormalTextureArray, sampler_LinearRepeat), _Subzone4Normal##C##UVParams, _Subzone4Normal##C##FadeParams, _Subzone4NormalIndices.idx, normCtx, frame, (float3x3)_PQSToWorld);
#else
    #define CBL_SAMPLE_SUBZONE_NORMALS(C, idx)
#endif
    #define CBL_SAMPLE_CHANNEL_NORMALS(C, idx) \
        [branch] \
        if (biomeWeightNorm.idx > 0.0) \
        { \
            mid##C = CBL_SampleMidNormal(TEXTURE2D_ARGS(_MidNormal##C, sampler_LinearRepeat), _MidNormal##C##UVParams, _MidNormal##C##FadeParams, objNorm, objectNormal, triWeight, triSign, triUV_YZ, triUV_XZ, triUV_XY, (float3x3)_PQSToWorld); \
            large##C = CBL_SampleLargeNormal(TEXTURE2D_ARGS(_LargeNormal##C, sampler_LinearRepeat), _LargeNormal##C##UVParams, _LargeNormal##C##FadeParams, normCtx, frame, (float3x3)_PQSToWorld); \
            CBL_SAMPLE_SUBZONE_NORMALS(C, idx) \
        }
    CBL_SAMPLE_CHANNEL_NORMALS(R, x)
    CBL_SAMPLE_CHANNEL_NORMALS(G, y)
    CBL_SAMPLE_CHANNEL_NORMALS(B, z)
    CBL_SAMPLE_CHANNEL_NORMALS(A, w)
    #undef CBL_SAMPLE_CHANNEL_NORMALS
    #undef CBL_SAMPLE_SUBZONE_NORMALS

#if defined(SUB_ZONES_ENABLED)
    // dot(subzone weight, filter) per family and channel. Scales each layer normal and gates the
    // channel's fade so an inactive channel adds nothing to BlendLayerNormal's weight.
    #define CBL_SZ_FILTER(PRE) float4( \
        dot(subzoneWeight, _##PRE##RSubzoneFilter), dot(subzoneWeight, _##PRE##GSubzoneFilter), \
        dot(subzoneWeight, _##PRE##BSubzoneFilter), dot(subzoneWeight, _##PRE##ASubzoneFilter))
    float4 largeSzFilter = CBL_SZ_FILTER(Large);
    float4 midSzFilter = CBL_SZ_FILTER(Mid);
    float4 sz3SzFilter = CBL_SZ_FILTER(Subzone3);
    float4 sz4SzFilter = CBL_SZ_FILTER(Subzone4);
    #undef CBL_SZ_FILTER

    #define CBL_APPLY_SZ(r, szF, c) { r.normal *= (szF); r.fade *= step(0.001, (szF) * (c)); }
    CBL_APPLY_SZ(midR, midSzFilter.x, biomeWeightNorm.x);
    CBL_APPLY_SZ(midG, midSzFilter.y, biomeWeightNorm.y);
    CBL_APPLY_SZ(midB, midSzFilter.z, biomeWeightNorm.z);
    CBL_APPLY_SZ(midA, midSzFilter.w, biomeWeightNorm.w);
    CBL_APPLY_SZ(largeR, largeSzFilter.x, biomeWeightNorm.x);
    CBL_APPLY_SZ(largeG, largeSzFilter.y, biomeWeightNorm.y);
    CBL_APPLY_SZ(largeB, largeSzFilter.z, biomeWeightNorm.z);
    CBL_APPLY_SZ(largeA, largeSzFilter.w, biomeWeightNorm.w);

    CBL_APPLY_SZ(sz3R, sz3SzFilter.x, biomeWeightNorm.x);
    CBL_APPLY_SZ(sz3G, sz3SzFilter.y, biomeWeightNorm.y);
    CBL_APPLY_SZ(sz3B, sz3SzFilter.z, biomeWeightNorm.z);
    CBL_APPLY_SZ(sz3A, sz3SzFilter.w, biomeWeightNorm.w);

    CBL_APPLY_SZ(sz4R, sz4SzFilter.x, biomeWeightNorm.x);
    CBL_APPLY_SZ(sz4G, sz4SzFilter.y, biomeWeightNorm.y);
    CBL_APPLY_SZ(sz4B, sz4SzFilter.z, biomeWeightNorm.z);
    CBL_APPLY_SZ(sz4A, sz4SzFilter.w, biomeWeightNorm.w);
    #undef CBL_APPLY_SZ

    // Subzone blend order: scaled -> Subzone4 -> Subzone3 -> Mid -> Large.
    float3 blendedNorm = CBL_BlendLayerNormal(scaledNormal, sz4R, sz4G, sz4B, sz4A, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
    blendedNorm = CBL_BlendLayerNormal(blendedNorm, sz3R, sz3G, sz3B, sz3A, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
    blendedNorm = CBL_BlendLayerNormal(blendedNorm, midR, midG, midB, midA, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
    blendedNorm = CBL_BlendLayerNormal(blendedNorm, largeR, largeG, largeB, largeA, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
#else
    // Blend order: scaled -> Mid -> Large.
    float3 blendedNorm = CBL_BlendLayerNormal(scaledNormal, midR, midG, midB, midA, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
    blendedNorm = CBL_BlendLayerNormal(blendedNorm, largeR, largeG, largeB, largeA, biomeWeightNorm, frame.normalWS, normalFade, cameraDist);
#endif

    // Height-map gradients, per biome channel weighted by hmBiomeWeight.
    CBLHeightSampleCtx hmCtx;
    hmCtx.faceUV = float2(2.0 * uv.x, uv.y);
    hmCtx.localUV = normLocalPos.xz * 0.5;
    float absY2 = absY * absY;
    hmCtx.hmNormYBlend = 1.0 - absY * absY2 * absY2;

    CBLHeightAccum hm = (CBLHeightAccum)0;
#if defined(SUB_ZONES_ENABLED)
    #define CBL_ACCUM_HEIGHT(C, idx) \
        if (0.001 < hmBiomeWeight.idx) \
        { \
            hm.large += hmBiomeWeight.idx * largeSzFilter.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_LargeGradience##C, sampler_LinearRepeat), _LargeHeightMapUVScales.idx, hmCtx); \
            hm.med += hmBiomeWeight.idx * midSzFilter.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_MidGradience##C, sampler_LinearRepeat), _MediumHeightMapUVScales.idx, hmCtx); \
            hm.sz3 += hmBiomeWeight.idx * sz3SzFilter.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_Subzone3Gradience##C, sampler_LinearRepeat), _Subzone3HeightMapUVScales.idx, hmCtx); \
            hm.sz4 += hmBiomeWeight.idx * sz4SzFilter.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_Subzone4Gradience##C, sampler_LinearRepeat), _Subzone4HeightMapUVScales.idx, hmCtx); \
        }
#else
    #define CBL_ACCUM_HEIGHT(C, idx) \
        if (0.001 < hmBiomeWeight.idx) \
        { \
            hm.large += hmBiomeWeight.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_LargeGradience##C, sampler_LinearRepeat), _LargeHeightMapUVScales.idx, hmCtx); \
            hm.med += hmBiomeWeight.idx * CBL_SampleAndBlendHeight(TEXTURE2D_ARGS(_MidGradience##C, sampler_LinearRepeat), _MediumHeightMapUVScales.idx, hmCtx); \
        }
#endif
    CBL_ACCUM_HEIGHT(R, x)
    CBL_ACCUM_HEIGHT(G, y)
    CBL_ACCUM_HEIGHT(B, z)
    CBL_ACCUM_HEIGHT(A, w)
    #undef CBL_ACCUM_HEIGHT

    CBLHeightDiffs diffs;
#if defined(REDUX_GRADIENCE)
    // Redux two-channel encoding (du, dv) * 0.5 + 0.5.
    diffs.large = (hm.large.xy - 0.5) * 2.0;
    diffs.mid = (hm.med.xy - 0.5) * 2.0;
    diffs.global = (SAMPLE_TEXTURE2D(_GlobalGradienceTex, sampler_LinearRepeat, uv).xy - 0.5) * 2.0;
#else
    diffs.large = hm.large.xy - hm.large.zw;
    diffs.mid = hm.med.xy - hm.med.zw;
    diffs.global = (float2)0;
#endif
#if defined(SUB_ZONES_ENABLED)
    diffs.sz3 = hm.sz3.xy - hm.sz3.zw;
    diffs.sz4 = hm.sz4.xy - hm.sz4.zw;
    // The runtime binds the biome mask to a second slot in the subzone variant.
    diffs.aux = biomeMask.xy - biomeMask.zw;
#else
    diffs.sz3 = (float2)0;
    diffs.sz4 = (float2)0;
    diffs.aux = subzoneWeight.xy - subzoneWeight.zw;
#endif

#if defined(CBL_DECALS_PACKED4)
    uint triangleData = i.triangleData;
#else
    uint triangleData = 0u;
#endif
    CBL_ApplyDecals(diffs, blendedNorm, localPos, ddx(localPos), ddy(localPos), frame.normalWS, cameraDist, triangleData);

    bool4 biomeActive = biomeWeightNorm > 0.0;
    float height = i.uvHeight.z;

#if defined(CBL_NORMALS_ONLY)
    float3 normal = normalize(blendedNorm);
    return half4(_PqsNormalsOct > 0.5 ? PackGBufferNormal(normal) : normal, 0.0);
#else
    CBLPrepassOutput o;
    o.tex0 = CBL_LAYERS4(0, biomeActive.x);
    o.tex1 = CBL_LAYERS4(1, biomeActive.y);
    o.tex2 = CBL_LAYERS4(2, biomeActive.z);
    o.tex3 = CBL_LAYERS4(3, biomeActive.w);
    o.normal = float4(blendedNorm, 1.0);
    return o;
#endif
}

#endif // KSP2_CELESTIALBODY_LOCAL_PREPASS_INCLUDED
