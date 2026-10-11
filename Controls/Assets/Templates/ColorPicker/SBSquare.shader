Shader "Redux/UI/ColorPicker/SBSquare"
{
    Properties
    {
        _Hue ("Hue", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        LOD 100
        Pass
        {
            // Drawn by ColorPicker with Graphics.Blit, so it keeps the plain mesh vertex input.
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _Hue;
            CBUFFER_END

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float saturation = i.uv.x;
                float value_linear = i.uv.y;

                float value_gamma_correct = pow(value_linear, 2.2);

                float3 hsv = float3(_Hue, saturation, value_gamma_correct);
                float3 rgb = hsv2rgb(hsv);

                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}