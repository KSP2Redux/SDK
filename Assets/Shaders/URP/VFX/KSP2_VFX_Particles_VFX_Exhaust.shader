// URP port of KSP2/VFX/Particles/VFX_Exhaust, the Amplify-authored engine exhaust plume mesh.
// Math from ksp2-shaders/VFX_Particles_VFX_Exhaust/final (pass 1, ForwardBase), texture bindings from
// the AssetRipper export (the pixel program samples _NoiseTexture and _TracesTexture). The stock pass is
// purely emissive, so the port is a single unlit pass. The stock ForwardAdd pass wrote black with One One
// blending and is dropped.
Shader "KSP2/VFX/Particles/VFX_Exhaust"
{
    Properties
    {
        [Header(Main)] _Alpha ("Alpha", Range(0, 1)) = 0
        _ScrollSpeedX ("Scroll Speed X", Float) = 0
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0
        [Header(Color)] _ColorTintBoost ("Color Tint Boost", Range(0, 1)) = 0
        [HDR] _ColorTintStart ("Color Tint Start", Color) = (1,0,0,1)
        [HDR] _ColorTintMiddle ("Color Tint Middle", Color) = (0,1,0,0)
        [HDR] _ColorTintEnd ("Color Tint End", Color) = (0,0,1,1)
        _ColorTintOffset ("Color Tint Start Offset", Range(0, 1)) = 0
        _ColorTintFalloff ("Color Tint Start Gradient", Range(0, 1)) = 1
        _ColorTintMiddlePos ("Color Tint Middle Pos", Range(0, 1)) = 0.5
        _ColorTintEndOffset ("Color Tint End Offset", Range(0, 1)) = 0
        _ColorTintEndGradient ("Color Tint End Gradient", Range(0, 1)) = 1
        [Header(Noise)] _NoiseAmount ("Noise Amount", Range(0, 1)) = 1
        _NoiseStrength ("Noise Strength", Range(0.5, 5)) = 0
        _TextureOffsetX ("Texture Offset X", Float) = 0
        _TextureOffsetY ("Texture Offset Y", Float) = 0
        _TextureScaleX ("Texture Scale X", Float) = 1
        _TextureScaleY ("Texture Scale Y", Float) = 1
        [NoScaleOffset] _NoiseTexture ("NoiseTexture", 2D) = "white" {}
        [Header(Fresnel)] _TdotVScale ("TdotV Scale", Float) = 3
        _FresnelOuter ("Fresnel Outer", Range(0, 10)) = 10
        _FresnelOuterBeneath ("Fresnel Outer Beneath", Range(0, 10)) = 0
        _FresnelOuterErosionAmount ("Fresnel Outer Erosion Amount", Range(0, 1)) = 1
        _FresnelOuterErosionOffset ("Fresnel Outer Erosion Offset", Range(0, 1)) = 0.282353
        _FresnelOuterErosionFalloff ("Fresnel Outer Erosion Falloff", Range(0.001, 1)) = 0.8824706
        _FresnelInner ("Fresnel Inner", Range(0, 10)) = 10
        _FresnelInnerBeneath ("Fresnel Inner Beneath", Range(0, 10)) = 0
        [Header(Top Bottom Fade)] _TopGradientPosOffset ("Top Gradient Pos Offset", Range(0, 1)) = 0
        _TopGradientFalloff ("Top Gradient Falloff", Range(0, 1)) = 0.2439874
        _ErosionAmount ("Erosion Amount", Range(0, 1)) = 1
        _ErosionPosOffset ("Erosion Pos Offset", Range(-1, 1)) = 0.2945153
        _ErosionFalloffGradient ("Erosion Falloff Gradient", Range(0, 5)) = 0.2384171
        [Header(Vertex Displacement)] _VertexDispScale ("Vertex Disp Scale", Range(0, 10)) = 0
        _VertexDispContrast ("Vertex Disp Contrast", Range(0.5, 5)) = 0.5
        _VertexDispPosOffset ("Vertex Disp Pos Offset", Range(0, 1)) = 0
        _VertexDispFalloffGradient ("Vertex Disp Falloff Gradient", Range(0, 3)) = 0
        [NoScaleOffset] _DistortionTexture ("DistortionTexture", 2D) = "white" {}
        [Header(Exit Traces)] _TracesAmount ("Traces Amount", Range(0, 1)) = 0
        _TracesLength ("Traces Length", Range(1, 20)) = 1
        [IntRange] _TracesCount ("Traces Count", Range(0, 10)) = 3
        _TracesThickness ("Traces Thickness", Range(0.1, 4)) = 2
        _TracesStrength ("Traces Strength", Range(0, 5)) = 1
        _TracesTopPosOffset ("Traces Top Pos Offset", Range(0, 1)) = 0.282353
        _TracesTopFalloffGradient ("Traces Top Falloff Gradient", Range(0, 2)) = 0.25
        [NoScaleOffset] _TracesTexture ("Traces Texture", 2D) = "white" {}
        [Header(Camera Distance Fade)] _CameraDistanceFadeLength ("Camera Distance Fade Length", Range(0, 50)) = 50
        _CameraDistanceFalloff ("Camera Distance Falloff", Range(0.01, 2)) = 1
        _CameraDistanceTopGradient ("Camera Distance Top Gradient", Range(0, 1)) = 0
        [Header(Bending)] _AccelerationDir ("AccelerationDir", Vector) = (0,0,0,0)
        _AccelerationScaleFactor ("AccelerationScaleFactor", Float) = 0
        _BendCenterOffset ("BendCenterOffset", Float) = 0
        _BendRotationOffset ("BendRotationOffset", Float) = 0
        _BendCenterOffsetMultipler ("BendCenterOffsetMultipler", Float) = 0
        [HideInInspector] _texcoord ("", 2D) = "white" {}
        [HideInInspector] __dirty ("", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" "Queue" = "Transparent+0" "RenderType" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorTintStart;
            float4 _ColorTintMiddle;
            float4 _ColorTintEnd;
            float4 _AccelerationDir;
            float4 _texcoord_ST;
            float _Alpha;
            float _ScrollSpeedX;
            float _ScrollSpeedY;
            float _ColorTintBoost;
            float _ColorTintOffset;
            float _ColorTintFalloff;
            float _ColorTintMiddlePos;
            float _ColorTintEndOffset;
            float _ColorTintEndGradient;
            float _NoiseAmount;
            float _NoiseStrength;
            float _TextureOffsetX;
            float _TextureOffsetY;
            float _TextureScaleX;
            float _TextureScaleY;
            float _TdotVScale;
            float _FresnelOuter;
            float _FresnelOuterBeneath;
            float _FresnelOuterErosionAmount;
            float _FresnelOuterErosionOffset;
            float _FresnelOuterErosionFalloff;
            float _FresnelInner;
            float _FresnelInnerBeneath;
            float _TopGradientPosOffset;
            float _TopGradientFalloff;
            float _ErosionAmount;
            float _ErosionPosOffset;
            float _ErosionFalloffGradient;
            float _VertexDispScale;
            float _VertexDispContrast;
            float _VertexDispPosOffset;
            float _VertexDispFalloffGradient;
            float _TracesAmount;
            float _TracesLength;
            float _TracesCount;
            float _TracesThickness;
            float _TracesStrength;
            float _TracesTopPosOffset;
            float _TracesTopFalloffGradient;
            float _CameraDistanceFadeLength;
            float _CameraDistanceFalloff;
            float _CameraDistanceTopGradient;
            float _AccelerationScaleFactor;
            float _BendCenterOffset;
            float _BendRotationOffset;
            float _BendCenterOffsetMultipler;
            float __dirty;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "FORWARD"
            Tags { "IsEmissive" = "true" "Queue" = "Transparent+0" "RenderType" = "Transparent" }
            Blend SrcAlpha One, SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_DistortionTexture);  SAMPLER(sampler_DistortionTexture);
            TEXTURE2D(_NoiseTexture);       SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_TracesTexture);      SAMPLER(sampler_TracesTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            float Exhaust_Smooth(float t)
            {
                return t * t * (3.0 - 2.0 * t);
            }

            // Acceleration bending plus noise displacement along the normal, in object space.
            float3 ComputeVertexOffset(float3 normalOS, float2 uv)
            {
                float3 accelAbs = abs(_AccelerationDir.xyz);
                float3 accelSign = sign(_AccelerationDir.xyz);
                float2 bendForceXZ = ((1.0 - pow(accelAbs.xz + 1.0, -2.5)) * accelSign.xz) * 4.0 * _AccelerationScaleFactor;
                float sinBend = sin(3.1415927 * _BendRotationOffset);
                float cosBend = cos(3.1415927 * _BendRotationOffset);
                float bendAxisU = dot(bendForceXZ, float2(-sinBend, cosBend));
                float bendAxisV = dot(bendForceXZ, float2(cosBend, sinBend));
                float3 accelForce = ((1.0 - pow(accelAbs + 1.0, -2.5)) * accelSign) * 4.0;
                float bendBase = ((1.0 - uv.y) * length(accelForce)) * 0.01 + 1.0;
                float bendFactor = bendBase * bendBase;
                float bendQuad = bendFactor * bendFactor - bendFactor;
                float bendU = bendAxisU * bendQuad;
                float bendV = bendAxisV * bendQuad;
                float3 centrifugal = float3(bendU * bendU * sign(bendU), bendV * bendV * sign(bendV), 0.0);
                float3 bendCenterOffset = float3(
                    (bendAxisU * 0.16) * (bendAxisU * 0.16) * sign(bendAxisU) * _BendCenterOffsetMultipler * _BendCenterOffset,
                    (bendAxisV * 0.16) * (bendAxisV * 0.16) * sign(bendAxisV) * _BendCenterOffsetMultipler * _BendCenterOffset,
                    0.0);

                float2 dispUV = (_Time.yy * float2(_ScrollSpeedX, _ScrollSpeedY)) + (float2(_TextureScaleX, _TextureScaleY) * uv);
                float3 dispNormal = (SAMPLE_TEXTURE2D_LOD(_DistortionTexture, sampler_DistortionTexture, dispUV, 0).x
                    * (_VertexDispContrast - (1.0 - _VertexDispContrast)) + (1.0 - _VertexDispContrast)) * normalOS;
                float dispFadeT = saturate(((1.0 - uv.y) - _VertexDispPosOffset) * (1.0 / _VertexDispFalloffGradient));
                float3 dispWithFade = (dispNormal * _VertexDispScale) * Exhaust_Smooth(dispFadeT);

                return centrifugal + (dispWithFade + bendCenterOffset);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 displacedOS = ComputeVertexOffset(input.normalOS, input.uv0.xy) + input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(displacedOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv0.xy * _texcoord_ST.xy + _texcoord_ST.zw;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            float4 frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 uv = input.uv;
                float height = 1.0 - uv.y;

                // Exit traces: banded streaks along U, sampled through a 180 degree UV rotator.
                float2 traceSinUV = float2(abs(sin(((6.2831855 * _TracesCount) * uv.x) * 0.5)), height * _TracesLength) - 0.5;
                float2 traceUV = float2(dot(traceSinUV, float2(-1.0, 1.2246469e-16)), dot(traceSinUV, -float2(1.2246469e-16, 1.0))) + 0.5;
                float3 tracesSample = pow(max(sin(((6.2831855 * _TracesCount) * (1.0 - uv.x)) + 1.5707964), 0.0001), _TracesThickness)
                    * SAMPLE_TEXTURE2D(_TracesTexture, sampler_TracesTexture, traceUV).rgb;
                float traceTopFade = saturate((height - _TracesTopPosOffset) * (1.0 / _TracesTopFalloffGradient));
                float3 tracesWeighted = tracesSample * min(Exhaust_Smooth(traceTopFade), 1.0);

                // Two scrolling noise samples multiplied together.
                float2 noiseUV = (uv * float2(_TextureScaleX, _TextureScaleY)) + float2(_TextureOffsetX, _TextureOffsetY);
                float noiseSample = SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, (_Time.yy * float2(_ScrollSpeedX, _ScrollSpeedY)) + noiseUV).x
                    * SAMPLE_TEXTURE2D(_NoiseTexture, sampler_NoiseTexture, (float2(_Time.y, 1.3) * float2(-_ScrollSpeedX, _ScrollSpeedY * _Time.y)) + noiseUV).y;
                float noiseFactor = saturate(_NoiseAmount * (noiseSample - 1.0) + 1.0);

                // Start -> middle -> end colour tint along the height.
                float colorStartT = saturate(((1.0 - (height / _ColorTintMiddlePos)) - _ColorTintOffset) * (1.0 / (noiseFactor * _ColorTintFalloff)));
                float3 lowerColor = (_ColorTintMiddlePos >= height ? 1.0 : 0.0)
                    * (Exhaust_Smooth(colorStartT) * (_ColorTintStart.rgb - _ColorTintMiddle.rgb) + _ColorTintMiddle.rgb);
                float colorEndT = saturate((((height - _ColorTintMiddlePos) / (1.0 - _ColorTintMiddlePos)) - _ColorTintEndOffset) * (1.0 / (noiseFactor * _ColorTintEndGradient)));
                float3 upperColor = (height >= _ColorTintMiddlePos ? 1.0 : 0.0)
                    * (Exhaust_Smooth(colorEndT) * (_ColorTintEnd.rgb - _ColorTintMiddle.rgb) + _ColorTintMiddle.rgb);
                float3 tintColor = lowerColor + upperColor;
                float3 boostedTintColor = (tintColor * _ColorTintBoost) + tintColor;

                float3 tracesResult = tracesWeighted * boostedTintColor * _TracesAmount * _TracesStrength;

                float topFadeT = saturate((height - _TopGradientPosOffset) * (1.0 / _TopGradientFalloff));
                float topFadeWeight = min(Exhaust_Smooth(topFadeT), 1.0);
                float3 tracesTopFaded = tracesResult * topFadeWeight;

                // Noise erosion along the height.
                float noiseStrengthBlend = saturate(noiseSample * (_NoiseStrength - (1.0 - _NoiseStrength)) + (1.0 - _NoiseStrength));
                float erosionFactor = saturate(_ErosionAmount * ((noiseStrengthBlend - ((height - _ErosionPosOffset) / _ErosionFalloffGradient)) - 1.0) + 1.0);
                float3 tracesWithNoise = tracesTopFaded * erosionFactor * noiseFactor;

                // Camera distance fade, held off near the top of the mesh.
                float camDistTopT = saturate(height * (1.0 / _CameraDistanceTopGradient));
                float camDistFade = max(1.0 - Exhaust_Smooth(camDistTopT), 0.0)
                    * min(pow(length(input.positionWS - _WorldSpaceCameraPos.xyz) / _CameraDistanceFadeLength, _CameraDistanceFalloff), 1.0);

                float3 toCamera = _WorldSpaceCameraPos.xyz - input.positionWS;
                float3 viewDir = normalize(toCamera * rsqrt(max(dot(toCamera, toCamera), 0.001)));

                // How much the view runs along the object's Z axis (looking up the plume).
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 objectZ = float3(objectToWorld[0][2], objectToWorld[1][2], objectToWorld[2][2]);
                float tdotVPow = pow(abs(dot(normalize(toCamera), normalize(objectZ))), _TdotVScale);

                float3 worldNormal = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
                float ndotVClamped = min(abs(dot(viewDir, worldNormal)), 1.0);
                float ndotVSmooth = Exhaust_Smooth(ndotVClamped);

                // Outer fresnel with noise erosion, inner fresnel added on top.
                float fresnelOuterExp = tdotVPow * (_FresnelOuterBeneath - _FresnelOuter) + _FresnelOuter;
                float fresnelOuter = pow(ndotVSmooth, (camDistFade * -fresnelOuterExp) + fresnelOuterExp);
                float fresnelOuterEroded = ((camDistFade * -_FresnelOuterErosionAmount) + _FresnelOuterErosionAmount)
                    * (saturate(fresnelOuter - (noiseStrengthBlend - ((fresnelOuter - _FresnelOuterErosionOffset) / _FresnelOuterErosionFalloff))) - fresnelOuter)
                    + fresnelOuter;

                float3 tracesFresnelOut = tracesWithNoise * fresnelOuterEroded;
                float innerFresnel = pow(max(1.0 - ndotVSmooth, 0.001), tdotVPow * (_FresnelInnerBeneath - _FresnelInner) + _FresnelInner);
                float3 innerFresnelAdd = tracesFresnelOut + ((((innerFresnel * fresnelOuterEroded) * topFadeWeight) * erosionFactor) * noiseFactor) * boostedTintColor;
                float3 finalColor = saturate(innerFresnelAdd);

                return float4(MixFog(finalColor * _Alpha, input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}