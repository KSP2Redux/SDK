// URP port of KSP2/VFX/Particles/VFX_Exhaust_HeatRefraction, the heat haze mesh behind engine plumes.
// Math from the AssetRipper export (the ksp2-shaders Reference copy is a stub). The stock shader used a
// GrabPass in the Overlay queue and wrote the refracted _GrabTexture back with no blending, so it refracted
// everything drawn before it, transparents included. URP has no GrabPass: the pass is tagged
// KSP2HeatRefraction, which URP's own passes skip, and HeatRefractionRenderHook draws it after the
// transparents with a copy of the frame as _HeatRefractionGrabTexture.
Shader "KSP2/VFX/Particles/VFX_Exhaust_HeatRefraction"
{
    Properties
    {
        [Header(Main)] _ScrollSpeedX ("Scroll Speed X", Float) = 0
        _ScrollSpeedY ("Scroll Speed Y", Float) = 0
        [Header(Refraction)] _RefractionAmount ("Refraction Amount", Range(0, 0.5)) = 0
        _BottomRefractionPosOffset ("Bottom Refraction Pos Offset", Range(0, 1)) = 0
        _BottomRefractionFalloffGradient ("Bottom Refraction Falloff Gradient", Range(0, 5)) = 0
        _TextureScaleX ("Texture Scale X", Float) = 1
        _TextureScaleY ("Texture Scale Y", Float) = 1
        [Normal] _RefractionTex ("RefractionTex", 2D) = "bump" {}
        [Header(Vertex Displacement)] _VertexDispScale ("Vertex Disp Scale", Range(0, 10)) = 0
        _VertexDispContrast ("Vertex Disp Contrast", Range(0.5, 5)) = 0
        _VertexDispPosOffset ("Vertex Disp Pos Offset", Range(0, 1)) = 0
        _VertexDispFalloffGradient ("Vertex Disp Falloff Gradient", Range(0, 3)) = 0
        [Header(Bending)] _AccelerationDir ("AccelerationDir", Vector) = (0,0,0,0)
        _AccelerationScaleFactor ("AccelerationScaleFactor", Float) = 0
        _BendRotationOffset ("BendRotationOffset", Float) = 0
    }

    SubShader
    {
        LOD 100
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" "RenderType" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _RefractionTex_ST;
            float4 _AccelerationDir;
            float _ScrollSpeedX;
            float _ScrollSpeedY;
            float _RefractionAmount;
            float _BottomRefractionPosOffset;
            float _BottomRefractionFalloffGradient;
            float _TextureScaleX;
            float _TextureScaleY;
            float _VertexDispScale;
            float _VertexDispContrast;
            float _VertexDispPosOffset;
            float _VertexDispFalloffGradient;
            float _AccelerationScaleFactor;
            float _BendRotationOffset;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "KSP2HeatRefraction" "Queue" = "Overlay" "RenderType" = "Transparent" }
            Cull Off
            // Refracted background pixels retain their scene depth for temporal reconstruction.
            ZWrite Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_RefractionTex);  SAMPLER(sampler_RefractionTex);
            // The frame after transparents, bound by HeatRefractionRenderHook.
            TEXTURE2D_X(_HeatRefractionGrabTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uvDepth    : TEXCOORD1;  // xy uv0, z eye depth of the undisplaced vertex
                float3 normalWS   : TEXCOORD2;
                float3 positionWS : TEXCOORD4;
            };

            float HeatRefraction_Smooth(float t)
            {
                return t * t * (3.0 - 2.0 * t);
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float2 uv = input.uv0.xy;

                // Acceleration bending (no centre offset term in this shader).
                float3 accelSign = sign(_AccelerationDir.xyz);
                float3 accelForce = ((1.0 - pow(abs(_AccelerationDir.xyz) + 1.0, -2.5)) * accelSign) * 4.0;
                float2 bendForceXZ = accelForce.xz * _AccelerationScaleFactor;
                float bendBase = ((1.0 - uv.y) * length(accelForce)) * 0.01 + 1.0;
                float bendFactor = bendBase * bendBase;
                float bendQuad = bendFactor * bendFactor - bendFactor;
                float sinBend = sin(_BendRotationOffset * 3.1415927);
                float cosBend = cos(_BendRotationOffset * 3.1415927);
                float bendU = bendQuad * dot(float2(-sinBend, cosBend), bendForceXZ);
                float bendV = bendQuad * dot(float2(cosBend, sinBend), bendForceXZ);
                float3 centrifugal = float3(bendU * bendU * sign(bendU), bendV * bendV * sign(bendV), 0.0);

                // Noise displacement along the normal, from the refraction texture red channel.
                float2 dispUV = uv * float2(_TextureScaleX, _TextureScaleY) + _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY);
                float dispSample = SAMPLE_TEXTURE2D_LOD(_RefractionTex, sampler_RefractionTex, dispUV, 0).x
                    * (_VertexDispContrast - (1.0 - _VertexDispContrast)) + (1.0 - _VertexDispContrast);
                float dispFadeT = saturate(((1.0 - uv.y) - _VertexDispPosOffset) * (1.0 / _VertexDispFalloffGradient));
                float3 displacedOS = (dispSample * input.normalOS * _VertexDispScale) * HeatRefraction_Smooth(dispFadeT) + centrifugal + input.positionOS.xyz;

                output.positionWS = TransformObjectToWorld(displacedOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uvDepth = float4(uv, -TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz)).z, 0.0);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 uv = input.uvDepth.xy;
                float eyeDepth = input.uvDepth.z;

                float3 toCamera = _WorldSpaceCameraPos.xyz - input.positionWS;
                float3 viewDir = toCamera * rsqrt(max(dot(toCamera, toCamera), 0.001));
                float3 normalWS = normalize(IS_FRONT_VFACE(facing, true, false) ? input.normalWS : -input.normalWS);
                float ndotV = min(abs(dot(normalWS, viewDir)), 1.0);

                // Refraction strength: view facing, bottom gradient, top edge cut, distance scaled.
                float bottomT = saturate((uv.y - _BottomRefractionPosOffset) * (1.0 / _BottomRefractionFalloffGradient));
                float topEdgeT = saturate((1.0 - uv.y) * 100.0);
                float strength = HeatRefraction_Smooth(ndotV) * (min(HeatRefraction_Smooth(bottomT), 1.0) * min(HeatRefraction_Smooth(topEdgeT), 1.0));
                strength = (strength * _RefractionAmount) / max(eyeDepth, 0.1);

                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                strength *= saturate(LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - eyeDepth);

                // DXT5nm-style xy from the scrolling refraction texture.
                float4 refr = SAMPLE_TEXTURE2D(_RefractionTex, sampler_RefractionTex, uv * float2(_TextureScaleX, _TextureScaleY) + _Time.y * float2(_ScrollSpeedX, _ScrollSpeedY));
                float2 offsetDir = float2((refr.w * refr.x) * 2.0 - 1.0, refr.y * 2.0 - 1.0);

                // Only refract where the offset sample is still behind this surface.
                float offsetFade = saturate(LinearEyeDepth(SampleSceneDepth(offsetDir * strength + screenUV), _ZBufferParams) - eyeDepth);
                float2 refractedUV = (offsetDir * strength) * offsetFade + screenUV;

                // Snap to the texel centre, as the stock shader does with _CameraDepthTexture_TexelSize.
                float2 snappedUV = (floor(refractedUV * _CameraDepthTexture_TexelSize.zw) + 0.5) * _CameraDepthTexture_TexelSize.xy;
                return SAMPLE_TEXTURE2D_X(_HeatRefractionGrabTexture, sampler_LinearClamp, UnityStereoTransformScreenSpaceTex(snappedUV));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
