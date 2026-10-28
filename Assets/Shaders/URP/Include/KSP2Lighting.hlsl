// KSP2Lighting.hlsl
//
// Shared URP lighting for the ported KSP2 lit shaders. The material math comes from the
// decompiled BRP shaders (ksp2-shaders/<Shader>/final). Only the light and probe sources
// are URP: the main and Forward+ clustered lights, URP shadows, URP SH and reflection probes.
//
// KSP2's own model is kept:
//   - direct light: Unity Standard BRDF1 (Disney diffuse + GGX, KSP2 roughness = 1 - smoothness * _MipBias), its
//     grazing Fresnel limited by the smoothness (KSP2_StandardDirectLight)
//   - indirect diffuse: spherical-gaussian ambient + SH, scaled by _DiffuseAmbientIntensity,
//     plus a blurred environment cube scaled by _DiffuseCubemapIntensity
//   - indirect specular: environment at Unity's roughness mip plus KSP2's sigmoid mip step,
//     from the galaxy cube (RK_GALAXY_CUBEMAP), the observer cube over the galaxy cube
//     (RK_OBSERVER_CUBEMAP), or URP reflection probes
//
// Every lit pass defines _CLUSTER_LIGHT_LOOP before including this file: Ksp2Redux renders all
// cameras with Forward+ renderers, so the per-object additional light path is never compiled.
// Additional lights use the Built-in falloff and spot cone (KSP2LegacyLights.hlsl), not URP's
// inverse square, because every KSP2 light was authored for it.

#ifndef KSP2_URP_LIGHTING_INCLUDED
#define KSP2_URP_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "KSP2LegacyLights.hlsl"
#include "KSP2CloudShadow.hlsl"
#include "KSP2WaterShadow.hlsl"
#include "KSP2Eclipse.hlsl"
#include "KSP2LocalSunTint.hlsl"
#include "KSP2TerrainShadow.hlsl"

// ---------------------------------------------------------------------------
// Global KSP2 environment state (set from C#, never per material)
// ---------------------------------------------------------------------------
TEXTURECUBE(_GalaxyCubemapTexture);     SAMPLER(sampler_GalaxyCubemapTexture);
TEXTURECUBE(_ObserverCubemapTexture);   SAMPLER(sampler_ObserverCubemapTexture);
float4 _GalaxyCubemapTexture_HDR;
float4 _ObserverCubemapTexture_HDR;
float4x4 _ReflectionPhysicsWorldToLocalMatrix;
float4x4 _ReflectionToSkyboxRotationMatrix;

// CelestialBodyGIProbeData: three spherical-gaussian lobes (.xyz amplitude, .w sharpness) and axes.
float4 _SGAmplitudeAndSharpness[3];
float4 _SGAxis[3];

// ApplyCubemaps / RenderKitSettings. The integer-looking values are set as floats.
float _DiffuseAmbientIntensity;
float _DiffuseCubemapIntensity;
float _DiffuseCubemapMip;
float _ReflectionIntensityMultiplier;
float _MipBiasMaxSteps;
float _MipBiasGrowthRate;
float _MipBiasOffset;

// KSC time-of-day emission gate (surface-vs-sun dot product published by the game).
float _TimeOfDayKSC;

// The terrain horizon around the camera, from CubemapReflectionSystem: the sine of its elevation at each of
// KSP2_TERRAIN_HORIZON_AZIMUTHS azimuths clockwise from north, the camera's local frame in world space, and in
// _KSP2TerrainHorizonParams x 1 while it applies and y the sine of the elevation directions under it are moved to.
#define KSP2_TERRAIN_HORIZON_AZIMUTHS 64
float _KSP2TerrainHorizon[KSP2_TERRAIN_HORIZON_AZIMUTHS];
float4 _KSP2TerrainHorizonUp;
float4 _KSP2TerrainHorizonNorth;
float4 _KSP2TerrainHorizonEast;
float4 _KSP2TerrainHorizonParams;

// ---------------------------------------------------------------------------
// Surface description shared by every KSP2 lit shader
// ---------------------------------------------------------------------------
struct KSP2Surface
{
    float3 albedo;
    float3 diffuseAlbedo;   // albedo * (0.96 - 0.96 * metallic)
    float3 F0;
    float  metallic;
    float  smoothness;      // already scaled by the shader's _MipBias where the original did so
    float  occlusion;
    float3 emission;
    float  alpha;
};

