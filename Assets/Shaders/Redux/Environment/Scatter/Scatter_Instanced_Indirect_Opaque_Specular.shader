// ============================================================================
// Scatter_Instanced_Indirect_Opaque_Specular.shader
//
// Redux stand-in for the base-game terrain scatter shader
// KSP2/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular.
//
// See Scatter_Instanced_Indirect_Opaque.shader for why the stand-in exists and
// how the swap works. This variant is the specular counterpart, differing only
// in the specular block replacing the metallic one, which is what
// KSPScatterController keys on when deciding which knobs to expose.
//
// Note there is deliberately no Transparent stand-in. KSPScatterController
// matches a third name, Scatter_Instanced_Indirect_Transparent, but that shader
// is not present in ksp2.catalog, so a stand-in for it would map to nothing.
// ============================================================================
Shader "Redux/Environment/Scatter/Scatter_Instanced_Indirect_Opaque_Specular"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 10)) = 1
        _Contrast ("Contrast", Range(0, 1)) = 0.5
        _Saturation ("Saturation", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Specular ("Specular", Range(0, 1)) = 0.5
        _SpecularSmoothnessMap ("Specular Smoothness Map", 2D) = "white" {}
        _SpecularTintColor ("Specular Tint", Color) = (1,1,1,1)
        _SpecularScale ("Specular Scale", Range(0, 1)) = 1
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
