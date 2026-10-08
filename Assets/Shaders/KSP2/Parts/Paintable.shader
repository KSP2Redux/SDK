// SDK stand-in for the game's part shader KSP2/Scenery/Standard (Opaque).
//
// The game's shader ships in Redux's replacement shader bundle, so a material saved in the SDK or a mod
// project cannot reference it. Part materials are authored on this shader instead, and the game moves
// them onto KSP2/Scenery/Standard (Opaque) when a part loads (Redux.Patching.ShaderUtils.HotswapPartShaders).
// The passes are the URP port's own (Ksp2Redux Assets/ReduxAssets/Shaders/URP/Scenery), included rather
// than copied, so a part looks the same in the editor as in the game. Keep the SubShader in step with
// KSP2_Scenery_Standard_Opaque.shader. The properties keep the SDK's labels and authoring defaults.
Shader "KSP2/Parts/Paintable"
{
    Properties
    {
        [Header(Surface)]
        _Color ("Albedo Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [NoScaleOffset] _MetallicGlossMap ("Metallic / Smoothness", 2D) = "white" {}
        [Gamma] _Metallic ("Metallic Strength", Range(0, 1)) = 1
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
        _PaintA ("Accent Paint", Color) = (0.4418,0.4431,0.5176,1)
        _PaintB ("Base Paint", Color) = (0.6549,0.6667,0.6863,1)
        [NoScaleOffset] _PaintMaskGlossMap ("Paint Mask / Paint Smoothness", 2D) = "black" {}
        _PaintGlossMapScale ("Paint Smoothness Strength", Range(0, 1)) = 1
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
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _DetailMask_ST;
            float4 _PaintMaskGlossMap_ST;
            float4 _Color;
            float4 _EmissionColor;
            float4 _PaintA;
            float4 _PaintB;
            float4 _RimColor;
            float  _Metallic;
            float  _GlossMapScale;
            float  _MipBias;
            float  _DetailBumpScale;
            float  _DetailBumpTiling;
            float  _OcclusionStrength;
            float  _ReentryEmission;
            float  _UseTimeOfDay;
            float  _TimeOfDayDotMin;
            float  _TimeOfDayDotMax;
            float  _PaintGlossMapScale;
            float  _SmoothnessOverride;
            float  _RimFalloff;
            float  _Culling;
            float  _Offset;
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
            Cull [_Culling]
            Offset [_Offset], [_Offset]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2StandardOpaque_ForwardVertex
            #pragma fragment KSP2StandardOpaque_ForwardFragment

            // The variant set of the Standard (Opaque) port this pass includes.
            #pragma multi_compile_local_fragment _ _REENTRYEMISSION_ON
            #pragma multi_compile_local_fragment _ _SMOOTHNESSOVERRIDE_ON
            #pragma multi_compile_local_fragment _ USE_TIME_OF_DAY
            #pragma multi_compile_fragment _ RK_GALAXY_CUBEMAP RK_OBSERVER_CUBEMAP

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma dynamic_branch _ FOG_EXP FOG_EXP2

            #include "Assets/ReduxAssets/Shaders/URP/Scenery/KSP2SceneryStandardOpaqueForward.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Culling]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2_ShadowVertex
            #pragma fragment KSP2_ShadowFragment
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2DepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Culling]
            Offset [_Offset], [_Offset]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2_DepthVertex
            #pragma fragment KSP2_DepthFragment
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2DepthPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Culling]
            Offset [_Offset], [_Offset]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex KSP2_DepthVertex
            #pragma fragment KSP2_DepthNormalsFragment
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2DepthPasses.hlsl"
            ENDHLSL
        }
    }

    CustomEditor "PaintableShaderGUI"
    FallBack Off
}