// Builds the metallic-workflow terms the way Unity's Standard shader does.
KSP2Surface KSP2_MakeMetallicSurface(float3 albedo, float metallic, float smoothness, float occlusion, float3 emission, float alpha)
{
    KSP2Surface s;
    s.albedo = albedo;
    s.metallic = metallic;
    s.smoothness = smoothness;
    s.occlusion = occlusion;
    s.emission = emission;
    s.alpha = alpha;
    s.F0 = lerp(float3(0.04, 0.04, 0.04), albedo, metallic);
    s.diffuseAlbedo = albedo * (0.96 - metallic * 0.96);
    return s;
}

float KSP2_Pow5(float x)
{
    float x2 = x * x;
    return x2 * x2 * x;
}

// ---------------------------------------------------------------------------
// Unity Standard BRDF1 as the KSP2 shaders evaluate it, with one change: the Fresnel term rises toward the
// smoothness at grazing angles instead of toward one (F90 = max(smoothness, F0)).
// Stock's Schlick term reaches one on every surface. With a low sun ahead of the camera the half vector stands
// near the normal while L.H is small, so rough ground (terrain, Decalicious decals through their normal maps)
// reflected the sun several times brighter than it was lit, the wet or plastic look at sunset. Microfacet
// shadowing keeps rough surfaces from reaching full reflection there, as the split-sum environment term
// (KSP2_EnvironmentReflectionWeight) already assumes. Smooth surfaces (water, ice, polished metal) keep their glint,
// and the term is unchanged at normal incidence.
// diffuseTerm already includes N.L, specularTerm includes N.L and Fresnel. minRoughness floors the specular lobe's
// GGX roughness alone, for a light with a size (KSP2_ShadeSun).
// ---------------------------------------------------------------------------
void KSP2_StandardDirectLight(float3 N, float3 V, float3 L, float3 F0, float perceptualRoughness, float minRoughness,
                              out float diffuseTerm, out float3 specularTerm)
{
    float3 H  = (V + L) * rsqrt(max(dot(V + L, V + L), 0.001));
    float  nl = saturate(dot(N, L));
    float  nv = abs(dot(N, V));
    float  nh = saturate(dot(N, H));
    float  lh = saturate(dot(L, H));

    float fd90m1 = ((lh + lh) * lh) * perceptualRoughness - 0.5;
    diffuseTerm = (fd90m1 * KSP2_Pow5(1.0 - nv) + 1.0) * (fd90m1 * KSP2_Pow5(1.0 - nl) + 1.0) * nl;

    float roughness = max(perceptualRoughness * perceptualRoughness, max(minRoughness, 0.002));
    float a2 = roughness * roughness;
    float d = ((nh * a2 - nh) * nh) + 1.0;
    float ggxD = (a2 * INV_PI) / (d * d + 1e-7);
    float ggxV = 0.5 / (((nl * ((nv * (1.0 - roughness)) + roughness)) + (nv * ((nl * (1.0 - roughness)) + roughness))) + 1e-5);
    float hasSpec = (dot(F0, F0) != 0.0) ? 1.0 : 0.0;
    float spec = hasSpec * max(((ggxD * ggxV) * PI) * nl, 0.0);
    float3 F90 = max((1.0 - perceptualRoughness).xxx, F0);
    float3 fresnel = ((F90 - F0) * KSP2_Pow5(1.0 - lh)) + F0;
    specularTerm = spec * fresnel;
}

void KSP2_StandardDirectLight(float3 N, float3 V, float3 L, float3 F0, float perceptualRoughness,
                              out float diffuseTerm, out float3 specularTerm)
{
    KSP2_StandardDirectLight(N, V, L, F0, perceptualRoughness, 0.0, diffuseTerm, specularTerm);
}

