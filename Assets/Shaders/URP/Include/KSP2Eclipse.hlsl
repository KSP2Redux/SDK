// KSP2Eclipse.hlsl
//
// Eclipses per pixel: how much of the star's disc the bodies around a point hide from it (URP/notes/eclipses.md,
// section 3). Its C# twin is Redux.Rendering.Eclipses.EclipseMath, whose SmoothCoveredFraction this mirrors and whose
// EditMode tests check it against the exact disc overlap. Redux.Rendering.Eclipses.EclipseOccluderTable uploads the
// star and every body that can occlude it once per frame, in the space the receivers draw in, and gives each receiver
// the table indices of its own occluders (its parent, its siblings and its moons, never itself).
//
//   _KSP2EclipseStar        xyz centre, w radius
//   _KSP2EclipseOccluders   xyz centre, w radius, one entry per body
//
// A receiver passes its indices as two float4 (up to eight, -1 for none) from its own material properties.

#ifndef KSP2_ECLIPSE_INCLUDED
#define KSP2_ECLIPSE_INCLUDED

#define KSP2_ECLIPSE_MAX_OCCLUDERS 32

float4 _KSP2EclipseStar;
float4 _KSP2EclipseOccluders[KSP2_ECLIPSE_MAX_OCCLUDERS];
// 1 while the map camera draws, which holds the table in map space (EclipseOccluderTable.UploadMap), 0 otherwise.
float _KSP2EclipseMapActive;

// The angular radius of a sphere seen from a distance. From on or inside the sphere it is a half-space, which makes a
// body the horizon of a point on its surface.
float KSP2_EclipseAngularRadius(float radius, float distance)
{
    return distance <= radius ? HALF_PI : asin(saturate(radius / distance));
}

// The covered fraction of the star's disc, in the cheap form EclipseMath.SmoothCoveredFraction documents: a smoothstep
// from first contact to full overlap, capped by the ratio of the disc areas so an occluder smaller than the star
// (an annular eclipse, a transit) never hides more than its own area.
float KSP2_EclipseCoveredFraction(float starAngularRadius, float occluderAngularRadius, float separation)
{
    float areaRatio = min(1.0, (occluderAngularRadius * occluderAngularRadius) / max(starAngularRadius * starAngularRadius, 1e-20));
    return areaRatio * smoothstep(occluderAngularRadius + starAngularRadius, abs(occluderAngularRadius - starAngularRadius), separation);
}

// The covered fraction for one sphere occluder seen from a point. An occluder at or beyond the star's distance, or
// behind the point while the point is outside it, hides nothing. The angle between the star and the occluder comes
// from atan2 of the cross and dot products, which keeps its precision for the small angles of distant moons, where
// acos of a cosine near 1 would not.
float KSP2_EclipseOccluderFraction(float3 positionWS, float3 toStar, float starDistance, float starAngularRadius, float4 occluder)
{
    float3 toOccluder = occluder.xyz - positionWS;
    float occluderDistance = length(toOccluder);
    if (occluderDistance >= starDistance || occluder.w <= 0.0)
        return 0.0;

    float cosine = dot(toStar, toOccluder);
    if (cosine <= 0.0 && occluderDistance > occluder.w)
        return 0.0;

    float separation = atan2(length(cross(toStar, toOccluder)), cosine);
    float occluderAngularRadius = KSP2_EclipseAngularRadius(occluder.w, occluderDistance);
    return KSP2_EclipseCoveredFraction(starAngularRadius, occluderAngularRadius, separation);
}

// The ring nearest the observer (EclipseOccluderTable.UploadRing), in scaled space and in physics space:
//   centre xyz, w 1 with a ring; plane normal xyz; radii x inner, y outer; the ring texture, opacity in alpha along u.
float4 _KSP2EclipseRingCenter;
float4 _KSP2EclipseRingNormal;
float4 _KSP2EclipseRingRadii;
float4 _KSP2EclipseLocalRingCenter;
float4 _KSP2EclipseLocalRingNormal;
float4 _KSP2EclipseLocalRingRadii;
TEXTURE2D(_KSP2EclipseRingTexture);

