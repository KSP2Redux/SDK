// URP version of the SDK stand-in for KSP2/Parts/Reentry, the material part prefabs give their reentry
// meshes (ReentryMat). At runtime ReentryMesh swaps those renderers to the stock Reentry/Shockwave
// material, so this shader only draws in the editor. The Built-in stand-in was a Standard surface shader
// with a white albedo: this keeps that look on the KSP2 lighting model and adds the part reentry glow
// when ReentryMesh enables _REENTRYEMISSION_ON. Name and properties are unchanged.
Shader "KSP2/Parts/Reentry"
{
    Properties
    {
        [NoScaleOffset] _HeatGradient ("Texture", 2D) = "white" {}
        [KeywordEnum(None, Friction, UV, VertColor, BaseNoise, Heat, Normals)] _DebugMode ("Debug Mode", Float) = 0
        [KeywordEnum(Both, Noise1, Noise2)] _NoiseMode ("Noise Mode", Float) = 0
        [Header(Script Controlled)] [PerRendererData] VesselId ("Vessel ID", Float) = 0
        [PerRendererData] ShockwaveDistance ("Shockwave Distance", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }
        LOD 200

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float  _DebugMode;
            float  _NoiseMode;
            float  VesselId;
            float  ShockwaveDistance;
            // Reentry heating, fed by ReentryMesh through a MaterialPropertyBlock.
            float  _ReentryIntensity;
            float  _ReentryEmissivePow;
            float  _ReentryNoiseScale;
            float  _ReentryNoiseIntensity;
            float  _PartGlowNormalEdgeOffset;
            float4 _MetallicMinMax;
            float4 _ReentryColor;
            float4 _ReentryHeadingWs;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local _ _REENTRYEMISSION_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ FOG_LINEAR

            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Lighting.hlsl"
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Reentry.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv0        : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogFactor  : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv0;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 emission = 0;
            #if defined(_REENTRYEMISSION_ON)
                emission += KSP2_ReentryEmission(input.uv, normalWS, input.positionWS, 0.0);
            #endif
                // The stand-in surface only set a white albedo: metallic and smoothness stay at zero.
                KSP2Surface surface = KSP2_MakeMetallicSurface(float3(1.0, 1.0, 1.0), 0.0, 0.0, 1.0, emission, 1.0);
                float3 color = KSP2_Shade(input.positionWS, input.positionCS, normalWS, viewDirWS, surface);
                color = MixFog(color, input.fogFactor);
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2_DepthVertex
            #pragma fragment KSP2_DepthFragment
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2DepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2_DepthVertex
            #pragma fragment KSP2_DepthNormalsFragment
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2DepthPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}