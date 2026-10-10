Shader "KSP2/Environment/Atmosphere/HillaireScaled"
{
    Properties
    {
        _Transition ("Transition", Float) = 1
        _DitheringScale ("Dithering Scale", Float) = 1
        _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        Cull [_Cull]
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ OUTER_ATMO_MODE
            #pragma multi_compile _ MAP_MODE
            #pragma multi_compile _ GAS_GIANT_MODE
            #include "UnityCG.cginc"
            #include "HillaireAtmosphereCommon.hlsl"

            float3 _HillairePlanetCenterWorld;
            float _Transition;
            float _DitheringScale;
            float scale_factor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 normalWorld : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 center = _HillairePlanetCenterWorld;
                float3 up = normalize(i.worldPos - center);
                float3 viewDir = normalize(i.worldPos - _WorldSpaceCameraPos);
                float3 sunDir = HillaireSafeSunDirection();
                float limb = pow(saturate(1.0 - abs(dot(up, -viewDir))), 2.2);
                float sun = saturate(dot(up, sunDir) * 0.5 + 0.5);
                float3 baseColor = _HillaireRayleighScattering * 12.0 + _HillaireMieScattering * 7.0 * sun;
                float horizon = pow(saturate(1.0 - dot(up, sunDir) * 0.5), 2.0);
                float3 sunset = float3(1.0, 0.38, 0.12) * horizon * _HillaireMieScattering.r * 9.0;
                float density = saturate((_HillaireTopRadius - _HillaireBottomRadius) / max(0.001, _HillaireBottomRadius) * 5.0);

                #if defined(GAS_GIANT_MODE)
                density *= 2.0;
                baseColor = lerp(baseColor, _HillaireSolarIrradiance * 0.18, 0.35);
                #endif

                float shell = pow(saturate(limb), 1.55);
                float alpha = saturate(shell * density * _Transition * _HillaireTransmittanceTint * 0.45);
                float3 color = (baseColor + sunset) * _HillaireExposure.y * 0.04;
                return float4(color * alpha, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
