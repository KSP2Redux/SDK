// Stand-in for the game's KSP2/Environment/Ocean/OceanWater, which ships in the game's data rather than any project.
// An ocean material authored on it saves with a real shader reference and shows stock's properties in the inspector.
// PQSRenderer draws with a copy moved onto the game's shader, so this pass only colours the material preview.
Shader "KSP2/Environment/Ocean/OceanWater (SDK Stand-in)"
{
    Properties {
        [Header(Ocean Mask)] [NoScaleOffset] _OceanMaskTexture ("Ocean Mask", 2D) = "black" {}
        [Header(PBR)] _Smoothness ("Smoothness", Range(0, 1)) = 0.8
        _Metalness ("Metalness", Range(0, 1)) = 0
        [Header(Color)] _DiffuseColor ("Diffuse Color", Vector) = (0.07843,0.298,0.345,1)
        _SubsurfaceColor ("Subsurface Color", Vector) = (0.2157,0.3608,0.4157,1)
        _SubsurfaceStrength ("Subsurface Strength", Range(0, 20)) = 1
        [Header(Shallow)] _ShallowColor ("Shallow Color", Vector) = (0.29,0.81,0.87,1)
        _ShallowMaxDepth ("Shallow Max Depth", Float) = 30
        _ShallowColorDistribution ("Shallow Color Distribution", Range(0, 1)) = 0.25
        [Header(Ocean Spectrum)] _SpectrumNormalStrength ("Spectrum Normal Strength", Range(0, 1)) = 1
        _SpectrumFadeStartHeight ("Spectrum Normal Fade Range Start Height", Range(0, 500)) = 200
        _SpectrumFadeEndHeight ("Spectrum Normal Fade Range End Height", Range(0, 500)) = 300
        [Header(Detail Region)] _DetailNormalTilingSize ("Detail Normal Tiling Size", Range(0.01, 4096)) = 128
        _DetailNormalMoveSpeed ("Detail Normal Move Speed", Float) = 0.02
        _DetailNormalDirectionX ("Detail Normal Direction X", Range(-1, 1)) = 1
        _DetailNormalDirectionY ("Detail Normal Direction Y", Range(-1, 1)) = 0
        [NoScaleOffset] _DetailNormalMap ("Detail Normal Map", 2D) = "bump" {}
        _DetailNormalStrength ("Detail Normal Strength", Range(0, 1)) = 1
        _DetailFadeStartHeight ("Detail Fade Start Height", Range(0, 10000)) = 50
        _DetailFadeEndHeight ("Detail Fade End Height", Range(0, 10000)) = 100
        [Header(Large Normal 1)] _LargeNormalMapTilingSize_1 ("Large Normal Tiling Size 1", Float) = 1024
        _LargeNormalMoveSpeed_1 ("Large Normal Move Speed 1", Float) = 0.02
        _LargeNormalDirectionX_1 ("Large Normal Direction X 1", Range(-1, 1)) = 1
        _LargeNormalDirectionY_1 ("Large Normal Direction Y 1", Range(-1, 1)) = 0
        [NoScaleOffset] _LargeNormalMap_1 ("Large Normal Map 1", 2D) = "bump" {}
        _LargeNormalStrength_1 ("Large Normal Strength 1", Range(0, 1)) = 1
        _LargeFadeStartHeight_1 ("Large Fade Start Height 1", Range(0, 25000)) = 300
        _LargeFadeEndHeight_1 ("Large Fade End Height 1", Range(0, 25000)) = 500
        [Header(Large Normal 2)] _LargeNormalMapTilingSize_2 ("Large Normal Tiling Size 2", Float) = 10240
        _LargeNormalMoveSpeed_2 ("Large Normal Move Speed 2", Float) = 0.003
        _LargeNormalDirectionX_2 ("Large Normal Direction X 2", Range(-1, 1)) = 1
        _LargeNormalDirectionY_2 ("Large Normal Direction Y 2", Range(-1, 1)) = 0
        _LargeNormalMap_2 ("Large Normal Map 2 ", 2D) = "bump" {}
        _LargeNormalStrength_2 ("Large Normal Strength 2", Range(0, 1)) = 1
        [Header(Scaled Space)] [NoScaleOffset] _ScaleAlbedoTexture ("Scale Albedo Texture", 2D) = "black" {}
        _ScaleAlbedoStartHeight ("Scale Albedo Start Height", Range(0, 160000)) = 20000
        _ScaleAlbedoEndHeight ("Scale Albedo End Height", Range(0, 160000)) = 50000
        _ScaleAlbedoStartOpacity ("Scale Albedo Start Opacity", Range(0, 1)) = 0
        _ScaleAlbedoEndOpacity ("Scale Albedo End Opacity", Range(0, 1)) = 0
        [NoScaleOffset] _ScaleNormalTexture ("Scale Normal Texture", 2D) = "bump" {}
        _ScaleNormalStartHeight ("Scale Normal Start Height", Range(0, 160000)) = 3000
        _ScaleNormalEndHeight ("Scale Normal End Height", Range(0, 160000)) = 20000
        _ScaleNormalStartOpacity ("Scale Normal Start Opacity", Range(0, 1)) = 0
        _ScaleNormalEndOpacity ("Scale Normal End Opacity", Range(0, 1)) = 0
        _ScaleNormalScale ("Scale Normal Scale", Float) = 1
        [NoScaleOffset] _ScalePackedTexture ("Scale Packed Texture", 2D) = "black" {}
        _ScalePackedStartHeight ("Scale Packed Start Height", Range(0, 160000)) = 3000
        _ScalePackedEndHeight ("Scale Packed End Height", Range(0, 160000)) = 20000
        _ScalePackedStartOpacity ("Scale Packed Start Opacity", Range(0, 1)) = 0
        _ScalePackedEndOpacity ("Scale Packed End Opacity", Range(0, 1)) = 0
        [Header(Foam)] _FoamStrength ("Foam Strength", Float) = 3
        _FoamFade ("Foam Fade", Float) = 3
        _FoamDiffuseColor ("Foam Diffuse Color", Vector) = (1,1,1,1)
        [NoScaleOffset] _FoamShapeTexture ("Foam Shape", 2D) = "black" {}
        _FoamShapeTilingSize ("Foam Shape Tiling Size", Range(0.01, 5000)) = 20
        _FoamAlpha ("Foam Alpha", Range(0, 1)) = 1
        _FoamMinDepth ("Foam Min Depth", Range(0, 20)) = 0.1
        _ShorelineFoamStrength ("Shoreline Foam Strength", Range(0, 5)) = 2
        _ShorelineFoamMovementSpeed ("Shoreline Foam Movement Speed", Range(0, 1)) = 0.02
        _ShorelineFoamMaxDepth ("Shoreline Foam Max Depth", Range(0, 50)) = 5
        _ShoreLineFoamAlpha ("Shoreline Foam Alpha", Range(0, 1)) = 0.8
        [Header(Far Foam)] _FarFoamStrength ("Far Foam Strength", Float) = 4
        _FarFoamFade ("Far Foam Fade", Float) = 2
        _FarFoamDiffuseColor ("Far Foam Diffuse Color", Vector) = (1,1,1,1)
        [NoScaleOffset] _FarFoamShapeTexture ("Far Foam Shape", 2D) = "black" {}
        _FarFoamShapeTilingSize ("Far Foam Shape Tiling Size", Range(0.01, 5000)) = 300
        _FarFoamAnimationSpeed ("Far Foam Animation Speed", Range(0, 1)) = 0.8
        _FarFoamMovementSpeed ("Far Foam Movement Speed", Range(0, 0.01)) = 0.001
        _FarFoamAlpha ("Far Foam Alpha", Range(0, 1)) = 1
        _FarShorelineFoamStrength ("Far Shoreline Foam Strength", Range(0, 5)) = 1
        _FarShorelineFoamMovementSpeed ("Far Shoreline Foam Movement Speed", Range(0, 1)) = 0.05
        _FarShorelineFoamMaxDepth ("Far Shoreline Foam Max Depth", Range(0, 50)) = 20
        _FarShoreLineFoamAlpha ("Far Shoreline Foam Alpha", Range(0, 1)) = 0.9
        _FoamFadeStartHeight ("Foam Fade Range Start Height", Range(0, 16000)) = 800
        _FoamFadeEndHeight ("Foam Fade Range End Height", Range(0, 16000)) = 2000
        _ShowFarFoamRegion ("Show Far Foam Region", Float) = 0
        [Header(Transparent)] _TransparentVisibleDepth ("Transparent Visible Depth", Float) = 30
        _TransparentFadeStrength ("Transparent Fade Strength", Range(0, 1)) = 0.7
        _MinTransparent ("Min Transparent", Range(0, 1)) = 0.05
        _RefractionStrength ("Refraction Strength", Range(0, 30)) = 10
        [Header(Reflection)] _ReflectionStarHeight ("Reflection Start Height", Range(0, 160000)) = 7000
        _ReflectionEndHeight ("Reflection End Height", Range(0, 160000)) = 20000
        _ReflectionStartStrength ("Reflection Start Strength", Range(0, 1)) = 1
        _ReflectionEndStrength ("Reflection End Strength", Range(0, 1)) = 0
        [Header(Shoreline)] [NoScaleOffset] _ShorelineSDFTexture ("Shoreline SDF", 2D) = "white" {}
        _ShoreWaveAttenuation ("Shore Wave Attenuation", Range(0, 1)) = 0.8
        [Header(Shadow)] _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.8
        [Header(UnderWater)] _UnderWaterDiffuseColor ("UnderWater Diffuse Color", Vector) = (0.07843,0.298,0.345,1)
        _UnderWaterMaxVisibleDistance ("Under Water Max Visible Distance", Float) = 300
        _UnderWaterDistanceFadeStrength ("Under Water Visible Distance Fade Strength", Range(0, 1)) = 0.5
        _UnderWaterMaxVisibleDepth ("Under Water Max Visible Depth", Float) = 300
        _UnderWaterDepthFadeStrength ("Under Water Visible Depth Fade Strength", Range(0, 1)) = 0.5
        _UnderWaterSpecularStrength ("UnderWater Specular Strength", Range(0, 10)) = 2
        _UnderWaterSpecularDistribution ("UnderWater Specular Distribution", Range(0, 30)) = 1
        _UnderWaterTransparentStrength ("Under Water Transparent Strength", Range(0, 1)) = 0.2
        _UnderWaterRefractionStrength ("Under Water Refraction Strength", Range(0, 30)) = 10
        [NoScaleOffset] _CausticsTexture ("Caustics Texture", 2D) = "black" {}
        _CausticsStrength ("Caustics Strength", Range(0, 10)) = 1
        _CausticsMaxVisibleDepth ("Caustics Falloff", Float) = 50
        _CausticsTilingSize ("Caustics Tiling Size", Range(0.01, 100)) = 15
        _CausticsAnimationStrength ("Caustics Animation Strength", Range(0, 1)) = 0.1
        _CausticsAnimationSpeed ("Caustics Animation Speed", Range(0, 20)) = 1
        _CausticsAnimationFactor ("Caustics Animation Factor", Range(0.1, 20)) = 4
        _GodRayStrength ("God Ray Strength", Range(0, 20)) = 1
        _GodRayColorMask ("God Ray Color Mask", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
#pragma surface surf Standard
#pragma target 3.0

        fixed4 _DiffuseColor;
        half _Smoothness;
        half _Metalness;

        struct Input
        {
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo = _DiffuseColor.rgb;
            o.Smoothness = _Smoothness;
            o.Metallic = _Metalness;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
