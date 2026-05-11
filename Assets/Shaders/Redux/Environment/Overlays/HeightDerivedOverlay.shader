Shader "Redux/PlanetAuthoring/Overlays/HeightDerivedOverlay"
{
    // Visualizes signals derived from the quad-mesh geometry.
    //   _Mode 0  Slope:   green (flat) to red (vertical) ramp.
    //   _Mode 1  Contour: thin topographic contour lines at multiples of _BandHeight,
    //            with every _MajorEvery-th line highlighted as a major contour.
    Properties
    {
        // 0 = Slope, 1 = Altitude contour lines.
        [IntRange] _Mode ("Mode (0=Slope, 1=Altitude contours)", Range(0, 1)) = 0
        _Strength ("Strength", Range(0, 1)) = 0.7

        // Slope-mode params.
        _SlopeLowColor  ("Slope: flat color",     Color) = (0.10, 0.85, 0.20, 1.0)
        _SlopeHighColor ("Slope: vertical color", Color) = (1.00, 0.10, 0.10, 1.0)
        _SlopeGamma     ("Slope: ramp gamma",     Range(0.2, 4.0)) = 1.0

        // Contour-mode params. Radius is the surface-zero radial distance in object-space
        // units, supplied by the C# overlay impl from CoreCelestialBodyData.
        _PlanetRadius   ("Planet radius (object units)", float) = 600000
        _BandHeight     ("Minor contour interval (m)",   float) = 500
        _MinorColor     ("Minor contour color",          Color) = (0.85, 0.85, 0.95, 1.0)
        _MajorEvery     ("Major contour every N bands",  Range(0, 32)) = 5
        _MajorColor     ("Major contour color",          Color) = (1.0, 0.85, 0.30, 1.0)
        _LineWidth      ("Line width (band-units)",      Range(0.0, 0.5)) = 0.08
        _LineSoftness   ("Line edge softness",           Range(0.0, 2.0)) = 1.0

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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _USE_PQS_BUFFER

            #include "OverlayCommon.hlsl"

            float  _Mode;
            float  _Strength;

            float4 _SlopeLowColor;
            float4 _SlopeHighColor;
            float  _SlopeGamma;

            float  _PlanetRadius;
            float  _BandHeight;
            float4 _MinorColor;
            float  _MajorEvery;
            float4 _MajorColor;
            float  _LineWidth;
            float  _LineSoftness;

            // Slope sources its normal from the surface shader's prepass world-normal RT
            // (LARGE + MID heightmap normals folded in) so artists see the same slope the
            // small-biome layer windows are evaluated against, not raw mesh facets.
            sampler2D _LocalSpacePrepassTex4;

            // The vertex stage adds a screen-pos varying so the fragment can sample the
            // prepass RT at this fragment's screen UV.
            struct slope_v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 posH   : TEXCOORD1;
                float4 screen : TEXCOORD2;
            };

            slope_v2f vert(appdata v)
            {
                QuadMeshData q = GetQuadMeshVert(v);
                slope_v2f o;
                o.vertex = UnityObjectToClipPos(q.position);
                o.uv     = q.uv;
                o.posH   = float4(q.position, q.height.w);
                o.screen = ComputeScreenPos(o.vertex);
                return o;
            }

            float4 EvalSlope(slope_v2f i)
            {
                float2 screenUV = i.screen.xy / max(i.screen.w, 1e-5);
                // Prepass tex4 stores the reconstructed terrain normal in world space.
                float3 worldN = normalize(tex2D(_LocalSpacePrepassTex4, screenUV).xyz * 2.0 - 1.0);
                // For an authoring scene with the body at world origin, posH.xyz is the
                // world-space radial vector; normalize gives the surface up direction.
                float3 radial = normalize(i.posH.xyz);
                float s = saturate(1.0 - abs(dot(worldN, radial)));
                float t = pow(s, _SlopeGamma);
                float3 rgb = lerp(_SlopeLowColor.rgb, _SlopeHighColor.rgb, t);
                return OverlayCompose(rgb, _Strength);
            }

            float4 EvalContour(slope_v2f i)
            {
                float altitude = OverlayRadial(i.posH.xyz) - _PlanetRadius;
                float bandIndexF = altitude / max(_BandHeight, 1e-3);

                // Distance to the nearest contour line, in band-units.
                float dist = abs(bandIndexF - round(bandIndexF));

                // Screen-space-stable line thickness via fwidth.
                float aaf = fwidth(bandIndexF) * _LineSoftness;
                float halfWidth = max(_LineWidth * 0.5, aaf);
                float coverage = 1.0 - smoothstep(halfWidth - aaf, halfWidth + aaf, dist);
                if (coverage <= 0.001)
                    return float4(0, 0, 0, 0);

                // Major contour distinction: every _MajorEvery-th line uses the major color.
                int nearestBand = (int)round(bandIndexF);
                int majorStep = (int)round(_MajorEvery);
                bool isMajor = false;
                if (majorStep > 0)
                {
                    int mod = nearestBand % majorStep;
                    if (mod < 0) mod += majorStep;
                    isMajor = (mod == 0);
                }
                float3 lineColor = isMajor ? _MajorColor.rgb : _MinorColor.rgb;

                return OverlayCompose(lineColor, _Strength * coverage);
            }

            // Explicit if/else; HLSL ternary evaluates BOTH branches, which would let
            // EvalContour's pixel-killing path leak into slope mode.
            float4 frag(slope_v2f i) : SV_Target
            {
                int mode = (int)round(_Mode);
                if (mode == 0)
                    return EvalSlope(i);
                return EvalContour(i);
            }
            ENDHLSL
        }
    }
}
