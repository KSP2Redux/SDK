// Exploratory airless-regolith diffuse response, shared by direct views and the observer cubemap.
#ifndef KSP2_CELESTIALBODY_SCALED_PHOTOMETRY_INCLUDED
#define KSP2_CELESTIALBODY_SCALED_PHOTOMETRY_INCLUDED

// Smooth isotropic Hapke: no opposition surge, particle phase lobe, porosity or macroscopic roughness.
// H(x) = (1 + 2x) / (1 + 2x sqrt(1-w)). The radiance term already includes incident foreshortening.
// https://github.com/DOI-USGS/ISIS3/blob/dev/isis/src/base/objs/Hapke/Hapke.cpp
// Divide by the normal-incidence, normal-view value to retain the authored RGB at that reference geometry.
// RGB remains a surface color, not calibrated single-scattering albedo. This is an angular approximation.
float KSP2_CelestialBodyRegolithDiffuse(float3 N, float3 V, float3 L)
{
    float incidence = saturate(dot(N, L));
    float emission = saturate(dot(N, V));
    if (incidence <= 0.0 || emission <= 0.0)
        return 0.0;

    float gamma = sqrt(1.0 - saturate(_RegolithSingleScatteringAlbedo));
    float incidentH = (1.0 + 2.0 * incidence) / (1.0 + 2.0 * incidence * gamma);
    float emittedH = (1.0 + 2.0 * emission) / (1.0 + 2.0 * emission * gamma);
    float normalH = 3.0 / (1.0 + 2.0 * gamma);
    return 2.0 * incidence / max(incidence + emission, 1e-7) * incidentH * emittedH / (normalH * normalH);
}

float KSP2_CelestialBodyRegolithWeight()
{
    return saturate(_KSP2RegolithWeight) * saturate(_RegolithStrength);
}

float KSP2_CelestialBodyDiffuse(float3 N, float3 V, float3 L, float originalDiffuse)
{
    float weight = KSP2_CelestialBodyRegolithWeight();
    [branch] if (weight <= 0.0)
        return originalDiffuse;

    return lerp(originalDiffuse, KSP2_CelestialBodyRegolithDiffuse(N, V, L), weight);
}

// Only scaled surfaces opt into this hook. Other materials keep the KSP2 Standard diffuse response.
#define KSP2_CUSTOM_DIFFUSE_TERM(N, V, L, diffuse) KSP2_CelestialBodyDiffuse(N, V, L, diffuse)

#endif // KSP2_CELESTIALBODY_SCALED_PHOTOMETRY_INCLUDED
