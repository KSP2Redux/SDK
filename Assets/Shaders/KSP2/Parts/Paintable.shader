Shader "KSP2/Parts/Paintable"
{
    Properties
    {
        [Header(Surface)]
        _Color ("Albedo Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [NoScaleOffset] _MetallicGlossMap ("Metallic / Smoothness", 2D) = "white" {}
        [Gamma] _Metallic ("Metallic Strength", Range(0, 1)) = 0
        _GlossMapScale ("Smoothness Strength", Range(0, 1)) = 1
        _MipBias ("Texture Mip Bias", Range(0, 1)) = 0.8

        [Header(Normals)]
        [NoScaleOffset] _BumpMap ("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _DetailBumpMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailMask ("Detail Normal Mask", 2D) = "white" {}
        _DetailBumpScale ("Detail Normal Strength", Range(0, 1)) = 1
        _DetailBumpTiling ("Detail Normal Tiling", Range(0.01, 10)) = 1

        [Header(Occlusion)]
        [NoScaleOffset] _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        [NoScaleOffset] _EmissionMap ("Emission Map", 2D) = "white" {}

        [Header(Reentry)]
        [Toggle] _ReentryEmission ("Reentry Heating Enabled", Float) = 0

        [Space]
        [Header(Sun Angle Emission)]
        [Toggle(USE_TIME_OF_DAY)] _UseTimeOfDay ("Use Sun-Angle Emission Fade", Float) = 0
        _TimeOfDayDotMin ("Sun Fade Start", Range(-1, 1)) = -0.005
        _TimeOfDayDotMax ("Sun Fade End", Range(-1, 1)) = 0.005

        [Header(Paint)]
        _PaintA ("Base Paint", Color) = (1,1,1,0)
        _PaintB ("Accent Paint", Color) = (1,1,1,0)
        [NoScaleOffset] _PaintMaskGlossMap ("Paint Mask / Paint Smoothness", 2D) = "white" {}
        _PaintGlossMapScale ("Paint Smoothness Strength", Range(0, 1)) = 1
        _PaintSmoothnessDamping ("Paint Matte Damping", Range(0, 1)) = 0.85
        [Toggle] _SmoothnessOverride ("Use Paint Mask Smoothness", Float) = 0

        [Header(Rim)]
        [PerRendererData] _RimFalloff ("Rim Falloff", Range(0.01, 5)) = 0.1
        [PerRendererData] _RimColor ("Rim Color", Color) = (0,0,0,0)

        [Header(Advanced Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Culling ("Cull Mode", Float) = 2
        _Offset ("Depth Offset", Range(-1, 1)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "PerformanceChecks" = "False" }
        LOD 300
        Cull [_Culling]
        Offset 0, [_Offset]

        CGPROGRAM
        #pragma surface surf KSPPart fullforwardshadows addshadow vertex:vert
        #pragma target 5.0
        #pragma shader_feature_local USE_TIME_OF_DAY
        #pragma shader_feature_local _REENTRYEMISSION_ON
        #pragma multi_compile _ RK_OBSERVER_CUBEMAP
        #pragma multi_compile _ RK_GALAXY_CUBEMAP

        #include "UnityCG.cginc"
        #include "UnityStandardUtils.cginc"
        #include "UnityPBSLighting.cginc"

        struct Input
        {
            float2 uv_MainTex;
            float2 uv2_PaintMaskGlossMap;
            float3 viewDir;
            float3 worldNormal;
            float3 localPos;
            INTERNAL_DATA
        };

        struct SurfaceOutputKSPPart
        {
            fixed3 Albedo;
            float3 Normal;
            half3 Emission;
            half Metallic;
            half Smoothness;
            half Occlusion;
            half PaintCoverage;
            fixed Alpha;
        };

        fixed4 _Color;
        sampler2D _MainTex;

        sampler2D _MetallicGlossMap;
        half _Metallic;
        half _GlossMapScale;
        half _MipBias;

        sampler2D _BumpMap;
        sampler2D _DetailBumpMap;
        sampler2D _DetailMask;
        half _DetailBumpScale;
        half _DetailBumpTiling;

        sampler2D _OcclusionMap;
        half _OcclusionStrength;

        sampler2D _EmissionMap;
        fixed4 _EmissionColor;
        half _ReentryEmission;
        half _UseTimeOfDay;
        half _TimeOfDayDotMin;
        half _TimeOfDayDotMax;

        UNITY_DECLARE_TEXCUBE(_ObserverCubemapTexture);
        half4 _ObserverCubemapTexture_HDR;
        UNITY_DECLARE_TEXCUBE(_GalaxyCubemapTexture);
        half4 _GalaxyCubemapTexture_HDR;
        half _ReflectionIntensityMultiplier;
        float4x4 _ReflectionPhysicsWorldToLocalMatrix;
        float4x4 _ReflectionToSkyboxRotationMatrix;

        sampler2D _PaintMaskGlossMap;
        fixed4 _PaintA;
        fixed4 _PaintB;
        half _SmoothnessOverride;
        half _PaintGlossMapScale;
        half _PaintSmoothnessDamping;

        fixed4 _RimColor;
        half _RimFalloff;

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.localPos = v.vertex.xyz;
        }

        fixed4 SampleTex2DBias(sampler2D tex, float2 uv)
        {
            return tex2Dbias(tex, float4(uv, 0.0, _MipBias));
        }

        half3 UnpackDetailNormal(float2 uv, half mask)
        {
            half3 detailNormal = UnpackScaleNormal(tex2D(_DetailBumpMap, uv * _DetailBumpTiling), _DetailBumpScale);
            return lerp(half3(0, 0, 1), detailNormal, mask);
        }

        half ApplyPaint(in fixed4 paintMask, inout fixed3 albedo, inout half metallic, inout half smoothness)
        {
            half paintAWeight = saturate(paintMask.r * _PaintA.a);
            half paintBWeight = saturate(paintMask.g * _PaintB.a);
            half paintCoverage = max(paintAWeight, paintBWeight);

            half dirtDarkening = lerp(1.0h, 0.65h, saturate(paintMask.b));
            albedo = lerp(albedo, _PaintB.rgb * dirtDarkening, paintBWeight);
            albedo = lerp(albedo, _PaintA.rgb * dirtDarkening, paintAWeight);

            if (_SmoothnessOverride > 0.5h)
            {
                half paintSmoothness = saturate(paintMask.a * _PaintGlossMapScale);
                smoothness = lerp(smoothness, paintSmoothness, paintCoverage);
            }

            return paintCoverage;
        }

        half3 CompressReflection(half3 color)
        {
            half peak = max(color.r, max(color.g, color.b));
            return color / (1.0h + peak);
        }

        half3 TransformRenderKitReflection(half3 reflection)
        {
            half3 physicsReflection = mul((half3x3)_ReflectionPhysicsWorldToLocalMatrix, reflection);
            return normalize(mul((half3x3)_ReflectionToSkyboxRotationMatrix, physicsReflection));
        }

        void BuildKspPartSpecular(
            fixed3 albedo,
            half metallic,
            half paintCoverage,
            out half3 diffuseColor,
            out half3 specColor,
            out half oneMinusReflectivity)
        {
            half reflectance = smoothstep(0.0h, 1.0h, metallic);
            half unpaintedMetal = reflectance * saturate(1.0h - paintCoverage * 4.0h);
            half paintedMetal = reflectance * saturate(paintCoverage);
            half3 paintedSpecular = max(albedo * 0.45h, half3(0.08h, 0.08h, 0.08h));
            half3 unpaintedSpecular = saturate(albedo * 0.54h + half3(0.010h, 0.010h, 0.012h));
            half3 metalSpecular = lerp(unpaintedSpecular, paintedSpecular, saturate(paintCoverage));

            specColor = lerp(unity_ColorSpaceDielectricSpec.rgb, metalSpecular, reflectance);
            oneMinusReflectivity = 1.0h - max(max(specColor.r, specColor.g), specColor.b);
            half diffuseScale = lerp(oneMinusReflectivity, 0.055h, unpaintedMetal);
            diffuseScale = lerp(diffuseScale, 0.16h, paintedMetal);
            diffuseColor = albedo * diffuseScale;
        }

        half3 SampleRenderKitSpecular(
            UnityGIInput data,
            half smoothness,
            half3 normal,
            fixed3 albedo,
            half metallic,
            half paintCoverage,
            half occlusion)
        {
            half3 diffuseColor;
            half3 specColor;
            half oneMinusReflectivity;
            BuildKspPartSpecular(albedo, metallic, paintCoverage, diffuseColor, specColor, oneMinusReflectivity);
            Unity_GlossyEnvironmentData gloss = UnityGlossyEnvironmentSetup(smoothness, data.worldViewDir, normal, specColor);
            half perceptualRoughness = gloss.roughness * (1.7h - 0.7h * gloss.roughness);
            half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
            half3 reflection;

            half3 reflectionDir = TransformRenderKitReflection(gloss.reflUVW);

            #if defined(RK_OBSERVER_CUBEMAP)
                reflection = UNITY_SAMPLE_TEXCUBE_LOD(_ObserverCubemapTexture, reflectionDir, mip).rgb;
            #elif defined(RK_GALAXY_CUBEMAP)
                reflection = UNITY_SAMPLE_TEXCUBE_LOD(_GalaxyCubemapTexture, reflectionDir, mip).rgb;
            #else
                reflection = half3(0.0h, 0.0h, 0.0h);
            #endif

            return CompressReflection(max(reflection, 0.0h)) * occlusion;
        }

        half3 SampleRenderKitSpecularFromView(
            half3 viewDir,
            half3 normal,
            half smoothness,
            half occlusion)
        {
            half perceptualRoughness = SmoothnessToPerceptualRoughness(smoothness);
            perceptualRoughness = perceptualRoughness * (1.7h - 0.7h * perceptualRoughness);
            half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
            half3 reflectionDir = TransformRenderKitReflection(reflect(-viewDir, normal));
            half3 reflection;

            #if defined(RK_OBSERVER_CUBEMAP)
                reflection = UNITY_SAMPLE_TEXCUBE_LOD(_ObserverCubemapTexture, reflectionDir, mip).rgb;
            #elif defined(RK_GALAXY_CUBEMAP)
                reflection = UNITY_SAMPLE_TEXCUBE_LOD(_GalaxyCubemapTexture, reflectionDir, mip).rgb;
            #else
                reflection = half3(0.0h, 0.0h, 0.0h);
            #endif

            return CompressReflection(max(reflection, 0.0h)) * occlusion;
        }

        inline half4 LightingKSPPart(SurfaceOutputKSPPart s, float3 viewDir, UnityGI gi)
        {
            s.Normal = normalize(s.Normal);

            half oneMinusReflectivity;
            half3 specColor;
            BuildKspPartSpecular(s.Albedo, s.Metallic, s.PaintCoverage, s.Albedo, specColor, oneMinusReflectivity);

            half outputAlpha;
            s.Albedo = PreMultiplyAlpha(s.Albedo, s.Alpha, oneMinusReflectivity, outputAlpha);

            half4 color = UNITY_BRDF_PBS(
                s.Albedo,
                specColor,
                oneMinusReflectivity,
                s.Smoothness,
                s.Normal,
                viewDir,
                gi.light,
                gi.indirect);

            half unpaintedMetal = saturate(s.Metallic * (1.0h - s.PaintCoverage * 4.0h));
            half3 metalFloor = s.Albedo * 0.024h;
            color.rgb = max(color.rgb, metalFloor * unpaintedMetal * s.Occlusion);
            color.a = outputAlpha;
            return color;
        }

        inline half4 LightingKSPPart_Deferred(
            SurfaceOutputKSPPart s,
            float3 viewDir,
            UnityGI gi,
            out half4 outGBuffer0,
            out half4 outGBuffer1,
            out half4 outGBuffer2)
        {
            half oneMinusReflectivity;
            half3 specColor;
            BuildKspPartSpecular(s.Albedo, s.Metallic, s.PaintCoverage, s.Albedo, specColor, oneMinusReflectivity);
            half reflectionOcclusion = max(s.Occlusion, saturate(s.Metallic) * 0.85h);

            UnityStandardData data;
            data.diffuseColor = s.Albedo;
            data.occlusion = reflectionOcclusion;
            data.specularColor = specColor;
            data.smoothness = s.Smoothness;
            data.normalWorld = s.Normal;

            UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

            half unpaintedMetal = saturate(s.Metallic * (1.0h - s.PaintCoverage * 4.0h));

            #if defined(RK_OBSERVER_CUBEMAP) || defined(RK_GALAXY_CUBEMAP)
                half3 renderKitReflection = SampleRenderKitSpecularFromView(
                    viewDir,
                    s.Normal,
                    s.Smoothness,
                    reflectionOcclusion);
                half3 deferredReflection = renderKitReflection * specColor * saturate(_ReflectionIntensityMultiplier) * saturate(s.Metallic) * 0.12h;
                half3 metalFloor = s.Albedo * 0.022h * unpaintedMetal;
                return half4(s.Emission + max(deferredReflection, metalFloor), 1);
            #endif

            return half4(s.Emission + s.Albedo * 0.020h * unpaintedMetal, 1);
        }

        inline void LightingKSPPart_GI(SurfaceOutputKSPPart s, UnityGIInput data, inout UnityGI gi)
        {
            #if defined(UNITY_PASS_DEFERRED) && UNITY_ENABLE_REFLECTION_BUFFERS
                gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal);
            #else
                half3 diffuseColor;
                half3 specColor;
                half oneMinusReflectivity;
                BuildKspPartSpecular(s.Albedo, s.Metallic, s.PaintCoverage, diffuseColor, specColor, oneMinusReflectivity);
                Unity_GlossyEnvironmentData gloss = UnityGlossyEnvironmentSetup(s.Smoothness, data.worldViewDir, s.Normal, specColor);
                gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal, gloss);
            #endif

            #if (defined(RK_OBSERVER_CUBEMAP) || defined(RK_GALAXY_CUBEMAP)) && !(defined(UNITY_PASS_DEFERRED) && UNITY_ENABLE_REFLECTION_BUFFERS)
                half reflectionStrength = saturate(_ReflectionIntensityMultiplier) * saturate(s.Metallic) * 0.13h;
                half3 renderKitSpecular = SampleRenderKitSpecular(
                    data,
                    s.Smoothness,
                    s.Normal,
                    s.Albedo,
                    s.Metallic,
                    s.PaintCoverage,
                    s.Occlusion) * reflectionStrength;
                gi.indirect.specular = max(gi.indirect.specular, renderKitSpecular);
            #endif
        }

        void surf(Input IN, inout SurfaceOutputKSPPart o)
        {
            float2 mainUv = IN.uv_MainTex;
            float2 paintUv = IN.uv2_PaintMaskGlossMap;

            fixed4 albedoSample = SampleTex2DBias(_MainTex, mainUv) * _Color;
            fixed4 metalSmoothSample = SampleTex2DBias(_MetallicGlossMap, mainUv);
            fixed4 paintMask = SampleTex2DBias(_PaintMaskGlossMap, paintUv);

            fixed3 albedo = albedoSample.rgb;
            half metallic = saturate(metalSmoothSample.g * _Metallic);
            half smoothness = saturate(metalSmoothSample.a * _GlossMapScale);

            half paintCoverage = ApplyPaint(paintMask, albedo, metallic, smoothness);

            half paintResponse = saturate(paintCoverage);
            metallic = saturate(metallic * lerp(1.04h, 0.12h, paintResponse));
            half paintSmoothnessDamping = saturate(_PaintSmoothnessDamping * paintResponse);
            smoothness = saturate(smoothness * lerp(1.05h, 0.42h, paintSmoothnessDamping));
            half unpaintedMetal = saturate(metallic * (1.0h - paintCoverage * 4.0h));
            smoothness = lerp(smoothness, saturate(smoothness * 0.68h + 0.080h), unpaintedMetal * unpaintedMetal * 0.78h);

            half3 baseNormal = UnpackNormal(tex2D(_BumpMap, mainUv));
            half detailMask = tex2D(_DetailMask, mainUv).a;
            half3 detailNormal = UnpackDetailNormal(mainUv, detailMask);

            o.Albedo = albedo;
            o.Metallic = metallic;
            o.Smoothness = smoothness;
            o.Normal = BlendNormals(baseNormal, detailNormal);
            o.Occlusion = lerp(1.0h, tex2D(_OcclusionMap, mainUv).g, _OcclusionStrength);
            o.PaintCoverage = paintCoverage;

            half3 emission = tex2D(_EmissionMap, mainUv).rgb * _EmissionColor.rgb;

            #if defined(USE_TIME_OF_DAY)
                half sunDot = dot(WorldNormalVector(IN, o.Normal), _WorldSpaceLightPos0.xyz);
                half tod = smoothstep(_TimeOfDayDotMin, _TimeOfDayDotMax, sunDot);
                emission *= tod;
            #endif

            half rim = 1.0h - saturate(dot(normalize(IN.viewDir), o.Normal));
            emission += _RimColor.rgb * pow(rim, _RimFalloff) * _RimColor.a;

            o.Emission = emission;
            o.Alpha = albedoSample.a;
        }
        ENDCG
    }

    CustomEditor "PaintableShaderGUI"
    FallBack "Standard"
}
