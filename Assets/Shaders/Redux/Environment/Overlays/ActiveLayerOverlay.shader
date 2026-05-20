Shader "Redux/PlanetAuthoring/Overlays/ActiveLayerOverlay"
{
    // Visualizes which of the 16 small-biome layers wins per pixel.
    //
    // The surface shader's prepass already runs the window evaluation (height /
    // slope trapezoid windows + gate + grad-weighted slope) and writes per-layer
    // weights into screen-space RTs (_LocalSpacePrepassTex0..3, four channels
    // per RT = R/G/B/A biomes x 4 layers each). We sample those RTs, multiply
    // by the per-biome _BiomeMaskTex weight, take argmax across all 16, and
    // colorize via a 16-entry palette (hue per biome, brightness per layer).
    //
    // PQSDecalController and the surface material set the prepass RTs each frame.
    // The PreviewOverlayManager mirrors them onto this overlay's material before
    // each draw via the same texture handles.
    Properties
    {
        _Strength ("Strength", Range(0, 1)) = 0.7
        _WeightFloor ("Weight floor (treat below as no-layer)", Range(0, 1)) = 0.001

        // Color the four biome channels independently. Within a biome, brightness
        // scales by layer index so layer 1 reads darkest, layer 4 lightest.
        _BiomeColorR ("Biome R color", Color) = (1.00, 0.30, 0.30, 1.0)
        _BiomeColorG ("Biome G color", Color) = (0.30, 1.00, 0.30, 1.0)
        _BiomeColorB ("Biome B color", Color) = (0.30, 0.50, 1.00, 1.0)
        _BiomeColorA ("Biome A color", Color) = (1.00, 0.95, 0.30, 1.0)
        _LayerBrightness1 ("Layer 1 brightness", Range(0.1, 1.0)) = 0.35
        _LayerBrightness2 ("Layer 2 brightness", Range(0.1, 1.0)) = 0.55
        _LayerBrightness3 ("Layer 3 brightness", Range(0.1, 1.0)) = 0.80
        _LayerBrightness4 ("Layer 4 brightness", Range(0.1, 1.0)) = 1.00

        // Per-(biome, layer) enable mask. Each component is 0 (disabled) or 1 (enabled).
        // Disabled layers are zeroed out before argmax so they never win the pixel.
        _LayerEnableR ("Biome R layers enabled (xyzw = L1..L4)", Vector) = (1, 1, 1, 1)
        _LayerEnableG ("Biome G layers enabled (xyzw = L1..L4)", Vector) = (1, 1, 1, 1)
        _LayerEnableB ("Biome B layers enabled (xyzw = L1..L4)", Vector) = (1, 1, 1, 1)
        _LayerEnableA ("Biome A layers enabled (xyzw = L1..L4)", Vector) = (1, 1, 1, 1)

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

            sampler2D _LocalSpacePrepassTex0;
            sampler2D _LocalSpacePrepassTex1;
            sampler2D _LocalSpacePrepassTex2;
            sampler2D _LocalSpacePrepassTex3;
            sampler2D _BiomeMaskTex;

            float  _Strength;
            float  _WeightFloor;
            float4 _BiomeColorR;
            float4 _BiomeColorG;
            float4 _BiomeColorB;
            float4 _BiomeColorA;
            float  _LayerBrightness1;
            float  _LayerBrightness2;
            float  _LayerBrightness3;
            float  _LayerBrightness4;
            float4 _LayerEnableR;
            float4 _LayerEnableG;
            float4 _LayerEnableB;
            float4 _LayerEnableA;

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 screen : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                QuadMeshData q = GetQuadMeshVert(v);
                v2f o;
                o.vertex = UnityObjectToClipPos(q.position);
                o.uv     = q.uv;
                o.screen = ComputeScreenPos(o.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 screenUV = i.screen.xy / max(i.screen.w, 1e-5);

                // Per-biome activeness from the planet's biome mask.
                float4 biomeMask = tex2D(_BiomeMaskTex, i.uv);

                // 16 weights = 4 layers x 4 biomes. Each prepass texture stores
                // one biome's 4-layer weights as the four channels of a float4. Per-layer
                // enable masks zero out layers the artist has hidden in the Preview Controls grid.
                float4 wR = tex2D(_LocalSpacePrepassTex0, screenUV) * biomeMask.r * _LayerEnableR;
                float4 wG = tex2D(_LocalSpacePrepassTex1, screenUV) * biomeMask.g * _LayerEnableG;
                float4 wB = tex2D(_LocalSpacePrepassTex2, screenUV) * biomeMask.b * _LayerEnableB;
                float4 wA = tex2D(_LocalSpacePrepassTex3, screenUV) * biomeMask.a * _LayerEnableA;

                // Find the winning (biome, layer) pair by argmax over all 16 values.
                float bestWeight = _WeightFloor;
                int   bestBiome  = -1;
                int   bestLayer  = -1;
                #define _PICK(biomeIdx, layerIdx, val) \
                    if (val > bestWeight) { bestWeight = val; bestBiome = biomeIdx; bestLayer = layerIdx; }

                _PICK(0, 0, wR.x) _PICK(0, 1, wR.y) _PICK(0, 2, wR.z) _PICK(0, 3, wR.w)
                _PICK(1, 0, wG.x) _PICK(1, 1, wG.y) _PICK(1, 2, wG.z) _PICK(1, 3, wG.w)
                _PICK(2, 0, wB.x) _PICK(2, 1, wB.y) _PICK(2, 2, wB.z) _PICK(2, 3, wB.w)
                _PICK(3, 0, wA.x) _PICK(3, 1, wA.y) _PICK(3, 2, wA.z) _PICK(3, 3, wA.w)
                #undef _PICK

                if (bestBiome < 0)
                {
                    discard;
                }

                float3 biomeColor = (bestBiome == 0) ? _BiomeColorR.rgb
                                  : (bestBiome == 1) ? _BiomeColorG.rgb
                                  : (bestBiome == 2) ? _BiomeColorB.rgb
                                                     : _BiomeColorA.rgb;
                float brightness = (bestLayer == 0) ? _LayerBrightness1
                                 : (bestLayer == 1) ? _LayerBrightness2
                                 : (bestLayer == 2) ? _LayerBrightness3
                                                    : _LayerBrightness4;
                float3 rgb = biomeColor * brightness;
                return OverlayCompose(rgb, _Strength);
            }
            ENDHLSL
        }
    }
}
