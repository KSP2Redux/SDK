// URP replacement for Unity's built-in "Particles/Standard Unlit", which stock KSP2 particle
// materials reference through the built-in shader bundle. Name, properties and keywords match
// Unity 2022.3; the math follows UnityStandardParticles.cginc (fragParticleUnlit) and
// UnityStandardParticleShadow.cginc. GrabPass distortion samples URP's opaque texture, soft
// particles read _CameraDepthTexture. Soft particles are always available (Unity gated them on
// the global SOFTPARTICLES_ON quality keyword, which the project has on); a material still
// enables them through _FADING_ON and _SoftParticleFadeParams like the original.
// _NORMALMAP and EFFECT_BUMP (no stock material uses them) are dynamic branches.
Shader "Particles/Standard Unlit"
{
    Properties
    {
        _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DistortionStrength("Strength", Float) = 1.0
        _DistortionBlend("Blend", Range(0.0, 1.0)) = 0.5

        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0.0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 1.0
        _CameraNearFadeDistance("Camera Near Fade", Float) = 1.0
        _CameraFarFadeDistance("Camera Far Fade", Float) = 2.0

        // Hidden properties, written by Unity's StandardParticlesShaderGUI.
        [HideInInspector] _Mode ("__mode", Float) = 0.0
        [HideInInspector] _ColorMode ("__colormode", Float) = 0.0
        [HideInInspector] _FlipbookMode ("__flipbookmode", Float) = 0.0
        [HideInInspector] _LightingEnabled ("__lightingenabled", Float) = 0.0
        [HideInInspector] _DistortionEnabled ("__distortionenabled", Float) = 0.0
        [HideInInspector] _EmissionEnabled ("__emissionenabled", Float) = 0.0
        [HideInInspector] _BlendOp ("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend ("__src", Float) = 1.0
        [HideInInspector] _DstBlend ("__dst", Float) = 0.0
        [HideInInspector] _ZWrite ("__zw", Float) = 1.0
        [HideInInspector] _Cull ("__cull", Float) = 2.0
        [HideInInspector] _SoftParticlesEnabled ("__softparticlesenabled", Float) = 0.0
        [HideInInspector] _CameraFadingEnabled ("__camerafadingenabled", Float) = 0.0
        [HideInInspector] _SoftParticleFadeParams ("__softparticlefadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _CameraFadeParams ("__camerafadeparams", Vector) = (0,0,0,0)
        [HideInInspector] _ColorAddSubDiff ("__coloraddsubdiff", Vector) = (0,0,0,0)
        [HideInInspector] _DistortionStrengthScaled ("__distortionstrengthscaled", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" "PerformanceChecks" = "False" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BumpMap_ST;
            float4 _EmissionMap_ST;
            float4 _Color;
            float4 _EmissionColor;
            float4 _SoftParticleFadeParams;
            float4 _CameraFadeParams;
            float4 _ColorAddSubDiff;
            float  _Cutoff;
            float  _BumpScale;
            float  _DistortionStrength;
            float  _DistortionBlend;
            float  _SoftParticlesNearFadeDistance;
            float  _SoftParticlesFarFadeDistance;
            float  _CameraNearFadeDistance;
            float  _CameraFarFadeDistance;
            float  _Mode;
            float  _ColorMode;
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

        // Vertex input. _REQUIRE_UV2 is flipbook blending: two UV sets in TEXCOORD0 and the blend in TEXCOORD1.
        struct ParticleAttributes
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
        };

        // Unity's vertTexcoord: the main UV is only scaled and offset when flipbook blending is off.
        void Particle_Texcoords(ParticleAttributes input, out float2 texcoord, out float3 texcoord2AndBlend)
        {
        #if defined(_REQUIRE_UV2)
            texcoord = input.texcoords.xy;
            texcoord2AndBlend = float3(input.texcoords.zw, input.texcoordBlend);
        #else
            texcoord = TRANSFORM_TEX(input.texcoords.xy, _MainTex);
            texcoord2AndBlend = 0.0;
        #endif
        }

        // Unity's readTexture: blends the two flipbook frames when flipbook blending is on.
        #if defined(_REQUIRE_UV2)
            #define PARTICLE_READ_TEXTURE(tex, samp, uv, uv2AndBlend) lerp(SAMPLE_TEXTURE2D(tex, samp, uv), SAMPLE_TEXTURE2D(tex, samp, (uv2AndBlend).xy), (uv2AndBlend).z)
        #else
            #define PARTICLE_READ_TEXTURE(tex, samp, uv, uv2AndBlend) SAMPLE_TEXTURE2D(tex, samp, uv)
        #endif

        // 4x4 ordered dither threshold. Stands in for Unity's 4x4x16 _DitherMaskLOD in the
        // shadows of alpha-blended particles.
        float Particle_DitherThreshold(float2 pixel)
        {
            static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
            uint2 p = uint2(pixel) & 3u;
            return (bayer[p.y * 4u + p.x] + 1.0) / 17.0;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            BlendOp [_BlendOp]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            ColorMask RGB

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ParticleUnlitVert
            #pragma fragment ParticleUnlitFrag

            #pragma multi_compile_local_fragment _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma multi_compile_local_fragment _ _COLOROVERLAY_ON _COLORCOLOR_ON _COLORADDSUBDIFF_ON
            #pragma multi_compile_local _ _REQUIRE_UV2
            #pragma multi_compile_local_fragment _ _FADING_ON
            #pragma multi_compile_local_fragment _ _EMISSION
            #pragma dynamic_branch_local_fragment _ _NORMALMAP
            #pragma dynamic_branch_local_fragment _ EFFECT_BUMP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);

            struct Varyings
            {
                float4 positionCS        : SV_POSITION;
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
                float3 texcoord2AndBlend : TEXCOORD1;
                float4 projectedPosition : TEXCOORD2; // xy, w screen position (ComputeScreenPos), z eye depth
                float  fogFactor         : TEXCOORD3;
            };

            // Alpha-only fading, or the whole color in premultiplied mode (Unity's ALBEDO_MUL).
            #if defined(_ALPHAPREMULTIPLY_ON)
                #define PARTICLE_FADE(albedo, fade) albedo *= (fade)
            #else
                #define PARTICLE_FADE(albedo, fade) albedo.a *= (fade)
            #endif

            float3 Particle_RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 P = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 Q = lerp(float4(P.xyw, c.r), float4(c.r, P.yzx), step(P.x, c.r));
                float D = Q.x - min(Q.w, Q.y);
                float E = 1e-4;
                return float3(abs(Q.z + (Q.w - Q.y) / (6.0 * D + E)), D / (Q.x + E), Q.x);
            }

            float3 Particle_HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 P = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(P - K.xxx), c.y);
            }

            Varyings ParticleUnlitVert(ParticleAttributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.color = input.color;
                Particle_Texcoords(input, output.texcoord, output.texcoord2AndBlend);
                output.projectedPosition = float4(positionInputs.positionNDC.xy, -positionInputs.positionVS.z, positionInputs.positionNDC.w);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float4 ParticleUnlitFrag(Varyings input) : SV_Target
            {
                float4 albedo = PARTICLE_READ_TEXTURE(_MainTex, sampler_MainTex, input.texcoord, input.texcoord2AndBlend);
                albedo *= _Color;

                // Unity's fragColorMode.
            #if defined(_COLOROVERLAY_ON)
                albedo.rgb = lerp(1.0 - 2.0 * (1.0 - albedo.rgb) * (1.0 - input.color.rgb), 2.0 * albedo.rgb * input.color.rgb, step(albedo.rgb, 0.5));
                albedo.a *= input.color.a;
            #elif defined(_COLORCOLOR_ON)
                float3 aHSV = Particle_RGBtoHSV(albedo.rgb);
                float3 bHSV = Particle_RGBtoHSV(input.color.rgb);
                albedo = float4(Particle_HSVtoRGB(float3(bHSV.x, bHSV.y, aHSV.z)), albedo.a * input.color.a);
            #elif defined(_COLORADDSUBDIFF_ON)
                albedo.rgb = albedo.rgb + input.color.rgb * _ColorAddSubDiff.x;
                albedo.rgb = lerp(albedo.rgb, abs(albedo.rgb), _ColorAddSubDiff.y);
                albedo.a *= input.color.a;
            #else
                albedo *= input.color;
            #endif

                // Unity's fragSoftParticles and fragCameraFading.
                float softParticlesFade = 1.0;
                float cameraFade = 1.0;
            #if defined(_FADING_ON)
                if (_SoftParticleFadeParams.x > 0.0 || _SoftParticleFadeParams.y > 0.0)
                {
                    float rawDepth = SampleSceneDepth(input.projectedPosition.xy / input.projectedPosition.w);
                    float sceneZ = (unity_OrthoParams.w == 0.0) ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
                    softParticlesFade = saturate(_SoftParticleFadeParams.y * ((sceneZ - _SoftParticleFadeParams.x) - input.projectedPosition.z));
                    PARTICLE_FADE(albedo, softParticlesFade);
                }
                cameraFade = saturate((input.projectedPosition.z - _CameraFadeParams.x) * _CameraFadeParams.y);
                PARTICLE_FADE(albedo, cameraFade);
            #endif

                float3 normalTS = float3(0.0, 0.0, 1.0);
                if (_NORMALMAP)
                {
                    normalTS = normalize(UnpackNormalScale(PARTICLE_READ_TEXTURE(_BumpMap, sampler_BumpMap, input.texcoord, input.texcoord2AndBlend), _BumpScale));
                }

            #if defined(_EMISSION)
                float3 emission = PARTICLE_READ_TEXTURE(_EmissionMap, sampler_EmissionMap, input.texcoord, input.texcoord2AndBlend).rgb;
            #else
                float3 emission = 0.0;
            #endif

                // Unity's fragDistortion, with the opaque texture standing in for the GrabPass.
                if (EFFECT_BUMP)
                {
                    float2 grabUV = (input.projectedPosition.xy + normalTS.xy * _DistortionStrengthScaled * albedo.a) / input.projectedPosition.w;
                    float3 grabPass = SampleSceneColor(grabUV);
                    albedo.rgb = lerp(grabPass, albedo.rgb, saturate(albedo.a - _DistortionBlend));
                }

                float4 result = albedo;
            #if defined(_ALPHAMODULATE_ON)
                result.rgb = lerp(float3(1.0, 1.0, 1.0), albedo.rgb, albedo.a);
            #endif
                result.rgb += emission * _EmissionColor.rgb * cameraFade * softParticlesFade;
            #if !defined(_ALPHABLEND_ON) && !defined(_ALPHAPREMULTIPLY_ON)
                result.a = 1.0;
            #endif
            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff + 0.0001);
            #endif

                // Unity 2022.3 fog: modulate fogs to white, additive (Fade keyword with One as
                // destination) fogs to black, everything else to the fog color.
            #if defined(_ALPHAMODULATE_ON)
                result.rgb = MixFogColor(result.rgb, float3(1.0, 1.0, 1.0), input.fogFactor);
            #elif defined(_ALPHABLEND_ON)
                if (_DstBlend == 1.0)
                    result.rgb = MixFogColor(result.rgb, float3(0.0, 0.0, 0.0), input.fogFactor);
                else
                    result.rgb = MixFog(result.rgb, input.fogFactor);
            #else
                result.rgb = MixFog(result.rgb, input.fogFactor);
            #endif
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            BlendOp Add
            Blend One Zero
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ParticleShadowVert
            #pragma fragment ParticleShadowFrag

            #pragma multi_compile_local_fragment _ _ALPHATEST_ON _ALPHABLEND_ON _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma multi_compile_local _ _REQUIRE_UV2
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            float3 _LightDirection;
            float3 _LightPosition;

            struct Varyings
            {
                float4 positionCS        : SV_POSITION;
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
                float3 texcoord2AndBlend : TEXCOORD1;
            };

            Varyings ParticleShadowVert(ParticleAttributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                output.color = input.color;
                Particle_Texcoords(input, output.texcoord, output.texcoord2AndBlend);
                return output;
            }

            // Unity's fragParticleShadowCaster: texture alpha times vertex alpha, cutoff clip for
            // Cutout, dithered for Fade and Transparent. The unlit shader has no metallic, so the
            // premultiplied path uses Unity's dielectric reflectivity.
            half4 ParticleShadowFrag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
                float alpha = PARTICLE_READ_TEXTURE(_MainTex, sampler_MainTex, input.texcoord, input.texcoord2AndBlend).a * input.color.a;
                #if defined(_ALPHATEST_ON)
                clip(alpha - _Cutoff);
                #else
                    #if defined(_ALPHAPREMULTIPLY_ON)
                    alpha = 0.04 + alpha * 0.96;
                    #endif
                clip(alpha - Particle_DitherThreshold(input.positionCS.xy));
                #endif
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ParticleDepthVert
            #pragma fragment ParticleDepthFrag

            #pragma multi_compile_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_local _ _REQUIRE_UV2

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            struct Varyings
            {
                float4 positionCS        : SV_POSITION;
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
                float3 texcoord2AndBlend : TEXCOORD1;
                float3 normalWS          : TEXCOORD2;
            };

            Varyings ParticleDepthVert(ParticleAttributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                Particle_Texcoords(input, output.texcoord, output.texcoord2AndBlend);
                return output;
            }

            // Clips like the forward pass (every color mode multiplies the alpha by the vertex alpha).
            half4 ParticleDepthFrag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                float alpha = PARTICLE_READ_TEXTURE(_MainTex, sampler_MainTex, input.texcoord, input.texcoord2AndBlend).a * _Color.a * input.color.a;
                clip(alpha - _Cutoff + 0.0001);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ParticleDepthVert
            #pragma fragment ParticleDepthNormalsFrag

            #pragma multi_compile_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_local _ _REQUIRE_UV2

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            struct Varyings
            {
                float4 positionCS        : SV_POSITION;
                float4 color             : COLOR;
                float2 texcoord          : TEXCOORD0;
                float3 texcoord2AndBlend : TEXCOORD1;
                float3 normalWS          : TEXCOORD2;
            };

            Varyings ParticleDepthVert(ParticleAttributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                Particle_Texcoords(input, output.texcoord, output.texcoord2AndBlend);
                return output;
            }

            half4 ParticleDepthNormalsFrag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                float alpha = PARTICLE_READ_TEXTURE(_MainTex, sampler_MainTex, input.texcoord, input.texcoord2AndBlend).a * _Color.a * input.color.a;
                clip(alpha - _Cutoff + 0.0001);
            #endif
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "StandardParticlesShaderGUI"
}