// ---------------------------------------------------------------------------
// Environment
// ---------------------------------------------------------------------------
float3 KSP2_SphericalGaussianAmbient(float3 N)
{
    float3 lobe0 = _SGAmplitudeAndSharpness[0].xyz * exp((dot(N, _SGAxis[0].xyz) - 1.0) * _SGAmplitudeAndSharpness[0].w);
    float3 lobe1 = _SGAmplitudeAndSharpness[1].xyz * exp((dot(N, _SGAxis[1].xyz) - 1.0) * _SGAmplitudeAndSharpness[1].w);
    float3 lobe2 = _SGAmplitudeAndSharpness[2].xyz * exp((dot(N, _SGAxis[2].xyz) - 1.0) * _SGAmplitudeAndSharpness[2].w);
    return lobe0 + lobe1 + lobe2;
}

float3 KSP2_SkyboxDirection(float3 dir)
{
    return mul((float3x3)mul(_ReflectionToSkyboxRotationMatrix, _ReflectionPhysicsWorldToLocalMatrix), dir);
}

float3 KSP2_ObserverDirection(float3 dir)
{
    return mul((float3x3)_ReflectionPhysicsWorldToLocalMatrix, dir);
}

// The observer cubemap holds the scaled bodies, and the camera's own body hides what lies under its horizon only as a
// sphere at the mean radius. From high ground that horizon lies far below the terrain's, so the ground reflected the
// bodies behind the hills around it, such as Jool behind Vall's ice ridges (#1882). A direction under the terrain's
// horizon is moved down to just under the sphere's, where the cubemap holds the body's own ground.
float3 KSP2_TerrainHorizonDirection(float3 dir)
{
    if (_KSP2TerrainHorizonParams.x <= 0.0)
        return dir;

    float3 up = _KSP2TerrainHorizonUp.xyz;
    float elevation = dot(dir, up);
    float azimuth = atan2(dot(dir, _KSP2TerrainHorizonEast.xyz), dot(dir, _KSP2TerrainHorizonNorth.xyz));
    float position = frac(azimuth * (0.5 * INV_PI) + 1.0) * KSP2_TERRAIN_HORIZON_AZIMUTHS;
    int index = (int)position % KSP2_TERRAIN_HORIZON_AZIMUTHS;
    float horizon = lerp(_KSP2TerrainHorizon[index], _KSP2TerrainHorizon[(index + 1) % KSP2_TERRAIN_HORIZON_AZIMUTHS], frac(position));
    if (elevation >= horizon)
        return dir;

    float3 level = dir - elevation * up;
    float levelLength = length(level);
    level = levelLength > 1e-4 ? level / levelLength : _KSP2TerrainHorizonNorth.xyz;
    float target = _KSP2TerrainHorizonParams.y;
    return level * sqrt(saturate(1.0 - target * target)) + up * target;
}

// Unity's roughness mip curve plus KSP2's sigmoid step term.
float KSP2_SpecularMipLevel(float perceptualRoughness)
{
    return (((1.7 - perceptualRoughness * 0.7) * perceptualRoughness) * 6.0)
        + (_MipBiasMaxSteps / (exp((perceptualRoughness - _MipBiasOffset) * -_MipBiasGrowthRate) + 1.0));
}

// URP picks the probe mip from perceptual roughness. Inverts mip = r * (1.7 - 0.7 r) * 6 so a
// KSP2 mip level can be requested through URP's reflection probe sampling.
float KSP2_MipToPerceptualRoughness(float mip)
{
    float m = clamp(mip, 0.0, 6.0) / 6.0;
    return saturate((1.7 - sqrt(max(2.89 - 2.8 * m, 0.0))) / 1.4);
}

// Samples the KSP2 environment (galaxy, observer over galaxy, or URP reflection probes) at a mip.
float3 KSP2_SampleEnvironment(float3 dir, float3 positionWS, float mip, float2 normalizedScreenSpaceUV)
{
#if defined(RK_OBSERVER_CUBEMAP)
    dir = KSP2_TerrainHorizonDirection(dir);
    float4 gal = SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, KSP2_SkyboxDirection(dir), mip);
    float4 obs = SAMPLE_TEXTURECUBE_LOD(_ObserverCubemapTexture, sampler_ObserverCubemapTexture, KSP2_ObserverDirection(dir), mip);
    return DecodeHDREnvironment(float4(gal.rgb * obs.a + obs.rgb, gal.a * obs.a + obs.a), _ObserverCubemapTexture_HDR);
