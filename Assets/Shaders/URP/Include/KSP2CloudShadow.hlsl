// KSP2CloudShadow.hlsl
//
// Screen-space volumetric cloud shadow of the flight physics camera (VolumeCloudRenderer,
// CumulusCloudShadowMask.hlsl). It is white on every other camera of the frame. Stock's deferred
// lighting multiplied every light's attenuation by it, so the ports of everything stock drew deferred
// apply it to their lights. _EnableCloudsShadow is set with Shader.SetGlobalInt, so it is a float.
//
// Include after URP Core.hlsl.

#ifndef KSP2_CLOUD_SHADOW_INCLUDED
#define KSP2_CLOUD_SHADOW_INCLUDED

TEXTURE2D(_CloudsShadowTexture);
float _EnableCloudsShadow;

// The cloud shadow at a pixel, 1 where there is none.
float KSP2_CloudShadow(float2 normalizedScreenSpaceUV)
{
    if (_EnableCloudsShadow == 0.0)
        return 1.0;
    return SAMPLE_TEXTURE2D_LOD(_CloudsShadowTexture, sampler_LinearClamp, normalizedScreenSpaceUV, 0).r;
}

#endif // KSP2_CLOUD_SHADOW_INCLUDED