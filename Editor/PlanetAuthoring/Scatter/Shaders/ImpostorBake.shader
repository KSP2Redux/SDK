// ============================================================================
// ImpostorBake.shader
//
// Editor-only helpers for ScatterImpostorBaker, each a single target blit.
//
// Pack builds one of the four impostor textures from the captured G-buffer
// atlas, chosen by _PackOutput: 0 AlbedoAlpha, 1 NormalDepth,
// 2 SpecularSmoothness, 3 EmissionOcclusion. The atlas comes from the scatter
// stand-ins' ImpostorCapture pass.
// Dilate grows colour one texel outward from the covered area, and GrowMask
// grows the coverage mask to match, so mip levels and bilinear taps at the
// silhouette never pull in the cleared background.
//
// URP: the baker draws these with Graphics.Blit, so every pass keeps Blit's
// vertex contract (POSITION + TEXCOORD0, source in _MainTex).
// ============================================================================
Shader "Hidden/Redux/ImpostorBake"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        struct ImpostorBakeAttributes
        {
            float4 positionOS : POSITION;
            float2 uv         : TEXCOORD0;
        };

        struct ImpostorBakeVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
        };

        ImpostorBakeVaryings ImpostorBake_Vertex(ImpostorBakeAttributes input)
        {
            ImpostorBakeVaryings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Pack"

            HLSLPROGRAM
            #pragma vertex ImpostorBake_Vertex
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D(_GBuffer0);   SAMPLER(sampler_GBuffer0);
            TEXTURE2D(_GBuffer1);   SAMPLER(sampler_GBuffer1);
            TEXTURE2D(_GBuffer2);   SAMPLER(sampler_GBuffer2);
            TEXTURE2D(_GBuffer3);   SAMPLER(sampler_GBuffer3);
            TEXTURE2D(_BakeDepth);  SAMPLER(sampler_BakeDepth);
            // Set with Material.SetInt, which stores a float.
            float _PackOutput;

            float4 frag(ImpostorBakeVaryings i) : SV_Target
            {
                float4 gBuffer0 = SAMPLE_TEXTURE2D(_GBuffer0, sampler_GBuffer0, i.uv);
                float4 gBuffer2 = SAMPLE_TEXTURE2D(_GBuffer2, sampler_GBuffer2, i.uv);

                // The capture pass writes 1 to the normal buffer's alpha wherever it draws.
                float coverage = gBuffer2.a > 0.5 ? 1.0 : 0.0;

                int packOutput = (int)round(_PackOutput);
                float4 output;
                if (packOutput == 0)
                {
                    output = float4(LinearToSRGB(gBuffer0.rgb), 1.0);
                }
                else if (packOutput == 1)
                {
                    // The bake camera's depth range is exactly _DepthSize, centred on the frame
                    // plane, so the stored depth is how far toward the camera the texel sits.
                    float rawDepth = SAMPLE_TEXTURE2D(_BakeDepth, sampler_BakeDepth, i.uv).r;
                    #if UNITY_REVERSED_Z
                    float depth = rawDepth;
                    #else
                    float depth = 1.0 - rawDepth;
                    #endif
                    output = float4(gBuffer2.rgb, depth);
                }
                else if (packOutput == 2)
                {
                    output = SAMPLE_TEXTURE2D(_GBuffer1, sampler_GBuffer1, i.uv);
                }
                else
                {
                    output = float4(SAMPLE_TEXTURE2D(_GBuffer3, sampler_GBuffer3, i.uv).rgb, gBuffer0.a);
                }

                return output * coverage;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Dilate"

            HLSLPROGRAM
            #pragma vertex ImpostorBake_Vertex
            #pragma fragment frag

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            TEXTURE2D(_Coverage);   SAMPLER(sampler_Coverage);
            float _PreserveAlpha;

            float4 frag(ImpostorBakeVaryings i) : SV_Target
            {
                float4 centre = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                if (SAMPLE_TEXTURE2D(_Coverage, sampler_Coverage, i.uv).r > 0.5)
                    return centre;

                float4 sum = 0.0;
                float count = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 uv = i.uv + float2(x, y) * _MainTex_TexelSize.xy;
                        float covered = SAMPLE_TEXTURE2D(_Coverage, sampler_Coverage, uv).r > 0.5 ? 1.0 : 0.0;
                        sum += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * covered;
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
            ENDHLSL
        }

        Pass
        {
            Name "GrowMask"

            HLSLPROGRAM
            #pragma vertex ImpostorBake_Vertex
            #pragma fragment frag

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            float4 frag(ImpostorBakeVaryings i) : SV_Target
            {
                float covered = 0.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        covered = max(covered, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(x, y) * _MainTex_TexelSize.xy).r);
                    }
                }

                return covered;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Coverage"

            HLSLPROGRAM
            #pragma vertex ImpostorBake_Vertex
            #pragma fragment frag

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

            float4 frag(ImpostorBakeVaryings i) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a > 0.5 ? 1.0 : 0.0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