#elif defined(RK_GALAXY_CUBEMAP)
    return DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, KSP2_SkyboxDirection(dir), mip), _GalaxyCubemapTexture_HDR);
#else
    return GlossyEnvironmentReflection(dir, positionWS, KSP2_MipToPerceptualRoughness(mip), 1.0, normalizedScreenSpaceUV);
#endif
}

// Diffuse environment cube for the ambient term. The observer path blends the galaxy cube under
// the observer cube using the observer alpha, the same way the decompiled program does.
float3 KSP2_SampleDiffuseEnvironment(float3 N, float3 positionWS, float2 normalizedScreenSpaceUV)
{
#if defined(RK_OBSERVER_CUBEMAP)
    float4 obs = SAMPLE_TEXTURECUBE_LOD(_ObserverCubemapTexture, sampler_ObserverCubemapTexture, KSP2_ObserverDirection(N), _DiffuseCubemapMip);
    float3 gal = DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(_GalaxyCubemapTexture, sampler_GalaxyCubemapTexture, KSP2_SkyboxDirection(N), _DiffuseCubemapMip), _GalaxyCubemapTexture_HDR);
    return gal * obs.a + DecodeHDREnvironment(obs, _ObserverCubemapTexture_HDR);
#else
    return KSP2_SampleEnvironment(N, positionWS, _DiffuseCubemapMip, normalizedScreenSpaceUV);
#endif
}

// The environment colour a surface reflects along reflDir: the KSP2 environment at the roughness mip.
float3 KSP2_EnvironmentReflection(float3 reflDir, float3 positionWS, float smoothness, float2 normalizedScreenSpaceUV)
{
    float perceptualRoughness = 1.0 - smoothness;
    return KSP2_SampleEnvironment(reflDir, positionWS, KSP2_SpecularMipLevel(perceptualRoughness), normalizedScreenSpaceUV)
        * _ReflectionIntensityMultiplier;
}

// The split-sum environment BRDF, scale and bias on F0, in Karis's analytic fit (Unreal's EnvBRDFApprox). It takes
// the microfacet shadowing at grazing angles into account, which Standard's grazing term does not.
float2 KSP2_EnvironmentBRDF(float perceptualRoughness, float NoV)
{
    const float4 c0 = float4(-1.0, -0.0275, -0.572, 0.022);
    const float4 c1 = float4(1.0, 0.0425, 1.04, -0.04);
    float4 r = perceptualRoughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    return float2(-1.04, 1.04) * a004 + r.zw;
}

// What the reflected colour is multiplied by: the environment BRDF, occluded.
// Stock used Standard's surface reduction and FresnelLerp toward a grazing term of smoothness plus reflectivity, which
// lets rough surfaces reflect most of the sky at grazing angles (twice the split-sum value at smoothness 0.5, two and
// a half times at 0.2): the wet look of planets seen at a low angle. The two agree at normal incidence and on smooth
// metal. oneMinusReflectivity (0.96 - 0.96 metallic, or 1 - max(F0)) is kept for the callers but no longer needed.
float3 KSP2_EnvironmentReflectionWeight(float3 N, float3 V, float smoothness, float oneMinusReflectivity, float3 F0, float occlusion)
{
    float2 environmentBRDF = KSP2_EnvironmentBRDF(1.0 - smoothness, abs(dot(V, N)));
    return occlusion * (F0 * environmentBRDF.x + environmentBRDF.y);
}

// KSP2_IndirectLighting with the other bodies' eclipse at the pixel supplied by the caller, for a shader that already
// has it (the terrain interpolates it from its vertices).
void KSP2_IndirectLightingEclipsed(float3 positionWS, float3 N, float3 V, KSP2Surface s, float2 normalizedScreenSpaceUV,
                                   float eclipse, out float3 ambientDiffuse, out float3 envSpecular)
{
    float3 envColor = KSP2_EnvironmentReflection(reflect(-V, N), positionWS, s.smoothness, normalizedScreenSpaceUV);
    envSpecular = envColor * KSP2_EnvironmentReflectionWeight(N, V, s.smoothness, 0.96 - s.metallic * 0.96, s.F0, s.occlusion);

    float3 ambient = _DiffuseAmbientIntensity * (KSP2_SphericalGaussianAmbient(N) + SampleSH(N));
    float3 diffuseCube = KSP2_SampleDiffuseEnvironment(N, positionWS, normalizedScreenSpaceUV);
    ambientDiffuse = (ambient + diffuseCube * _DiffuseCubemapIntensity) * s.occlusion;

    // The sky and the surroundings this light comes from lie in the same moon's shadow as the pixel.
    ambientDiffuse *= eclipse;
    envSpecular *= eclipse;
}

