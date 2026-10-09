// SDK stand-in for the game's scaled-space body shader KSP2/Environment/CelestialBody/CelestialBody_Scaled.
//
// The game's shader ships in Redux's replacement shader bundle, so a material saved in the SDK or a mod
// project cannot reference it. Scaled body materials are authored on this shader instead, and
// Redux.CelestialBody.CelestialScaledMaterialReplacer moves them onto the game's shader when the body
// loads. The properties and passes are the URP port's own (Ksp2Redux
// Assets/ReduxAssets/Shaders/URP/CelestialBody), the pass code included rather than copied, so a body
// looks the same in the SDK preview as in the game. Keep it in step with CelestialBody_Scaled.shader.
Shader "KSP2/Planets/Scaled"
{
    Properties
    {
        [Header(Textures)] [NoScaleOffset] _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Main Color", Color) = (1,1,1,1)
        [Space(5)] [NoScaleOffset] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Float) = 1
        [Space(5)] [NoScaleOffset] _PackedMap ("Packed Map", 2D) = "black" {}
        _AOScale ("AO Strength", Range(0, 1)) = 1
        _EmissionTex ("Emission Map", 2D) = "black" {}
        _EmissionScale ("Emission Scale", Range(0, 20)) = 0
        [Space(5)] [Header(Scaled GI)] _Body1Color ("Color", Color) = (0,0,0,0)
        _Body1Intensity ("Intensity", Float) = 0
        _Body1Direction ("Direction", Vector) = (0,0,0,0)
        _Body2Color ("Color", Color) = (0,0,0,0)
        _Body2Intensity ("Intensity", Float) = 0
        _Body2Direction ("Direction", Vector) = (0,0,0,0)
        [Space(5)] _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.9
        [HideInInspector] _Transition ("Transition", Float) = 1
        [HideInInspector] _DitheringScale ("Dithering Scale", Range(0.001, 10)) = 1
        [Space(5)] [Header(Shared poles)] _PlanetScale ("PlanetScale", Float) = 1
        [Space(5)] [Header(North pole)] [NoScaleOffset] _NorthPoleDiffuse ("North pole diffuse", 2D) = "white" {}
        [NoScaleOffset] _NorthPoleNormal ("North pole normal", 2D) = "bump" {}
        _NorthPoleBlendStart ("North pole blend start", Range(0, 1)) = 0
        _NorthPoleBlend ("North pole blend", Range(0, 5)) = 0
        _NorthPoleScale ("North pole scale", Range(0, 10)) = 1
        [ShowAsVector2] _NorthPoleOffset ("North pole offset", Vector) = (0,0,0,0)
        [Space(5)] [Header(South pole)] [NoScaleOffset] _SouthPoleDiffuse ("South pole diffuse", 2D) = "white" {}
        [NoScaleOffset] _SouthPoleNormal ("South pole normal", 2D) = "bump" {}
        _SouthPoleBlendStart ("South pole blend start", Range(0, 1)) = 0
        _SouthPoleBlend ("South pole blend", Range(0, 5)) = 0
        _SouthPoleScale ("South pole scale", Range(0, 10)) = 1
        [ShowAsVector2] _SouthPoleOffset ("South pole offset", Vector) = (0,0,0,0)
        [HideInInspector] _KSP2EclipseOccluderIndices0 ("Eclipse occluders 0", Vector) = (-1,-1,-1,-1)
        [HideInInspector] _KSP2EclipseOccluderIndices1 ("Eclipse occluders 1", Vector) = (-1,-1,-1,-1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "RenderType" = "Opaque" }

        HLSLINCLUDE
        #include "Assets/ReduxAssets/Shaders/URP/Include/KSP2Pipeline.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _EmissionTex_ST;
            float4 _Color;
            float4 _Body1Color;
            float4 _Body1Direction;
            float4 _Body2Color;
            float4 _Body2Direction;
            float4 _NorthPoleOffset;
            float4 _SouthPoleOffset;
            float4 _KSP2EclipseOccluderIndices0;
            float4 _KSP2EclipseOccluderIndices1;
            float  _NormalScale;
            float  _AOScale;
            float  _EmissionScale;
            float  _Body1Intensity;
            float  _Body2Intensity;
            float  _ShadowStrength;
            float  _Transition;
            float  _DitheringScale;
            float  _PlanetScale;
            float  _NorthPoleBlendStart;
            float  _NorthPoleBlend;
            float  _NorthPoleScale;
            float  _SouthPoleBlendStart;
            float  _SouthPoleBlend;
            float  _SouthPoleScale;
        CBUFFER_END

        #include "Assets/ReduxAssets/Shaders/URP/CelestialBody/KSP2CelestialBodyScaledCommon.hlsl"
        ENDHLSL

        // Scaled-space Lambert, drawn only by explicit pass index (observer cubemap).
        Pass
        {
            Name "ScaledSpace"
            Tags { "LightMode" = "KSP2ScaledSpace" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CelestialBodyScaled_ScaledSpaceVertex
            #pragma fragment CelestialBodyScaled_ScaledSpaceFragment
            #include "Assets/ReduxAssets/Shaders/URP/CelestialBody/KSP2CelestialBodyScaledSpace.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CelestialBodyScaled_ForwardVertex
            #pragma fragment CelestialBodyScaled_ForwardFragment

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Assets/ReduxAssets/Shaders/URP/CelestialBody/KSP2CelestialBodyScaledForward.hlsl"
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
            #pragma target 4.5
            #pragma vertex KSP2_ShadowVertex
            #pragma fragment KSP2_ShadowFragment
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

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CelestialBodyScaled_DepthVertex
            #pragma fragment CelestialBodyScaled_DepthFragment
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex CelestialBodyScaled_DepthVertex
            #pragma fragment CelestialBodyScaled_DepthNormalsFragment
            ENDHLSL
        }
    }

    FallBack Off
}
