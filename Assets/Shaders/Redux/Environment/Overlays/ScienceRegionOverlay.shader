Shader "Redux/PlanetAuthoring/Overlays/ScienceRegionOverlay"
{
    // Renders a science-region texture (either the artist's source paint or the post-bake
    // colorized palette) as a translucent overlay on the body's surface. The texture is
    // sampled directly without channel mixing - both modes already supply per-pixel RGB.
    Properties
    {
        [MainTexture] _OverlayTexture ("Overlay Texture", 2D) = "black" {}
        _Strength ("Strength", Range(0, 1)) = 0.7

        [Toggle(_USE_PQS_BUFFER)] _NoComputeBuffer ("Use PQS QuadMeshDataBuffer", float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex OverlayVert
            #pragma fragment frag
            #pragma multi_compile_local _USE_PQS_BUFFER

            #include "OverlayCommon.hlsl"

            sampler2D _OverlayTexture;
            float     _Strength;

            float4 frag(overlay_v2f i) : SV_Target
            {
                float4 c = tex2D(_OverlayTexture, i.uv);
                return OverlayCompose(c.rgb, _Strength);
            }
            ENDHLSL
        }
    }
}
