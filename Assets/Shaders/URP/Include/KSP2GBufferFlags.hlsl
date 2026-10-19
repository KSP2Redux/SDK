// KSP2GBufferFlags.hlsl
//
// KSP2's own G-buffer material flag, in a bit URP leaves free (URP uses 1, 2, 4 and 8, see GBufferCommon.hlsl).
// Written by KSP2GBuffer.hlsl, read by KSP2ClusterDeferred.

#ifndef KSP2_GBUFFER_FLAGS_INCLUDED
#define KSP2_GBUFFER_FLAGS_INCLUDED

// Specular setup only: gBuffer0 holds the final diffuse colour, not an albedo URP scales by 1 - max(F0).
#define kKSP2MaterialFlagDiffuseAsIs 16

// The lighting target holds the KSP2 environment specular of KSP2_IndirectLighting, which the screen-space
// reflections recompute from the G-buffer and blend towards the traced colour. Surfaces with other indirect light
// keep their probe reflections.
#define kKSP2MaterialFlagEnvironmentReflections 32

// Debug view of the KSP2 deferred path, set with Shader.SetGlobalFloat (see KSP2ClusterDeferred.hlsl for the
// views). 0 is normal rendering.
float _KSP2DeferredDebug;

#endif // KSP2_GBUFFER_FLAGS_INCLUDED