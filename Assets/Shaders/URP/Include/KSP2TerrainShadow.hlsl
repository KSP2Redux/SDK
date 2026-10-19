// KSP2TerrainShadow.hlsl
//
// The shadow the SOI body's PQS terrain casts from the sun, at any distance around the camera: crater lips over crater
// floors, ridges over valleys, mountains over a vessel. Redux.Planets.TerrainShadowCascades renders the terrain alone
// into sun-facing cascades (a few kilometres, tens of kilometres and the visible horizon), keeps them in the body's
// frame and refreshes them in turn. URP's main light shadow map keeps the parts and scenery near the camera. Stock had
// no terrain shadow at all (its terrain caster never got its PQS matrix).
//
//   _KSP2TerrainShadowMap0 to 2       the cascades, nearest first, D32 shadow maps
//   _KSP2TerrainShadowMatrices[3]     world to shadow coordinates for this frame (the body's motion folded in)
//   _KSP2TerrainShadowParams          x cascades in use (0 off), y texel size in shadow coordinates
//   _KSP2TerrainShadowNormalOffsets   receiver normal offset per cascade in metres (xyz)
//
// Scaled-space shaders define KSP2_NO_TERRAIN_SHADOW: their positions are not in the local scene's space.

#ifndef KSP2_TERRAIN_SHADOW_INCLUDED
#define KSP2_TERRAIN_SHADOW_INCLUDED

#define KSP2_TERRAIN_SHADOW_CASCADES 3

// Sampled with URP's sampler_LinearClampCompare (Shadows.hlsl), as its own shadow maps are: the shadow coordinates come
// from the same transform as URP's (TerrainShadowCascades.ShadowTransform), so the reversed-Z comparison matches.
TEXTURE2D_SHADOW(_KSP2TerrainShadowMap0);
TEXTURE2D_SHADOW(_KSP2TerrainShadowMap1);
TEXTURE2D_SHADOW(_KSP2TerrainShadowMap2);
float4x4 _KSP2TerrainShadowMatrices[KSP2_TERRAIN_SHADOW_CASCADES];
float4 _KSP2TerrainShadowParams;
float4 _KSP2TerrainShadowNormalOffsets;

// 3x3 bilinear comparisons, a 4x4 texel tent.
float KSP2_TerrainShadowPCF(TEXTURE2D_SHADOW_PARAM(shadowMap, samplerShadowMap), float3 coord, float texel)
{
    float lit = 0.0;
    [unroll] for (int y = -1; y <= 1; y++)
    {
        [unroll] for (int x = -1; x <= 1; x++)
            lit += SAMPLE_TEXTURE2D_SHADOW(shadowMap, samplerShadowMap, float3(coord.xy + float2(x, y) * texel, coord.z));
    }

    return lit * (1.0 / 9.0);
}

// One cascade's lit fraction. Branches rather than a ternary, which would sample every cascade.
float KSP2_TerrainShadowCascade(int cascade, float3 coord, float texel)
{
    float lit;
    [branch] if (cascade == 0)
        lit = KSP2_TerrainShadowPCF(TEXTURE2D_SHADOW_ARGS(_KSP2TerrainShadowMap0, sampler_LinearClampCompare), coord, texel);
    else if (cascade == 1)
        lit = KSP2_TerrainShadowPCF(TEXTURE2D_SHADOW_ARGS(_KSP2TerrainShadowMap1, sampler_LinearClampCompare), coord, texel);
    else
        lit = KSP2_TerrainShadowPCF(TEXTURE2D_SHADOW_ARGS(_KSP2TerrainShadowMap2, sampler_LinearClampCompare), coord, texel);
    return lit;
}

// The visible fraction of the sun at a point of the local scene, as far as the terrain is concerned: 1 outside the
// cascades, with the last tenth of each cascade blended into the next so the switch does not show.
float KSP2_TerrainShadow(float3 positionWS, float3 normalWS)
{
#if defined(KSP2_NO_TERRAIN_SHADOW)
    return 1.0;
#else
    int count = (int)_KSP2TerrainShadowParams.x;
    float texel = _KSP2TerrainShadowParams.y;
    float shadow = 1.0;
    float remaining = 1.0;
    [branch] if (count > 0)
    {
        [unroll] for (int i = 0; i < KSP2_TERRAIN_SHADOW_CASCADES; i++)
        {
            [branch] if (i < count && remaining > 0.0)
            {
                float3 offsetPosition = positionWS + normalWS * _KSP2TerrainShadowNormalOffsets[i];
                float3 coord = mul(_KSP2TerrainShadowMatrices[i], float4(offsetPosition, 1.0)).xyz;
                float2 edge = abs(coord.xy - 0.5) * 2.0;
                float border = max(edge.x, edge.y);
                [branch] if (border < 1.0 && coord.z > 0.0 && coord.z < 1.0)
                {
                    float lit = KSP2_TerrainShadowCascade(i, coord, texel);
                    // Inside the inner nine tenths this cascade decides alone. Toward its edge the next one takes over.
                    float weight = remaining * (i + 1 < count ? saturate((1.0 - border) * 10.0) : 1.0);
                    shadow -= weight * (1.0 - lit);
                    remaining -= weight;
                }
            }
        }
    }

    return shadow;
#endif
}

// Debug view (KSP2ClusterDeferred, _KSP2DeferredDebug 8), in the first cascade that holds the point: r the lit
// fraction, g the comparison at the point's own depth with no offset, b the comparison with the reference depth
// forced to 0 (reversed Z: 0 is farthest from the sun, so b is 0 wherever the map holds terrain). All 0 outside.
float3 KSP2_TerrainShadowDebug(float3 positionWS, float3 normalWS)
{
    int count = (int)_KSP2TerrainShadowParams.x;
    [unroll] for (int i = 0; i < KSP2_TERRAIN_SHADOW_CASCADES; i++)
    {
        float3 coord = mul(_KSP2TerrainShadowMatrices[i], float4(positionWS, 1.0)).xyz;
        [branch] if (i < count && all(abs(coord.xy - 0.5) < 0.5))
        {
            float own = KSP2_TerrainShadowCascade(i, coord, 0.0);
            float farthest = KSP2_TerrainShadowCascade(i, float3(coord.xy, 0.0), 0.0);
            return float3(KSP2_TerrainShadow(positionWS, normalWS), own, farthest);
        }
    }

    return 0.0;
}

#endif // KSP2_TERRAIN_SHADOW_INCLUDED