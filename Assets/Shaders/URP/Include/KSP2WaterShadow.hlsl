// KSP2WaterShadow.hlsl
//
// Sun shadow under water. Stock resolved the directional shadow in screen space from the nearer of the scene
// depth and the water surface depth (_GlobalWaterDepthTexture) while _EnableWaterShadow was on, so everything
// below the water took the shadow of the water surface in front of it. A splashed-down capsule's heat shield
// stayed sunlit under a sunlit surface instead of falling into the hull's own shadow. Only what stock drew
// deferred got this, so forward-only ports (KSP2_NO_CLOUD_SHADOW) keep their own position.
//
// WaterManager renders the water depth for its camera before the opaques and sets _KSP2WaterShadowActive
// while that camera's opaques draw, so no other camera reads it. _EnableWaterShadow is set with
// Shader.SetGlobalInt, so it is a float.
//
// Include after URP Core.hlsl.

#ifndef KSP2_WATER_SHADOW_INCLUDED
#define KSP2_WATER_SHADOW_INCLUDED

TEXTURE2D_FLOAT(_GlobalWaterDepthTexture);
float _EnableWaterShadow;
float _KSP2WaterShadowActive;

// The position the main light's shadow is looked up at: the water surface in front of the pixel, or the
// pixel itself.
float3 KSP2_MainLightShadowPosition(float3 positionWS, float4 positionCS, float2 normalizedScreenSpaceUV)
{
#if UNITY_REVERSED_Z
    if (_KSP2WaterShadowActive == 0.0 || _EnableWaterShadow == 0.0)
        return positionWS;

    // Cleared to 0, the far plane, where there is no water.
    float waterDepth = SAMPLE_TEXTURE2D_LOD(_GlobalWaterDepthTexture, sampler_PointClamp, normalizedScreenSpaceUV, 0).r;
    if (waterDepth <= positionCS.z)
        return positionWS;

    return ComputeWorldSpacePosition(normalizedScreenSpaceUV, waterDepth, UNITY_MATRIX_I_VP);
#else
    return positionWS;
#endif
}

#endif // KSP2_WATER_SHADOW_INCLUDED