// Indirect diffuse (to be multiplied by diffuse albedo) and indirect specular, both occluded.
void KSP2_IndirectLighting(float3 positionWS, float3 N, float3 V, KSP2Surface s, float2 normalizedScreenSpaceUV,
                           out float3 ambientDiffuse, out float3 envSpecular)
{
    KSP2_IndirectLightingEclipsed(positionWS, N, V, s, normalizedScreenSpaceUV,
        KSP2_EclipseLocalOtherBodiesFactor(positionWS), ambientDiffuse, envSpecular);
}

// ---------------------------------------------------------------------------
// Direct lights: URP main light + Forward+ clustered lights through the KSP2 BRDF.
// ---------------------------------------------------------------------------

// A light on this rendering layer is a broad source around the surface, such as the glow of the reentry plasma all
// around a vessel (LegacyLightCullingMask.SetBroadSource). A surface turning away from a source of this angular size
// sees it set behind its horizon gradually, as a sphere light does, so its diffuse light ramps down smoothly between
// N.L of plus and minus the sine of the source's radius instead of cutting off at zero. Surfaces that see the whole
// source are lit as before. Its highlight is as wide as the source. Lit as a point, wings edge-on to the plasma turned
// the 4x4 blocks of their compressed normal maps into lit and dark squares (#1819).
#define KSP2_BROAD_LIGHT_LAYER (1u << 31)

// Sine of the angular radius of the broad sources, set globally (BROAD_LIGHT_HORIZON in LegacyLightCullingMask). Zero
// lights them as points.
float _KSP2BroadLightHorizon;

// Diffuse N.L of a sphere light whose radius has the given sine: N.L where the whole source is above the horizon,
// a quadratic ramp while it sets, and zero once it has.
float KSP2_SphereLightNdotL(float nl, float horizon)
{
    float rising = nl + horizon;
    return nl >= horizon ? nl : (rising > 0.0 ? rising * rising / (4.0 * horizon) : 0.0);
}

float3 KSP2_ShadeLight(Light light, float3 N, float3 V, KSP2Surface s, float cloudShadow, float minRoughness)
{
    float horizon = (light.layerMask & KSP2_BROAD_LIGHT_LAYER) != 0u ? _KSP2BroadLightHorizon : 0.0;
    float diffuseTerm;
    float3 specularTerm;
    KSP2_StandardDirectLight(N, V, light.direction, s.F0, 1.0 - s.smoothness, max(minRoughness, horizon),
        diffuseTerm, specularTerm);
    if (horizon > 0.0)
        diffuseTerm = KSP2_SphereLightNdotL(dot(N, light.direction), horizon);
    float3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * cloudShadow);
    return (s.diffuseAlbedo * diffuseTerm + specularTerm) * radiance;
}

float3 KSP2_ShadeLight(Light light, float3 N, float3 V, KSP2Surface s, float cloudShadow)
{
    return KSP2_ShadeLight(light, N, V, s, cloudShadow, 0.0);
}

float3 KSP2_ShadeLight(Light light, float3 N, float3 V, KSP2Surface s)
{
    return KSP2_ShadeLight(light, N, V, s, 1.0, 0.0);
}

// The local sun through KSP2_ShadeLight, with the star's disc as the floor of the specular lobe. A near mirror (the ice
// of Minmus and Vall) reflected the sun as a point, thousands of times brighter than the disc and a pixel wide, so it
// bloomed and sparkled as the camera moved. GGX roughness half the disc's angular radius reflects the disc at its own
// size and brightness.
float3 KSP2_ShadeSun(Light light, float3 N, float3 V, KSP2Surface s, float cloudShadow)
{
    return KSP2_ShadeLight(light, N, V, s, cloudShadow, 0.5 * KSP2_EclipseLocalStarAngularRadius());
}

