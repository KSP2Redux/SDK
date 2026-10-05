// ============================================================================
// ImpostorBake.shader
//
// Editor-only helpers for ScatterImpostorBaker, each a single target blit.
//
// Pack builds one of the four impostor textures from the captured G-buffer
// atlas, chosen by _PackOutput: 0 AlbedoAlpha, 1 NormalDepth,
// 2 SpecularSmoothness, 3 EmissionOcclusion.
// Dilate grows colour one texel outward from the covered area, and GrowMask
// grows the coverage mask to match, so mip levels and bilinear taps at the
// silhouette never pull in the cleared background.
// ============================================================================
Shader "Hidden/Redux/ImpostorBake"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Pack"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _GBuffer0;
            sampler2D _GBuffer1;
            sampler2D _GBuffer2;
            sampler2D _GBuffer3;
            sampler2D _BakeDepth;
            int _PackOutput;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 gBuffer0 = tex2D(_GBuffer0, i.uv);
                float4 gBuffer2 = tex2D(_GBuffer2, i.uv);

                // The deferred pass writes 1 to the normal buffer's alpha wherever it draws.
                float coverage = gBuffer2.a > 0.5 ? 1.0 : 0.0;

                float4 output;
                if (_PackOutput == 0)
                {
                    output = float4(LinearToGammaSpace(gBuffer0.rgb), 1.0);
                }
                else if (_PackOutput == 1)
                {
                    // The bake camera's depth range is exactly _DepthSize, centred on the frame
                    // plane, so the stored depth is how far toward the camera the texel sits.
                    float rawDepth = tex2D(_BakeDepth, i.uv).r;
                    #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                    #else
                    float depth = 1.0 - rawDepth;
                    #endif
                    output = float4(gBuffer2.rgb, depth);
                }
                else if (_PackOutput == 2)
                {
                    output = tex2D(_GBuffer1, i.uv);
                }
                else
                {
                    output = float4(tex2D(_GBuffer3, i.uv).rgb, gBuffer0.a);
                }

                return output * coverage;
            }
            ENDCG
        }

        Pass
        {
            Name "Dilate"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _Coverage;
            float _PreserveAlpha;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 centre = tex2D(_MainTex, i.uv);
                if (tex2D(_Coverage, i.uv).r > 0.5)
                    return centre;

                float4 sum = 0.0;
                float count = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 uv = i.uv + float2(x, y) * _MainTex_TexelSize.xy;
                        float covered = tex2D(_Coverage, uv).r > 0.5 ? 1.0 : 0.0;
                        sum += tex2D(_MainTex, uv) * covered;
                        count += covered;
                    }
                }

                if (count == 0.0)
                    return centre;

                float4 grown = sum / count;
                if (_PreserveAlpha > 0.5)
                    grown.a = centre.a;
                return grown;
            }
            ENDCG
        }

        Pass
        {
            Name "GrowMask"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 frag(v2f_img i) : SV_Target
            {
                float covered = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        covered = max(covered, tex2D(_MainTex, i.uv + float2(x, y) * _MainTex_TexelSize.xy).r);
                    }
                }

                return covered;
            }
            ENDCG
        }

        Pass
        {
            Name "Coverage"

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            float4 frag(v2f_img i) : SV_Target
            {
                return tex2D(_MainTex, i.uv).a > 0.5 ? 1.0 : 0.0;
            }
            ENDCG
        }
    }
}
