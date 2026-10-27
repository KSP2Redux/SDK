// URP port of RenderKit/Particles/Standard Uber, the lit particle shader for smoke, dust and spray.
// It is Unity's Particles/Standard Surface with RenderKit lighting: wrap diffuse, back scatter, a cap on
// the light intensity, desaturated ambient and Unity's BRDF2 specular. Math from the per-variant
// decompile in SDK/Shaders/Particles (Pass02_FORWARD, Pass01_ShadowCaster). The unused GrabPass is
// dropped (no variant in the keyword pool samples _GrabTexture).
Shader "RenderKit/Particles/Standard Uber"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _MetallicGlossMap ("Metallic", 2D) = "white" {}
        [Gamma] _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _BumpScale ("Scale", Float) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _EmissionColor ("Color", Color) = (0,0,0,1)
        _EmissionMap ("Emission", 2D) = "white" {}
        _DistortionStrength ("Strength", Float) = 1
        _DistortionBlend ("Blend", Range(0, 1)) = 0.5
        _SoftParticlesNearFadeDistance ("Soft Particles Near Fade", Float) = 0
        _SoftParticlesFarFadeDistance ("Soft Particles Far Fade", Float) = 1
        _CameraNearFadeDistance ("Camera Near Fade", Float) = 1
        _CameraFarFadeDistance ("Camera Far Fade", Float) = 2
        _DiffuseWrapPercentage ("Diffuse Wrap Percentage", Range(0, 1)) = 0.5
        _ScatterPercentage ("Light Scatter Percentage", Range(0, 1)) = 0
        _LightIntensityPercentage ("Light Intensity Percentage", Range(0, 1)) = 1
        _DiffuseSaturation ("Diffuse Saturation", Range(0, 1)) = 1
        [HideInInspector] _Mode ("__mode", Float) = 0
        [HideInInspector] _FlipbookMode ("__flipbookmode", Float) = 0
        [HideInInspector] _LightingEnabled ("__lightingenabled", Float) = 1
        [HideInInspector] _DistortionEnabled ("__distortionenabled", Float) = 0
        [HideInInspector] _EmissionEnabled ("__emissionenabled", Float) = 0
        [HideInInspector] _BlendOp ("__blendop", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _Cull ("__cull", Float) = 2
        [HideInInspector] _SoftParticlesEnabled ("__softparticlesenabled", Float) = 0
        [HideInInspector] _CameraFadingEnabled ("__camerafadingenabled", Float) = 0
        [HideInInspector] _SoftParticleFadeParams ("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadeParams ("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionStrengthScaled ("__distortionstrengthscaled", Float) = 0
    }

    SubShader
    {
        // No Queue tag, as in the stock shader: the material GUI writes the queue (3000 for the fade modes).
        Tags { "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PerformanceChecks" = "False" "PreviewType" = "Plane" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _MetallicGlossMap_ST;
            float4 _BumpMap_ST;
            float4 _EmissionMap_ST;
            float4 _Color;
            float4 _EmissionColor;
            float4 _SoftParticleFadeParams;
            float4 _CameraFadeParams;
            float  _Cutoff;
            float  _Metallic;
            float  _Glossiness;
            float  _BumpScale;
            float  _DistortionStrength;
            float  _DistortionBlend;
            float  _SoftParticlesNearFadeDistance;
            float  _SoftParticlesFarFadeDistance;
            float  _CameraNearFadeDistance;
            float  _CameraFarFadeDistance;
            float  _DiffuseWrapPercentage;
            float  _ScatterPercentage;
            float  _LightIntensityPercentage;
            float  _DiffuseSaturation;
            float  _Mode;
            float  _FlipbookMode;
            float  _LightingEnabled;
            float  _DistortionEnabled;
            float  _EmissionEnabled;
            float  _BlendOp;
            float  _SrcBlend;
            float  _DstBlend;
            float  _ZWrite;
            float  _Cull;
            float  _SoftParticlesEnabled;
            float  _CameraFadingEnabled;
            float  _DistortionStrengthScaled;
        CBUFFER_END

        // Particle system mesh instancing (stock PROCEDURAL_INSTANCING_ON): per-particle transform, color
        // and sheet frame come from unity_ParticleInstanceData.
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ParticlesInstancing.hlsl"

        float4 RK_ParticleColor(float4 color)
        {
        #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
            UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
            color = lerp(float4(1.0, 1.0, 1.0, 1.0), color, unity_ParticleUseMeshColors);
            color *= UnpackFromR8G8B8A8(data.color);
        #endif
            return color;
        }

        // uv, and (uv2, blend) for flipbook blending. Instanced draws build both frames from the sheet data.
        void RK_ParticleTexcoords(float4 texcoords, float texcoordBlend, out float2 uv, out float3 uv2AndBlend)
        {
        #if defined(UNITY_PARTICLE_INSTANCING_ENABLED)
            uv = texcoords.xy;
            uv2AndBlend = float3(texcoords.xy, 0.0);
            if (unity_ParticleUVShiftData.x != 0.0)
            {
                UNITY_PARTICLE_INSTANCE_DATA data = unity_ParticleInstanceData[unity_InstanceID];
                float tilesX = unity_ParticleUVShiftData.y;
                float2 animScale = unity_ParticleUVShiftData.zw;
                float frame0 = floor(data.animFrame);
                float row0 = floor(frame0 / tilesX);
                uv = texcoords.xy * animScale + float2(floor(frame0 - row0 * tilesX) * animScale.x, (1.0 - animScale.y) - row0 * animScale.y);
                float frame1 = floor(data.animFrame + 1.0);
                float row1 = floor(frame1 / tilesX);
                uv2AndBlend.xy = texcoords.xy * animScale + float2(floor(frame1 - row1 * tilesX) * animScale.x, (1.0 - animScale.y) - row1 * animScale.y);
                uv2AndBlend.z = frac(data.animFrame);
            }
        #else
            uv = texcoords.xy;
            uv2AndBlend = float3(texcoords.zw, texcoordBlend);
        #endif
        }

        // Texture sample with the flipbook frame blend (_REQUIRE_UV2).
        float4 RK_SampleFlipbook(TEXTURE2D_PARAM(tex, samp), float2 uv, float3 uv2AndBlend)
        {
            float4 c = SAMPLE_TEXTURE2D(tex, samp, uv);
        #if defined(_REQUIRE_UV2)
            c = lerp(c, SAMPLE_TEXTURE2D(tex, samp, uv2AndBlend.xy), uv2AndBlend.z);
        #endif
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local_fragment _ _ALPHABLEND_ON
            #pragma multi_compile_local_fragment _ _EMISSION
            #pragma multi_compile_local _ _FADING_ON
            #pragma multi_compile_local _ _NORMALMAP
            #pragma multi_compile_local _ _REQUIRE_UV2
            #pragma multi_compile_fragment _ _LIGHT_LAYERS

            // No shadow keywords: stock particles render in the transparent queue, where the built-in pipeline
            // gave them no shadows.
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2LegacyLights.hlsl"
            #include "Packages/ksp2redux.sdk/Assets/Shaders/URP/Include/KSP2Eclipse.hlsl"
            #if defined(_FADING_ON)
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #endif

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS    : POSITION;
                float3 normalOS      : NORMAL;
                float4 color         : COLOR;
            #if defined(_REQUIRE_UV2)
                float4 texcoords     : TEXCOORD0;
                float  texcoordBlend : TEXCOORD1;
            #else
                float2 texcoords     : TEXCOORD0;
            #endif
            #if defined(_NORMALMAP)
                float4 tangentOS     : TANGENT;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS        : SV_POSITION;
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
            #if defined(_REQUIRE_UV2)
                float3 texcoord2AndBlend : TEXCOORD1;
            #endif
                float3 positionWS        : TEXCOORD2;
                float3 normalWS          : TEXCOORD3;
            #if defined(_NORMALMAP)
                float4 tangentWS         : TEXCOORD4; // w = bitangent sign
            #endif
            #if defined(_FADING_ON)
                float4 projectedPosition : TEXCOORD5; // screen position, z = eye depth
            #endif
                float  fogFactor         : TEXCOORD6;
            };

            // RenderKit light: wrap diffuse plus back scatter, light color capped at 2 * intensity
            // percentage, BRDF2 specular. diffColor and specColor come from the metallic setup.
            float3 RK_ShadeLight(Light light, float3 N, float3 V, float3 diffColor, float3 specColor, float perceptualRoughness)
            {
                float3 L = light.direction;
                float3 H = (V + L) * rsqrt(max(dot(V + L, V + L), 0.001));
                float wrapOne = _DiffuseWrapPercentage + 1.0;
                float wrap = (dot(N, L) + _DiffuseWrapPercentage) / (wrapOne * wrapOne);
                float scatter = (dot(N, -L) + 0.1) * (1.0 / 1.21);
                float nl = saturate(saturate(scatter) * _ScatterPercentage + saturate(wrap));
                float3 radiance = light.color * (light.distanceAttenuation * light.shadowAttenuation);
                float3 lightTerm = nl * min(radiance, max(_LightIntensityPercentage * 2.0, 0.0));

                float nh = saturate(dot(N, H));
                float lh = saturate(dot(L, H));
                float roughness = perceptualRoughness * perceptualRoughness;
                float a2 = roughness * roughness;
                float d = nh * nh * (a2 - 1.0) + 1.00001;
                float specularTerm = a2 / (max(lh * lh, 0.1) * (roughness + 0.5) * (d * d) * 4.0);
                return (diffColor + specularTerm * specColor) * lightTerm;
            }

            // URP main light and Forward+ lights through RK_ShadeLight (the KSP2_DirectLighting loop).
            float3 RK_DirectLighting(float3 positionWS, float4 positionCS, float3 N, float3 V, float2 screenUV,
                                     float3 diffColor, float3 specColor, float perceptualRoughness)
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.positionCS = positionCS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.normalizedScreenSpaceUV = screenUV;
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 shadowMask = half4(1, 1, 1, 1);
                AmbientOcclusionFactor aoFactor = (AmbientOcclusionFactor)0;
                aoFactor.directAmbientOcclusion = 1;
                aoFactor.indirectAmbientOcclusion = 1;
                uint meshRenderingLayers = GetMeshRenderingLayer();

                float3 color = 0;
                Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
                // The local sun leaves out other bodies' eclipses, which each particle takes itself.
                mainLight.shadowAttenuation *= KSP2_EclipseLocalSunFactor(positionWS);
            #ifdef _LIGHT_LAYERS
                if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
            #endif
                {
                    color += RK_ShadeLight(mainLight, N, V, diffColor, specColor, perceptualRoughness);
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
                        color += RK_ShadeLight(light, N, V, diffColor, specColor, perceptualRoughness);
                    }
                }
            #endif
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light light = KSP2_GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
            #ifdef _LIGHT_LAYERS
                    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
            #endif
                    {
                        color += RK_ShadeLight(light, N, V, diffColor, specColor, perceptualRoughness);
                    }
                LIGHT_LOOP_END
            #endif
                return color;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                VertexPositionInputs positionInputs = GetParticleVertexPositionInputs(input.positionOS.xyz);
            #if defined(_NORMALMAP)
                VertexNormalInputs normalInputs = GetParticleVertexNormalInputs(input.normalOS, input.tangentOS);
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            #else
                VertexNormalInputs normalInputs = GetParticleVertexNormalInputs(input.normalOS);
            #endif
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = RK_ParticleColor(input.color);

                float3 uv2AndBlend;
            #if defined(_REQUIRE_UV2)
                // Flipbook blending: raw frame UVs, no _MainTex tiling (as the stock shader).
                RK_ParticleTexcoords(input.texcoords, input.texcoordBlend, output.texcoord, uv2AndBlend);
                output.texcoord2AndBlend = uv2AndBlend;
            #else
                RK_ParticleTexcoords(input.texcoords.xyxy, 0.0, output.texcoord, uv2AndBlend);
                output.texcoord = TRANSFORM_TEX(output.texcoord, _MainTex);
            #endif

            #if defined(_FADING_ON)
                output.projectedPosition = ComputeScreenPos(positionInputs.positionCS);
                output.projectedPosition.z = -positionInputs.positionVS.z;
            #endif
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
            #if defined(_REQUIRE_UV2)
                float3 uv2AndBlend = input.texcoord2AndBlend;
            #else
                float3 uv2AndBlend = 0;
            #endif
                float2 uv = input.texcoord;

                float4 albedo = RK_SampleFlipbook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), uv, uv2AndBlend) * _Color;
                albedo *= input.color;
                float alpha = albedo.a;

                // Soft particles and camera fade. Soft particles follow the material: the GUI zeroes
                // _SoftParticleFadeParams when they are off (stock also needed the BRP SOFTPARTICLES_ON).
                float fade = 1.0;
            #if defined(_FADING_ON)
                if (_SoftParticleFadeParams.x > 0.0 || _SoftParticleFadeParams.y > 0.0)
                {
                    float2 depthUV = input.projectedPosition.xy / input.projectedPosition.w;
                    float sceneZ = LinearEyeDepth(SampleSceneDepth(depthUV), _ZBufferParams);
                    fade = saturate(_SoftParticleFadeParams.y * ((sceneZ - _SoftParticleFadeParams.x) - input.projectedPosition.z));
                }
                fade *= saturate((input.projectedPosition.z - _CameraFadeParams.x) * _CameraFadeParams.y);
                alpha *= fade;
            #endif

                float3 geomNormal = input.normalWS;
            #if defined(_NORMALMAP)
                float4 packedNormal = RK_SampleFlipbook(TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), uv, uv2AndBlend);
                float2 nxy = (float2(packedNormal.a * packedNormal.r, packedNormal.g) * 2.0 - 1.0) * _BumpScale;
                float3 normalTS = normalize(float3(nxy, sqrt(1.0 - min(dot(nxy, nxy), 1.0))));
                float3 tangent = input.tangentWS.xyz;
                float3 bitangent = cross(geomNormal, tangent) * input.tangentWS.w;
                float3 N = normalize(normalTS.x * tangent + normalTS.y * bitangent + normalTS.z * geomNormal);
            #else
                float3 N = normalize(geomNormal);
            #endif
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Metallic setup (BRP DiffuseAndSpecularFromMetallic).
                float perceptualRoughness = 1.0 - _Glossiness;
                float roughness = perceptualRoughness * perceptualRoughness;
                float3 specColor = lerp(float3(0.04, 0.04, 0.04), albedo.rgb, _Metallic);
                float oneMinusReflectivity = 0.96 - _Metallic * 0.96;
                float3 diffColor = albedo.rgb * oneMinusReflectivity;

                // Ambient: SH, desaturated toward its luminance by _DiffuseSaturation.
                float3 ambient = max(SampleSH(N), 0.0);
                float ambientLum = dot(ambient, float3(0.2126729, 0.7151522, 0.0721750));
                float3 giDiffuse = lerp(ambientLum.xxx, ambient, _DiffuseSaturation);

                // Reflection probe at Unity's roughness mip, BRDF2 surface reduction and fast fresnel.
                float3 envSpecular = GlossyEnvironmentReflection(reflect(-V, N), input.positionWS, perceptualRoughness, 1.0, screenUV);
                float surfaceReduction = 1.0 - roughness * perceptualRoughness * (0.6 - 0.08 * perceptualRoughness);
                float grazingTerm = saturate(_Glossiness + (1.0 - oneMinusReflectivity));
                float3 fresnel = lerp(specColor, grazingTerm.xxx, Pow4(1.0 - saturate(dot(N, V))));

                float3 color = diffColor * giDiffuse + envSpecular * surfaceReduction * fresnel;
                color += RK_DirectLighting(input.positionWS, input.positionCS, N, V, screenUV, diffColor, specColor, perceptualRoughness);

            #if defined(_EMISSION)
                float3 emission = RK_SampleFlipbook(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), uv, uv2AndBlend).rgb * fade;
                color += emission * _EmissionColor.rgb;
            #endif

                color = MixFog(color, input.fogFactor);
            #if defined(_ALPHABLEND_ON)
                return float4(color, alpha);
            #else
                return float4(color, 1.0);
            #endif
            }
            ENDHLSL
        }

        // The stock ShadowCaster is kept: a stock renderer (smoke_explosion, exp_atm_03) casts shadows
        // with this material. Fade materials cast dithered shadows from the texture and vertex alpha.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_local _ _ALPHABLEND_ON
            #pragma multi_compile_local _ _REQUIRE_UV2
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS    : POSITION;
                float3 normalOS      : NORMAL;
                float4 color         : COLOR;
            #if defined(_REQUIRE_UV2)
                float4 texcoords     : TEXCOORD0;
                float  texcoordBlend : TEXCOORD1;
            #else
                float2 texcoords     : TEXCOORD0;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionCS        : SV_POSITION;
            #if defined(_ALPHABLEND_ON)
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
                float3 texcoord2AndBlend : TEXCOORD1;
            #endif
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 positionWS = GetParticleVertexPositionInputs(input.positionOS.xyz).positionWS;
                float3 normalWS = GetParticleVertexNormalInputs(input.normalOS).normalWS;
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));

            #if defined(_ALPHABLEND_ON)
                output.color = RK_ParticleColor(input.color);
                float3 uv2AndBlend;
            #if defined(_REQUIRE_UV2)
                RK_ParticleTexcoords(input.texcoords, input.texcoordBlend, output.texcoord, uv2AndBlend);
            #else
                RK_ParticleTexcoords(input.texcoords.xyxy, 0.0, output.texcoord, uv2AndBlend);
                output.texcoord = TRANSFORM_TEX(output.texcoord, _MainTex);
            #endif
                output.texcoord2AndBlend = uv2AndBlend;
            #endif
                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
            #if defined(_ALPHABLEND_ON)
                // Stock used _DitherMaskLOD (16 coverage levels on a 4x4 tile): keep floor(alpha * 15) of
                // 16 pixels, ordered by a 4x4 Bayer matrix.
                static const float kBayer4x4[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                float alpha = RK_SampleFlipbook(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), input.texcoord, input.texcoord2AndBlend).a * input.color.a;
                uint2 pixel = uint2(input.positionCS.xy) & 3u;
                clip(floor(alpha * 15.0) - kBayer4x4[pixel.y * 4u + pixel.x] - 0.5);
            #endif
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}