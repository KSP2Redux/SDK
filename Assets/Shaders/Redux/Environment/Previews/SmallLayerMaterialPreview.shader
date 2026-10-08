// Editor-only preview shader for SmallLayerMaterial. Renders a single sphere with the SO's
// composition fields, mirroring the per-sample math of the terrain's biome layers
// (CBL_GradeColor and CBL_LayerStrengths in Ksp2Redux
// Assets/ReduxAssets/Shaders/URP/CelestialBody/KSP2CelestialBodyLocalForward.hlsl:
// albedo grading -> normal -> PBR threshold-overrides -> emission). No triplanar, no cascade,
// no biome blend - the preview is single-layer over a sphere UV. URP, lit by
// SmallLayerMaterialPreview's preview lights through the URP renderer.
Shader "Hidden/Ksp2UnityTools/SmallLayerMaterialPreview"
{
    Properties
    {
        _Albedo ("Albedo", 2D) = "white" {}
        _Normal ("Normal+SAO", 2D) = "bump" {}
        _Metal ("Metallic", 2D) = "black" {}

        _UVScale ("UV Scale", Float) = 1
        _UVOffset ("UV Offset", Float) = 0

        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Float) = 0
        _Contrast ("Contrast", Float) = 1
        _Saturation ("Saturation", Float) = 1

        _NormalStrength ("Normal Strength", Float) = 1
        _GlossStrength ("Gloss Strength", Float) = 1
        _MetallicStrength ("Metallic Strength", Float) = 1
        _AOStrength ("AO Strength", Float) = 1

        _EmissionStrength ("Emission Strength", Float) = 0
        _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Albedo_ST;
                float4 _Normal_ST;
                float4 _Metal_ST;
                float4 _Tint;
                float4 _EmissionColor;
                float  _UVScale;
                float  _UVOffset;
                float  _Brightness;
                float  _Contrast;
                float  _Saturation;
                float  _NormalStrength;
                float  _GlossStrength;
                float  _MetallicStrength;
                float  _AOStrength;
                float  _EmissionStrength;
            CBUFFER_END

            TEXTURE2D(_Albedo);     SAMPLER(sampler_Albedo);
            TEXTURE2D(_Normal);     SAMPLER(sampler_Normal);
            TEXTURE2D(_Metal);      SAMPLER(sampler_Metal);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float4 tangentWS  : TEXCOORD3; // w = bitangent sign
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = float4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv * _Albedo_ST.xy + _Albedo_ST.zw;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv * _UVScale + _UVOffset;

                half4 a = SAMPLE_TEXTURE2D(_Albedo, sampler_Albedo, uv);
                half4 n = SAMPLE_TEXTURE2D(_Normal, sampler_Normal, uv);
                half  m = SAMPLE_TEXTURE2D(_Metal, sampler_Metal, uv).r;

                // Albedo grading, as the terrain's biome layer pass grades a small layer.
                half3 albedo = a.rgb * _Tint.rgb;
                albedo += _Brightness;
                albedo = (albedo - 0.21763764) * _Contrast + 0.21763764;
                half lum = dot(albedo, half3(0.2126729, 0.7151522, 0.072175));
                albedo = lum + _Saturation * (albedo - lum);

                // Normal: DXT5nm-packed (.w = X, .y = Y), reconstruct Z, scale XY by strength.
                half3 tsNormal;
                tsNormal.xy = half2(n.w, n.y) * 2.0 - 1.0;
                tsNormal.xy *= _NormalStrength;
                tsNormal.z = sqrt(saturate(1.0 - dot(tsNormal.xy, tsNormal.xy)));

                // PBR with the terrain's >= 15 override convention (CBL_LayerStrengths): at 15 and above the
                // strength no longer scales the map but adds (strength - 15) * 0.2 to it.
                half metallic = (_MetallicStrength >= 15.0) ? (m + (_MetallicStrength - 15.0) * 0.2) : (m * _MetallicStrength);
                half smoothness = (_GlossStrength >= 15.0) ? (n.x + (_GlossStrength - 15.0) * 0.2) : (n.x * _GlossStrength);
                half ao = pow(saturate(n.z), _AOStrength);

                float3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                float3 normalWS = TransformTangentToWorld(tsNormal, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = saturate(albedo);
                surface.metallic = saturate(metallic);
                surface.smoothness = saturate(smoothness);
                surface.occlusion = ao;
                surface.emission = _EmissionColor.rgb * _EmissionStrength;
                surface.alpha = 1.0;
                return half4(UniversalFragmentPBR(inputData, surface).rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
