// ============================================================================
// Scatter_Instanced_Indirect_Opaque.shader
//
// Redux stand-in for the base-game terrain scatter shader
// KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque.
//
// The base-game shader lives in the ksp2.catalog AssetBundle and has no
// AssetDatabase GUID, so a project .mat asset cannot serialize a reference to
// it. Scatter materials reference this stand-in instead, and the real shader is
// bound at runtime by Redux.CelestialBody.ScatterShaderMapping when Vegetation
// Studio builds its per item material copies.
//
// This stand-in is never rendered. Its only jobs are to compile, to be
// findable, and to declare the same property names as the base-game shader so
// authored values survive the swap. The property list mirrors the real shader
// exactly, read from the loaded bundle.
// ============================================================================
Shader "Redux/Environment/Scatter/Scatter_Instanced_Indirect_Opaque"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MetallicSmoothnessMap ("Metallic Smoothness Map", 2D) = "white" {}
        _MetallicScale ("Metallic Scale", Range(0, 1)) = 1
        _SmoothnessScale ("Smoothness Scale", Range(0, 1)) = 1
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 1)) = 1
        _OcclusionMap ("Occlusion Map", 2D) = "white" {}
        _AOScale ("AO Scale", Range(0, 1)) = 1
        _DetailMask ("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMap ("Detail Albedo", 2D) = "white" {}
        _DetailAlbedoScale ("Detail Albedo Scale", Range(0, 1)) = 1
        _DetailNormalMap ("Detail Normal", 2D) = "bump" {}
        _DetailNormalScale ("Detail Normal Scale", Range(0, 1)) = 1
        [HideInInspector] _texcoord ("", 2D) = "white" {}
        [HideInInspector] __dirty ("", Float) = 0
        _LODDebugColor ("LOD Debug Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }

    FallBack "Diffuse"
}
