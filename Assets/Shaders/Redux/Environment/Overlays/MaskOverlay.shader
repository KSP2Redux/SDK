Shader "Redux/PlanetAuthoring/Overlays/MaskOverlay"
{
    // Visualizes any 4-channel RGBA mask texture (biome mask, subzone mask)
    // as a sum of four channel-colored layers, or as a single isolated channel.
    Properties
    {
        [MainTexture] _MaskTex ("Mask Texture", 2D) = "black" {}
        _Strength ("Strength", Range(0, 1)) = 0.7

        // 0 = all channels blended; 1..4 = isolate R/G/B/A.
        [IntRange] _ChannelMode ("Channel Mode (0=All,1=R,2=G,3=B,4=A)", Range(0, 4)) = 0

        _ColorR ("Color R", Color) = (1.0, 0.30, 0.30, 1.0)
        _ColorG ("Color G", Color) = (0.30, 1.0, 0.30, 1.0)
        _ColorB ("Color B", Color) = (0.30, 0.50, 1.0, 1.0)
        _ColorA ("Color A", Color) = (1.0, 0.95, 0.30, 1.0)

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

            sampler2D _MaskTex;
            float     _Strength;
            float     _ChannelMode;
            float4    _ColorR;
            float4    _ColorG;
            float4    _ColorB;
            float4    _ColorA;

            float4 frag(overlay_v2f i) : SV_Target
            {
                float4 m = tex2D(_MaskTex, i.uv);

                int mode = (int)round(_ChannelMode);
                float3 rgb;
                if (mode == 0)
                {
                    rgb = m.r * _ColorR.rgb
                        + m.g * _ColorG.rgb
                        + m.b * _ColorB.rgb
                        + m.a * _ColorA.rgb;
                }
                else
                {
                    float c = (mode == 1) ? m.r
                            : (mode == 2) ? m.g
                            : (mode == 3) ? m.b
                                          : m.a;
                    rgb = c.xxx;
                }

                return OverlayCompose(rgb, _Strength);
            }
            ENDHLSL
        }
    }
}
