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
        // 0 = continuous ramp. Any positive value bins the displayed slope to multiples
        // of that step (in the same stretched degrees the runtime/bake trapezoid uses).
        _SlopeStepDeg   ("Slope: quantization step (deg, 0=continuous)", Range(0, 30)) = 0

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
            #pragma multi_compile_local _ REDUX_GRADIENCE

            #include "OverlayCommon.hlsl"

            float  _Mode;
            float  _Strength;

            float4 _SlopeLowColor;
            float4 _SlopeHighColor;
            float  _SlopeGamma;
            float  _SlopeStepDeg;

            float  _PlanetRadius;
            float  _BandHeight;
            float4 _MinorColor;
            float  _MajorEvery;
            float4 _MajorColor;
            float  _LineWidth;
            float  _LineSoftness;

            // Slope samples the same gradience heightmaps the runtime prepass samples so
            // the overlay shows exactly what the trapezoid window will gate against. The
            // gradience texture's RGBA packs (sample_+u, sample_+v, sample_-u, sample_-v),
            // so xy-zw gives the finite-difference gradient directly. Subzones and decals
            // are intentionally not contributed -- the overlay reads the macro slope from
            // Large + Mid with unit weights, matching the bake's anchor evaluation.
            Texture2D<float4>  _LargeGradienceR;
            Texture2D<float4>  _LargeGradienceG;
            Texture2D<float4>  _LargeGradienceB;
            Texture2D<float4>  _LargeGradienceA;
            Texture2D<float4>  _MidGradienceR;
            Texture2D<float4>  _MidGradienceG;
            Texture2D<float4>  _MidGradienceB;
            Texture2D<float4>  _MidGradienceA;
            Texture2D<float4>  _GlobalGradienceTex;
            Texture2D<float4>  _BiomeMaskTex;
            SamplerState       sampler_LinearRepeat;
            float4             _LargeHeightMapUVScales;
            float4             _MediumHeightMapUVScales;

            struct slope_v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 posH   : TEXCOORD1;
            };

            slope_v2f vert(appdata v)
            {
                QuadMeshData q = GetQuadMeshVert(v);
                slope_v2f o;
                o.vertex = UnityObjectToClipPos(q.position);
                o.uv     = q.uv;
                o.posH   = float4(q.position, q.height.w);
                return o;
            }

            float4 EvalSlope(slope_v2f i)
            {
                float2 uv = i.uv;

                // Per-pixel normalized biome weight, matching Prepass.cginc:343-346.
                float4 mask        = _BiomeMaskTex.Sample(sampler_LinearRepeat, uv);
                float  maskSum     = max(mask.x + mask.y + mask.z + mask.w, 0.001);
                float4 biomeWeight = mask / maskSum;

                // Biome-weighted Large + Mid gradience accumulation. Each Sample returns
                // (sample_+u, sample_+v, sample_-u, sample_-v) packed in xyzw.
                float4 large = 0;
                float4 mid   = 0;
                if (biomeWeight.x > 0.001)
                {
                    large += biomeWeight.x * _LargeGradienceR.Sample(sampler_LinearRepeat, uv * _LargeHeightMapUVScales.x);
                    mid   += biomeWeight.x * _MidGradienceR.Sample(sampler_LinearRepeat,   uv * _MediumHeightMapUVScales.x);
                }
                if (biomeWeight.y > 0.001)
                {
                    large += biomeWeight.y * _LargeGradienceG.Sample(sampler_LinearRepeat, uv * _LargeHeightMapUVScales.y);
                    mid   += biomeWeight.y * _MidGradienceG.Sample(sampler_LinearRepeat,   uv * _MediumHeightMapUVScales.y);
                }
                if (biomeWeight.z > 0.001)
                {
                    large += biomeWeight.z * _LargeGradienceB.Sample(sampler_LinearRepeat, uv * _LargeHeightMapUVScales.z);
                    mid   += biomeWeight.z * _MidGradienceB.Sample(sampler_LinearRepeat,   uv * _MediumHeightMapUVScales.z);
                }
                if (biomeWeight.w > 0.001)
                {
                    large += biomeWeight.w * _LargeGradienceA.Sample(sampler_LinearRepeat, uv * _LargeHeightMapUVScales.w);
                    mid   += biomeWeight.w * _MidGradienceA.Sample(sampler_LinearRepeat,   uv * _MediumHeightMapUVScales.w);
                }

#ifdef REDUX_GRADIENCE
                // Redux 2-channel encoding: each source's .rg stores (du*0.5+0.5, dv*0.5+0.5).
                // Biome-weighted accumulation preserves the offset; recover via (rg - 0.5) * 2.
                float2 grad = (large.xy - 0.5) * 2.0 + (mid.xy - 0.5) * 2.0;
                float4 globalSample = _GlobalGradienceTex.Sample(sampler_LinearRepeat, uv);
                grad += (globalSample.xy - 0.5) * 2.0;
                float slopeDeg = degrees(atan(length(grad)));
#else
                // Stock 4-channel signed-split: xy - zw recovers signed gradient per source.
                float2 grad = (large.xy - large.zw) + (mid.xy - mid.zw);
                float slopeDeg = min(length(grad), 1.0) * 90.0;
#endif
                if (_SlopeStepDeg > 0.001)
                    slopeDeg = floor(slopeDeg / _SlopeStepDeg) * _SlopeStepDeg;

                float t = pow(saturate(slopeDeg / 90.0), _SlopeGamma);
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