// The local sun's own corrections at a pixel: its eclipse (KSP2_EclipseLocalSunFactor), the terrain's shadow and the
// atmosphere's colour (KSP2LocalSunTint.hlsl).
void KSP2_ApplyLocalSun(inout Light mainLight, float3 positionWS, float3 N)
{
    mainLight.shadowAttenuation *= KSP2_EclipseLocalSunFactor(positionWS) * KSP2_TerrainShadow(positionWS, N);
    mainLight.color *= KSP2_LocalSunTint(positionWS, mainLight.direction);
}

float3 KSP2_DirectLighting(float3 positionWS, float4 positionCS, float3 N, float3 V, KSP2Surface s, float2 normalizedScreenSpaceUV)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = N;
    inputData.viewDirectionWS = V;
    inputData.normalizedScreenSpaceUV = normalizedScreenSpaceUV;
    inputData.shadowMask = half4(1, 1, 1, 1);

    half4 shadowMask = half4(1, 1, 1, 1);
    AmbientOcclusionFactor aoFactor = (AmbientOcclusionFactor)0;
    aoFactor.directAmbientOcclusion = 1;
    aoFactor.indirectAmbientOcclusion = 1;
    uint meshRenderingLayers = GetMeshRenderingLayer();

    // Stock's deferred lighting multiplied every light's attenuation by the cloud shadow and looked up the sun
    // shadow at the water surface for everything under water. Forward-only shaders (transparents, overlays)
    // never got either, and define KSP2_NO_CLOUD_SHADOW before including this file.
#if defined(KSP2_NO_CLOUD_SHADOW)
    float cloudShadow = 1.0;
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    float cloudShadow = KSP2_CloudShadow(normalizedScreenSpaceUV);
    inputData.shadowCoord = TransformWorldToShadowCoord(KSP2_MainLightShadowPosition(positionWS, positionCS, normalizedScreenSpaceUV));
#endif

    float3 color = 0;
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    // The local sun carries the night and the atmosphere's colour at the observer. A pixel past the terminator or in a
    // moon's shadow goes darker, and takes its own colour of the light.
    KSP2_ApplyLocalSun(mainLight, positionWS, N);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        color += KSP2_ShadeSun(mainLight, N, V, s, cloudShadow);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = KSP2_GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            color += KSP2_ShadeLight(light, N, V, s, cloudShadow);
        }
    }
#endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = KSP2_GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
#ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
#endif
        {
            color += KSP2_ShadeLight(light, N, V, s, cloudShadow);
        }
    LIGHT_LOOP_END
#endif
    return color;
}

// Full KSP2 shading: indirect + direct + emission. Fog is left to the caller.
float3 KSP2_Shade(float3 positionWS, float4 positionCS, float3 N, float3 V, KSP2Surface s)
{
    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    float3 ambientDiffuse, envSpecular;
    KSP2_IndirectLighting(positionWS, N, V, s, normalizedScreenSpaceUV, ambientDiffuse, envSpecular);
    float3 direct = KSP2_DirectLighting(positionWS, positionCS, N, V, s, normalizedScreenSpaceUV);
    return envSpecular + s.diffuseAlbedo * ambientDiffuse + direct + s.emission;
}

// ---------------------------------------------------------------------------
// Small shared helpers
// ---------------------------------------------------------------------------

// DXT5nm / BC5 tangent-space normal as the KSP2 shaders decode it (x in alpha * red).
float3 KSP2_DecodeNormal(float4 packedNormal, float scale)
{
    float2 xy = (float2(packedNormal.w * packedNormal.x, packedNormal.y) * 2.0 - 1.0) * scale;
    float z = sqrt(1.0 - min(dot(xy, xy), 1.0));
    return float3(xy, z);
}

// Rim emission: view-vs-base-normal falloff tinted by rimColor (.w = intensity), tangent space.
float3 KSP2_RimEmission(float3 tangentSpaceView, float3 baseTangentNormal, float rimFalloff, float4 rimColor)
{
    return pow(1.0 - saturate(dot(normalize(tangentSpaceView), baseTangentNormal)), rimFalloff) * rimColor.rgb * rimColor.w;
}

#endif // KSP2_URP_LIGHTING_INCLUDED