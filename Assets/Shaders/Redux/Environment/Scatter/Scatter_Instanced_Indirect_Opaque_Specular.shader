// ============================================================================
// Scatter_Instanced_Indirect_Opaque_Specular.shader
//
// Redux stand-in for the game's terrain scatter shader
// KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular.
//
// See Scatter_Instanced_Indirect_Opaque.shader for why the stand-in exists and
// how the swap works. This variant is the specular counterpart, differing only
// in the specular block replacing the metallic one, which is what
// KSPScatterController keys on when deciding which knobs to expose. Keep it in
// step with KSP2_Environment_Scatter_Scatter_Instanced_Indirect_Opaque_Specular.shader.
//
// Note there is deliberately no Transparent stand-in. KSPScatterController
// matches a third name, Scatter_Instanced_Indirect_Transparent, but the game
// has no shader by that name, so a stand-in for it would map to nothing.
// ============================================================================
Shader "Redux/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular"
{
    Properties
    {
        [Header(Color)] _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _Specular ("Specular", Range(0, 1)) = 0
        [Header(Specular Smoothness)] _SpecularSmoothnessMap ("Specular (RGB) Smoothness (A)", 2D) = "black" {}
        _SpecularTintColor ("Specular Tint", Color) = (1,1,1,1)
        _SpecularScale ("Specular Scale", Range(0, 1)) = 0
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
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" "UniversalMaterialType" = "Lit" }

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        #define KSP2_SCATTER_SPECULAR 1

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _SpecularSmoothnessMap_ST;
            float4 _BumpMap_ST;
            float4 _OcclusionMap_ST;
            float4 _DetailMask_ST;
            float4 _DetailAlbedoMap_ST;
            float4 _DetailNormalMap_ST;
            float4 _texcoord_ST;
            float4 _Color;
            float4 _LODDebugColor;
            float4 _SpecularTintColor;
            float  _Brightness;
            float  _Contrast;
            float  _Saturation;
            float  _Smoothness;
            float  _Specular;
            float  _SpecularScale;
            float  _SmoothnessScale;
            float  _BumpScale;
            float  _AOScale;
            float  _DetailAlbedoScale;
            float  _DetailNormalScale;
            float  __dirty;
            // Terrain blend, set on the material copies by KSPScatterController (no Properties entry).
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
            // The variant set of the scatter port this pass includes.
            #pragma multi_compile_local_fragment _ SCATTER_SYSTEM

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Lighting.hlsl"
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2ScatterOpaque.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma exclude_renderers gles3 glcore
            #pragma vertex KSP2Scatter_ForwardVertex
            #pragma fragment KSP2Scatter_GBufferFragment

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile_local_fragment _ SCATTER_SYSTEM

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _WRITE_RENDERING_LAYERS
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

            #define KSP2_SPECULAR_SETUP
            #define KSP2_GBUFFER_PASS
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
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
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
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
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
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
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