// What a ring lets through toward the star: 1 minus its opacity where the ray from the point toward the star crosses
// the ring plane, 1 when it misses the ring or there is none. A point on the ring itself is not shadowed by it.
float KSP2_EclipseRingTransmittance(float3 positionWS, float3 toStarDirection, float4 center, float3 normal, float2 radii)
{
    float transmittance = 1.0;
    float facing = dot(toStarDirection, normal);
    [branch] if (center.w > 0.0 && abs(facing) > 1e-5)
    {
        float t = dot(center.xyz - positionWS, normal) / facing;
        float radius = length(positionWS + toStarDirection * t - center.xyz);
        float width = max(radii.y - radii.x, 1e-6);
        float u = (radius - radii.x) / width;
        // The plane must lie ahead by more than float noise, so the ring does not shadow its own surface.
        if (t > width * 1e-3 && u > 0.0 && u < 1.0)
            transmittance = 1.0 - SAMPLE_TEXTURE2D_LOD(_KSP2EclipseRingTexture, sampler_LinearClamp, float2(u, 0.5), 0).a;
    }

    return transmittance;
}

// The visible fraction of the star from a point: the product of what each listed occluder leaves, and what the ring
// lets through.
float KSP2_EclipseVisibility(float3 positionWS, float4 occluderIndices0, float4 occluderIndices1)
{
    float3 toStar = _KSP2EclipseStar.xyz - positionWS;
    float starDistance = length(toStar);
    if (_KSP2EclipseStar.w <= 0.0 || starDistance <= _KSP2EclipseStar.w)
        return 1.0;

    float starAngularRadius = KSP2_EclipseAngularRadius(_KSP2EclipseStar.w, starDistance);
    // Both sides of a conditional are evaluated once the loop is unrolled, so the eight indices are unpacked first.
    float indices[8] = { occluderIndices0.x, occluderIndices0.y, occluderIndices0.z, occluderIndices0.w,
        occluderIndices1.x, occluderIndices1.y, occluderIndices1.z, occluderIndices1.w };
    float visibility = 1.0;
    [unroll]
    for (int i = 0; i < 8; i++)
    {
        float index = indices[i];
        [branch]
        if (index >= 0.0)
        {
            float4 occluder = _KSP2EclipseOccluders[min((int)index, KSP2_ECLIPSE_MAX_OCCLUDERS - 1)];
            visibility *= 1.0 - KSP2_EclipseOccluderFraction(positionWS, toStar, starDistance, starAngularRadius, occluder);
        }
    }

    return visibility * KSP2_EclipseRingTransmittance(positionWS, toStar / starDistance, _KSP2EclipseRingCenter,
        _KSP2EclipseRingNormal.xyz, _KSP2EclipseRingRadii.xy);
}

// The local scene (physics space, the URP world space of the flight camera): the star and the bodies around the
// observer, the observer's own body included, set by EclipseOccluderTable.UploadLocal. The local sun already carries
// the eclipse at the observer (CelestialBodyStarManager dims it), so a pixel only takes the part of its own eclipse
// that goes beyond the observer's.
#define KSP2_ECLIPSE_MAX_LOCAL_OCCLUDERS 8

float4 _KSP2EclipseLocalStar;
float4 _KSP2EclipseLocalObserver;   // xyz observer, w its visibility of the star
float4 _KSP2EclipseLocalOccluders[KSP2_ECLIPSE_MAX_LOCAL_OCCLUDERS];
float _KSP2EclipseLocalCount;
// The same count for the local camera whichever light is the main one. A total eclipse switches the sun off, and the
// air and clouds still need the table to go dark (#2026). The main-light factors below keep _KSP2EclipseLocalCount.
float _KSP2EclipseLocalSceneCount;

