// ============================================================================
// Scatter_Instanced_Indirect_Opaque.shader
//
// Redux stand-in for the game's terrain scatter shader
// KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque.
//
// The game's shader ships in Redux's replacement shader bundle, so a project
// .mat asset cannot serialize a reference to it. Scatter materials reference
// this stand-in instead, and Redux.CelestialBody.ScatterShaderMapping binds the
// game's shader when Vegetation Studio builds its per item material copies.
//
// The properties and passes are the URP port's own (Ksp2Redux
// Assets/ReduxAssets/Shaders/URP/Environment), the pass code included rather
// than copied, so a scatter prefab looks the same in the editor as in the game.
// Keep it in step with KSP2_Environment_Scatter_Scatter_Instanced_Indirect_Opaque.shader.
// The extra ImpostorCapture pass is what ScatterImpostorBaker renders.
// ============================================================================
Shader "Redux/Environment/Scatter/Scatter_Instanced_Indirect_Opaque"
{
    Properties
    {
        [Header(Color)] _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _Metallic ("Metallic", Range(0, 1)) = 0
        [Header(Metallic Smoothness)] _MetallicSmoothnessMap ("Metallic (RGB) Smoothness (A)", 2D) = "black" {}
        _MetallicScale ("Metallic Scale", Range(0, 1)) = 0
        _SmoothnessScale ("Smoothness Scale", Range(0, 1)) = 1
        [Header(Normals)] _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Intensity", Range(0, 10)) = 1
        [Header(Occlusion)] _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _AOScale ("AO Scale", Range(0, 1)) = 1
        [Header(Detail)] _DetailMask ("Detail Mask (A)", 2D) = "white" {}
        _DetailAlbedoMap ("Detail Albedo", 2D) = "gray" {}
        _DetailAlbedoScale ("Detail Albedo Scale", Range(0, 1)) = 0
        _DetailNormalMap ("Detail Normal", 2D) = "bump" {}
        _DetailNormalScale ("Detail Normal Scale", Range(0, 10)) = 1
        [HideInInspector] _texcoord ("", 2D) = "white" {}
        [HideInInspector] __dirty ("", Float) = 1
        _LODDebugColor ("LOD Debug color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _MetallicSmoothnessMap_ST;
            float4 _BumpMap_ST;
            float4 _OcclusionMap_ST;
            float4 _DetailMask_ST;
            float4 _DetailAlbedoMap_ST;
            float4 _DetailNormalMap_ST;
            float4 _texcoord_ST;
            float4 _Color;
            float4 _LODDebugColor;
            float  _Brightness;
            float  _Contrast;
            float  _Saturation;
            float  _Smoothness;
            float  _Metallic;
            float  _MetallicScale;
            float  _SmoothnessScale;
            float  _BumpScale;
            float  _AOScale;
            float  _DetailAlbedoScale;
            float  _DetailNormalScale;
            float  __dirty;
            // Set on the material copies by KSPScatterController (no Properties entry, unused here).
            float  _BlendDistance;
            float  _BlendStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex KSP2Scatter_ForwardVertex
            #pragma fragment KSP2Scatter_ForwardFragment

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile_local _ SCATTER_SYSTEM

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ FOG_LINEAR

            #define KSP2_SCATTER_FOG 1
            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Lighting.hlsl"
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex KSP2Scatter_ShadowVertex
            #pragma fragment KSP2Scatter_ShadowFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex KSP2Scatter_DepthVertex
            #pragma fragment KSP2Scatter_DepthFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex KSP2Scatter_DepthVertex
            #pragma fragment KSP2Scatter_DepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"
            ENDHLSL
        }

        // ScatterImpostorBaker only. URP never draws this LightMode.
        Pass
        {
            Name "ImpostorCapture"
            Tags { "LightMode" = "ReduxImpostorCapture" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex KSP2Scatter_ForwardVertex
            #pragma fragment ScatterImpostorCapture_Fragment
            #include "ScatterImpostorCapture.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
