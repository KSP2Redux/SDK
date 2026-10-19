// KSP2Pipeline.hlsl
//
// URP features that Ksp2Redux always has on, fixed at compile time instead of being shader
// keywords. Include it before any URP include in every lit shader. It matches
// Assets/ReduxAssets/Rendering/URP/KSP2Redux_URP.asset and its Deferred+ renderer, change both
// together.
//
//   _CLUSTER_LIGHT_LOOP                Forward+ and Deferred+ both cluster the additional lights
//   _REFLECTION_PROBE_BLENDING         probe blending is on in the URP asset
//   _REFLECTION_PROBE_BOX_PROJECTION   box projection is on in the URP asset
//   _REFLECTION_PROBE_ATLAS            clustered rendering with blending always uses the probe atlas
//   _SHADOWS_SOFT                      soft shadows are on, the quality is read per light at runtime
//   _GBUFFER_NORMALS_OCT               the renderer packs G-buffer normals octahedrally (accurate normals)

#ifndef KSP2_URP_PIPELINE_INCLUDED
#define KSP2_URP_PIPELINE_INCLUDED

#define _CLUSTER_LIGHT_LOOP 1
#define _REFLECTION_PROBE_BLENDING 1
#define _REFLECTION_PROBE_BOX_PROJECTION 1
#define _REFLECTION_PROBE_ATLAS 1
#define _SHADOWS_SOFT 1
#define _GBUFFER_NORMALS_OCT 1

#endif // KSP2_URP_PIPELINE_INCLUDED