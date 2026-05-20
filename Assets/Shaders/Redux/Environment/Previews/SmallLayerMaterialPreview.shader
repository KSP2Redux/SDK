// Editor-only preview shader for SmallLayerMaterial. Renders a single sphere with the SO's
// composition fields, mirroring the per-sample math in DeferredBiome.cginc:ProcessBiomeLayer
// (albedo grading -> normal -> PBR threshold-overrides -> emission). No triplanar, no cascade,
// no biome blend - the preview is single-layer over a sphere UV.
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
        Tags { "RenderType" = "Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _Albedo;
        sampler2D _Normal;
        sampler2D _Metal;

        float _UVScale;
        float _UVOffset;
        fixed4 _Tint;
        half _Brightness;
        half _Contrast;
        half _Saturation;
        half _NormalStrength;
        half _GlossStrength;
        half _MetallicStrength;
        half _AOStrength;
        half _EmissionStrength;
        fixed4 _EmissionColor;

        struct Input
        {
            float2 uv_Albedo;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_Albedo * _UVScale + _UVOffset;

            half4 a = tex2D(_Albedo, uv);
            half4 n = tex2D(_Normal, uv);
            half  m = tex2D(_Metal, uv).r;

            // Albedo grading - matches DeferredBiome.cginc:250-256.
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

            // PBR with the same >=15 threshold-override convention as DeferredBiome.cginc:264-279.
            half metallic = (_MetallicStrength >= 15.0) ? (_MetallicStrength - 15.0) : (m * _MetallicStrength);
            half smoothness = (_GlossStrength >= 15.0) ? (_GlossStrength - 15.0) : (n.x * _GlossStrength);
            half ao = pow(saturate(n.z), _AOStrength);

            o.Albedo = saturate(albedo);
            o.Metallic = saturate(metallic);
            o.Smoothness = saturate(smoothness);
            o.Occlusion = ao;
            o.Normal = tsNormal;
            o.Emission = _EmissionColor.rgb * _EmissionStrength;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
