// Scaled-space shell for Hillaire atmospheres (HillaireAtmosphereProfile.scaledShaderName).
// URP: same name, properties, keywords and render state as the built-in version. Every value it reads
// is set on the material (HillaireAtmosphereRenderer.BindProfile, AtmosphereDataModelComponent), so
// all of them are in UnityPerMaterial; HillaireAtmosphereCommon.hlsl declares them as plain globals,
// so this shader declares its own copies and does not include it.
Shader "KSP2/Environment/Atmosphere/HillaireScaled"
{
    Properties
    {
        _Transition ("Transition", Float) = 1
        _DitheringScale ("Dithering Scale", Float) = 1
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" }
        Cull [_Cull]
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ OUTER_ATMO_MODE
            #pragma multi_compile_local _ MAP_MODE
            #pragma multi_compile_local _ GAS_GIANT_MODE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float  _Transition;
                float  _DitheringScale;
                float  _Cull;
                float  scale_factor;
                float4 _HillairePlanetCenterWorld;
                float4 _HillaireSunDirection;
                float4 sun_direction;
                float4 _HillaireRayleighScattering;
                float4 _HillaireMieScattering;
                float4 _HillaireSolarIrradiance;
                float4 _HillaireExposure;
                float  _HillaireBottomRadius;
                float  _HillaireTopRadius;
                float  _HillaireTransmittanceTint;
            CBUFFER_END

            // Same as HillaireSafeSunDirection in HillaireAtmosphereCommon.hlsl.
            float3 HillaireScaledSunDirection()
            {
                float3 dir = dot(_HillaireSunDirection.xyz, _HillaireSunDirection.xyz) > 0.001 ? _HillaireSunDirection.xyz : sun_direction.xyz;
                return normalize(dot(dir, dir) > 0.001 ? dir : float3(0.0, 1.0, 0.0));
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWorld : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.worldPos = mul(UNITY_MATRIX_M, v.vertex).xyz;
                o.normalWorld = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 center = _HillairePlanetCenterWorld.xyz;
                float3 up = normalize(i.worldPos - center);
                float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
                float3 sunDir = HillaireScaledSunDirection();
                float limb = pow(saturate(1.0 - abs(dot(up, -viewDir))), 2.2);
                float sun = saturate(dot(up, sunDir) * 0.5 + 0.5);
                float3 baseColor = _HillaireRayleighScattering.xyz * 12.0 + _HillaireMieScattering.xyz * 7.0 * sun;
                float horizon = pow(saturate(1.0 - dot(up, sunDir) * 0.5), 2.0);
                float3 sunset = float3(1.0, 0.38, 0.12) * horizon * _HillaireMieScattering.r * 9.0;
                float density = saturate((_HillaireTopRadius - _HillaireBottomRadius) / max(0.001, _HillaireBottomRadius) * 5.0);

                #if defined(GAS_GIANT_MODE)
                density *= 2.0;
                baseColor = lerp(baseColor, _HillaireSolarIrradiance.xyz * 0.18, 0.35);
                #endif

                float shell = pow(saturate(limb), 1.55);
                float alpha = saturate(shell * density * _Transition * _HillaireTransmittanceTint * 0.45);
                float3 color = (baseColor + sunset) * _HillaireExposure.y * 0.04;
                return half4(color * alpha, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}