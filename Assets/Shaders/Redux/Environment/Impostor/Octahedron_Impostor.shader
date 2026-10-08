// ============================================================================
// Octahedron_Impostor.shader
//
// Far LOD for terrain scatter: draws a baked octahedral impostor, a camera
// facing quad that picks and blends the three nearest of _Frames x _Frames
// baked views. Vegetation Studio draws it as an item's BillboardCustomPrefab
// through an indirect instanced draw, on a copy of this material.
//
// Redux's own name for the stock KSP2 Amplify octahedron impostor
// (Hidden/Amplify Impostors/Octahedron Impostor), which ScatterImpostorBaker
// writes its materials against. The properties and passes are the URP port's
// own (Ksp2Redux Assets/ReduxAssets/Shaders/URP/Environment), the pass code
// included rather than copied, so a baked impostor renders the same as a stock
// one. Keep it in step with Hidden_Amplify_Impostors_Octahedron_Impostor.shader.
// ============================================================================
Shader "Redux/Environment/Impostor/Octahedron_Impostor"
{
    Properties
    {
        [NoScaleOffset] _Albedo ("Albedo & Alpha", 2D) = "white" {}
        [NoScaleOffset] _Normals ("Normals & Depth", 2D) = "white" {}
        [NoScaleOffset] _Specular ("Specular & Smoothness", 2D) = "black" {}
        [NoScaleOffset] _Emission ("Emission & Occlusion", 2D) = "black" {}
        [HideInInspector] _Frames ("Frames", Float) = 16
        [HideInInspector] _ImpostorSize ("Impostor Size", Float) = 1
        [HideInInspector] _Offset ("Offset", Vector) = (0,0,0,0)
        _TextureBias ("Texture Bias", Float) = -1
        _Parallax ("Parallax", Range(-1, 1)) = 1
        [HideInInspector] _DepthSize ("DepthSize", Float) = 1
        _ClipMask ("Clip", Range(0, 1)) = 0.5
        _AI_ShadowBias ("Shadow Bias", Range(0, 2)) = 0.25
        _AI_ShadowView ("Shadow View", Range(0, 1)) = 1
        [Toggle(_HEMI_ON)] _Hemi ("Hemi", Float) = 0
        [Toggle(EFFECT_HUE_VARIATION)] _Hue ("Use SpeedTree Hue", Float) = 0
        _HueVariation ("Hue Variation", Color) = (0,0,0,0)
        _Color ("Color tint", Color) = (1,1,1,1)
        _LODDebugColor ("LOD Debug color", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0
        _SpecularValue ("Specular", Range(0, 1)) = 0
        _SpecularTintColor ("Specular Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" "DisableBatching" = "True" }

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Offset;
            float4 _HueVariation;
            float4 _Color;
            float4 _LODDebugColor;
            float4 _SpecularTintColor;
            float  _Frames;
            float  _ImpostorSize;
            float  _TextureBias;
            float  _Parallax;
            float  _DepthSize;
            float  _ClipMask;
            float  _AI_ShadowBias;
            float  _AI_ShadowView;
            float  _Hemi;
            float  _Hue;
            float  _Brightness;
            float  _Contrast;
            float  _Saturation;
            float  _Smoothness;
            float  _SpecularValue;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex ImpostorVertex
            #pragma fragment ImpostorForwardFragment

            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ FOG_LINEAR

            #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Lighting.hlsl"
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2OctahedronImpostor.hlsl"
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
            #pragma vertex ImpostorShadowVertex
            #pragma fragment ImpostorShadowFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2OctahedronImpostor.hlsl"
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
            #pragma vertex ImpostorVertex
            #pragma fragment ImpostorDepthFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2OctahedronImpostor.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex ImpostorVertex
            #pragma fragment ImpostorDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include "Assets/ReduxAssets/Shaders/URP/Environment/KSP2OctahedronImpostor.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
