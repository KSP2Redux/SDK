Shader "KSP2/Aerodynamics/DragRender"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _BumpMap;
            float4 _BumpMap_ST;
            float _dragCubeNearClip;
            float _dragCubeFarClip;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 mainUv : TEXCOORD0;
                float2 bumpUv : TEXCOORD1;
                float3 viewNormal : TEXCOORD2;
                float3 viewTangent : TEXCOORD3;
                float3 viewBinormal : TEXCOORD4;
                float viewDepth : TEXCOORD5;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.mainUv = TRANSFORM_TEX(v.uv, _MainTex);
                o.bumpUv = TRANSFORM_TEX(v.uv, _BumpMap);

                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent.xyz);
                float tangentSign = v.tangent.w * unity_WorldTransformParams.w;
                float3 worldBinormal = cross(worldNormal, worldTangent) * tangentSign;

                o.viewNormal = normalize(mul((float3x3)UNITY_MATRIX_V, worldNormal));
                o.viewTangent = normalize(mul((float3x3)UNITY_MATRIX_V, worldTangent));
                o.viewBinormal = normalize(mul((float3x3)UNITY_MATRIX_V, worldBinormal));

                float3 viewPos = UnityObjectToViewPos(v.vertex);
                o.viewDepth = -viewPos.z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainSample = tex2D(_MainTex, i.mainUv);
                clip(mainSample.a - 0.001);

                float3 tangentNormal = UnpackNormal(tex2D(_BumpMap, i.bumpUv));
                float3 viewNormal = normalize(
                    tangentNormal.x * i.viewTangent +
                    tangentNormal.y * i.viewBinormal +
                    tangentNormal.z * i.viewNormal
                );

                float depthRange = max(_dragCubeFarClip - _dragCubeNearClip, 0.0001);
                float normalizedDepth = saturate((i.viewDepth - _dragCubeNearClip) / depthRange);
                float drag = saturate(abs(viewNormal.z));
                return fixed4(drag, normalizedDepth, 0, mainSample.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