// The visible fraction of the star at a point of the local scene, 1 without a table (off the local camera).
float KSP2_EclipseLocalVisibility(float3 positionWS)
{
    float3 toStar = _KSP2EclipseLocalStar.xyz - positionWS;
    float starDistance = length(toStar);
    float starAngularRadius = KSP2_EclipseAngularRadius(_KSP2EclipseLocalStar.w, starDistance);
    float visibility = 1.0;
    int count = _KSP2EclipseLocalStar.w > 0.0 ? (int)_KSP2EclipseLocalCount : 0;
    [loop]
    for (int i = 0; i < count; i++)
    {
        visibility *= 1.0 - KSP2_EclipseOccluderFraction(positionWS, toStar, starDistance, starAngularRadius,
            _KSP2EclipseLocalOccluders[i]);
    }

    return visibility;
}

// The visible fraction of the star at a point of the local scene with the ring's shadow, leaving out the occluder centred
// at excludedCenterWS: an atmosphere's own planet, whose shadow its scattering tables already hold. 1 without a table.
float KSP2_EclipseLocalVisibilityExcluding(float3 positionWS, float3 excludedCenterWS)
{
    float3 toStar = _KSP2EclipseLocalStar.xyz - positionWS;
    float starDistance = length(toStar);
    int count = _KSP2EclipseLocalStar.w > 0.0 ? (int)_KSP2EclipseLocalSceneCount : 0;
    if (count == 0 || starDistance <= _KSP2EclipseLocalStar.w)
        return 1.0;

    float starAngularRadius = KSP2_EclipseAngularRadius(_KSP2EclipseLocalStar.w, starDistance);
    float visibility = 1.0;
    [loop]
    for (int i = 0; i < count; i++)
    {
        float4 occluder = _KSP2EclipseLocalOccluders[i];
        // The same body when the centres lie within a hundredth of its radius.
        float3 offset = occluder.xyz - excludedCenterWS;
        if (dot(offset, offset) > occluder.w * occluder.w * 1e-4)
            visibility *= 1.0 - KSP2_EclipseOccluderFraction(positionWS, toStar, starDistance, starAngularRadius, occluder);
    }

    return visibility * KSP2_EclipseRingTransmittance(positionWS, toStar / starDistance, _KSP2EclipseLocalRingCenter,
        _KSP2EclipseLocalRingNormal.xyz, _KSP2EclipseLocalRingRadii.xy);
}

// The factor for the local main light at a pixel: its eclipse relative to the observer's, never brighter. Terrain past
// the terminator seen from altitude, a vessel across it, or the edge of a moon's shadow on the ground go dark, while
// pixels less eclipsed than the observer keep the observer's light. 1 when there is no table.
float KSP2_EclipseLocalSunFactor(float3 positionWS)
{
    // The observer's visibility is computed once on the CPU with the same formula
    // (EclipseOccluderTable.SmoothLocalVisibility).
    float observerVisibility = _KSP2EclipseLocalObserver.w;
    float factor = 1.0;
    [branch]
    if (_KSP2EclipseLocalCount >= 0.5 && _KSP2EclipseLocalStar.w > 0.0)
    {
        if (observerVisibility > 1e-4)
            factor = saturate(KSP2_EclipseLocalVisibility(positionWS) / observerVisibility);
        // Stock's sun at the observer knows nothing of rings, so the ring's shadow applies as it is.
        factor *= KSP2_EclipseRingTransmittance(positionWS, normalize(_KSP2EclipseLocalStar.xyz - positionWS),
            _KSP2EclipseLocalRingCenter, _KSP2EclipseLocalRingNormal.xyz, _KSP2EclipseLocalRingRadii.xy);
    }

    return factor;
}

// The local star's angular radius at the observer, the size of its disc in a mirror, or 0 without a table.
float KSP2_EclipseLocalStarAngularRadius()
{
    float distance = length(_KSP2EclipseLocalStar.xyz - _KSP2EclipseLocalObserver.xyz);
    return _KSP2EclipseLocalCount >= 0.5 && _KSP2EclipseLocalStar.w > 0.0
        ? KSP2_EclipseAngularRadius(_KSP2EclipseLocalStar.w, distance)
        : 0.0;
}

#endif // KSP2_ECLIPSE_INCLUDED