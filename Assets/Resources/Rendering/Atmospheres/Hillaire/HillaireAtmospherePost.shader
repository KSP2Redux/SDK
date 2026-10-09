Shader "Hidden/KSP/HillaireAtmospherePost"
{
    Properties
    {
        _Transition ("Transition", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "HillaireAtmosphereCommon.hlsl"

            // Rows are the far-plane corners as rays from the camera, so a pixel's interpolated ray reaches the far plane.
            float4x4 frustumCorners;
            float3 planet_center;
            float _Transition;
            sampler2D _HillaireSceneColor;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            UNITY_DECLARE_DEPTH_TEXTURE(_OceanDepthTexture);

            static const float BAYER[16] =
            {
                0.0 / 16.0, 8.0 / 16.0, 2.0 / 16.0, 10.0 / 16.0,
                12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0, 6.0 / 16.0,
                3.0 / 16.0, 11.0 / 16.0, 1.0 / 16.0, 9.0 / 16.0,
                15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0, 5.0 / 16.0
            };

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 ray : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = float4(v.vertex.xy, 0.0, 1.0);
                o.uv = v.uv;
                float3 bottom = lerp(frustumCorners[3].xyz, frustumCorners[2].xyz, v.uv.x);
                float3 top = lerp(frustumCorners[0].xyz, frustumCorners[1].xyz, v.uv.x);
                o.ray = lerp(bottom, top, v.uv.y);
                return o;
            }

            // Jimenez's interleaved gradient noise, to place each pixel's samples differently along its ray.
            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            // The nearer of the scene and ocean depths, or none when both are the far plane.
            bool TrySceneDepth(float2 uv, out float rawDepth)
            {
                float scene = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float ocean = SAMPLE_DEPTH_TEXTURE(_OceanDepthTexture, uv);
                #if defined(UNITY_REVERSED_Z)
                rawDepth = max(scene, ocean);
                return rawDepth > 0.0;
                #else
                rawDepth = min(scene, ocean);
                return rawDepth < 1.0;
                #endif
            }

            float4 frag(v2f i) : SV_Target
            {
                // Fades the post atmosphere out as the body's scaled shell takes over, the way stock's does.
                uint2 pixel = (uint2)i.pos.xy;
                if (1.0 - _Transition < BAYER[(pixel.y & 3) * 4 + (pixel.x & 3)])
                {
                    discard;
                }

                float4 sceneColor = tex2D(_HillaireSceneColor, i.uv);
                float3 cameraKm = (_WorldSpaceCameraPos - planet_center * 1000.0) * 0.001;
                float3 direction = normalize(i.ray);
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
                if (TrySceneDepth(i.uv, rawDepth))
                {
                    // Eye depth along the far-plane ray scales its length by depth over the far plane.
                    float sceneDistanceKm = LinearEyeDepth(rawDepth) * length(i.ray) / _ProjectionParams.z * 0.001;
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
                    InterleavedGradientNoise(i.pos.xy),
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
            ENDCG
        }
    }
}
