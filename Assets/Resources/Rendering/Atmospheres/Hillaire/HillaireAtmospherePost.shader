// Hillaire local-space atmosphere composite (HillaireAtmosphereProfile.postShaderName).
// URP: same name, property and math as the built-in version. PostAtmosphereRenderHook copies the camera
// colour and draws this pass over the camera with Blitter (Blit.hlsl Vert), reading the copy as
// _BlitTexture, at BeforeRenderingTransparents with a depth input. View rays come from the URP inverse
// matrices instead of the BRP frustumCorners matrix.
Shader "Hidden/KSP/HillaireAtmospherePost"
{
    Properties
    {
        _Transition ("Transition", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always
        Blend Off

        Pass
        {
            Name "HillaireComposite"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "HillaireAtmosphereCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Transition;
            CBUFFER_END

            float3 planet_center;
            TEXTURE2D(_OceanDepthTexture);

            static const float BAYER[16] =
            {
                0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
            };

            // Jimenez's interleaved gradient noise, to place each pixel's samples differently along its ray.
            float HillaireInterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            // The nearer of the scene and ocean depths, or none when both are the far plane.
            bool TrySceneDepth(float2 uv, out float rawDepth)
            {
                float scene = SampleSceneDepth(uv);
                float ocean = SAMPLE_TEXTURE2D_LOD(_OceanDepthTexture, sampler_PointClamp, uv, 0.0).r;
                #if defined(UNITY_REVERSED_Z)
                rawDepth = max(scene, ocean);
                return rawDepth > 0.0;
                #else
                rawDepth = min(scene, ocean);
                return rawDepth < 1.0;
                #endif
            }

            // View-space point on the far plane under the pixel, so its length over the far distance scales eye
            // depth to distance along the ray.
            float3 FarPlaneViewPosition(float2 uv)
            {
                float4 farCS = ComputeClipSpacePosition(uv, UNITY_RAW_FAR_CLIP_VALUE);
                float4 farVS = mul(UNITY_MATRIX_I_P, farCS);
                return farVS.xyz / farVS.w;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Fades the post atmosphere out as the body's scaled shell takes over, the way stock's does.
                uint2 pixel = (uint2)input.positionCS.xy;
                if (1.0 - _Transition < BAYER[(pixel.y & 3) * 4 + (pixel.x & 3)])
                {
                    discard;
                }

                float2 uv = input.texcoord;
                float4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv, 0.0);
                float3 rayVS = FarPlaneViewPosition(uv);
                float3 cameraKm = (_WorldSpaceCameraPos - planet_center * 1000.0) * 0.001;
                float3 direction = normalize(mul((float3x3)UNITY_MATRIX_I_V, rayVS));
                float tAtmosphereNear;
                float tAtmosphereFar;
                if (!HillaireRaySphereInterval(cameraKm, direction, _HillaireTopRadius, tAtmosphereNear, tAtmosphereFar))
                {
                    return sceneColor;
                }

                float tStart = max(0.0, tAtmosphereNear);
                float tEnd = tAtmosphereFar;
                float tGround = HillaireRaySphereNearest(cameraKm, direction, _HillaireBottomRadius);
                if (tGround > 0.0)
                {
                    tEnd = min(tEnd, tGround);
                }

                float rawDepth;
                if (TrySceneDepth(uv, rawDepth))
                {
                    // Eye depth along the far-plane ray scales its length by depth over the far plane.
                    float sceneDistanceKm = LinearEyeDepth(rawDepth, _ZBufferParams) * length(rayVS) / -rayVS.z * 0.001;
                    tEnd = min(tEnd, sceneDistanceKm);
                }

                if (tEnd <= tStart)
                {
                    return sceneColor;
                }

                float tMax = tEnd - tStart;
                float3 transmittance;
                float3 luminance = HillaireRayMarch(
                    cameraKm + direction * tStart,
                    direction,
                    tMax,
                    HillaireSampleCount(tMax),
                    HillaireInterleavedGradientNoise(input.positionCS.xy),
                    transmittance);

                // Stock's exposure, blended from its ground value to its top-of-atmosphere value by camera height.
                float bottomSq = _HillaireBottomRadius * _HillaireBottomRadius;
                float heightBlend = saturate((dot(cameraKm, cameraKm) - bottomSq) /
                    max(0.001, _HillaireTopRadius * _HillaireTopRadius - bottomSq));
                float exposure = lerp(_HillaireExposure.x, _HillaireExposure.y, heightBlend) * 0.1;

                float3 tintedTransmittance = lerp(
                    dot(transmittance, 1.0 / 3.0).xxx,
                    transmittance,
                    saturate(_HillaireTransmittanceTint));
                return float4(sceneColor.rgb * tintedTransmittance + luminance * exposure, sceneColor.a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}