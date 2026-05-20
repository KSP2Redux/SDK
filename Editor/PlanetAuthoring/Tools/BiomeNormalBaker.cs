using System;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Bakes per-biome Mid and Large normal maps for a body from the small-tile normal stack (Mid) and the per-biome raw heightmap gradients (Large).
    /// </summary>
    /// <remarks>
    /// Mid is sampled triplanar in world-space at runtime, so the bake produces an adirectional
    /// detail tile per biome. Large is sampled biplanar at equirect tiling, so the bake produces
    /// a directional gradient-derived tile per biome from the Mid + Large raw heightmaps.
    /// The two bakes are independent leaves and can skip individually.
    /// </remarks>
    public static class BiomeNormalBaker
    {
        private const string ComputeShaderPath = "Assets/Modules/KSP2UnityTools/Assets/Shaders/PlanetAuthoring/AnalyticScaledSpaceBake.compute";
        private const int FallbackResolution = 1024;
        // Anchor-mask total-weight epsilon below which a biome is treated as empty.
        private const float BiomeEmptyEpsilon = 1e-3f;

        /// <summary>
        /// Per-biome bake outputs.
        /// </summary>
        /// <remarks>
        /// Caller owns disposal of every non-null entry in <see cref="MidPerBiome" /> and <see cref="LargePerBiome" />.
        /// </remarks>
        public struct Result
        {
            /// <summary>
            /// Mid normal textures, indexed by biome (0=R, 1=G, 2=B, 3=A).
            /// </summary>
            /// <remarks>
            /// Callers writing the textures to disk should skip null entries rather than substituting a flat normal. A null
            /// entry means the biome was empty in the mask or the Mid bake was skipped wholesale.
            /// </remarks>
            public Texture2D[] MidPerBiome;
            /// <summary>
            /// Large normal textures, indexed by biome (0=R, 1=G, 2=B, 3=A).
            /// </summary>
            /// <remarks>
            /// Callers writing the textures to disk should skip null entries rather than substituting a flat normal. A null
            /// entry means the biome was empty or had no raw heightmaps configured.
            /// </remarks>
            public Texture2D[] LargePerBiome;
            /// <summary>
            /// True when the whole bake skipped before producing anything.
            /// </summary>
            /// <remarks>
            /// When true, both <see cref="MidPerBiome" /> and <see cref="LargePerBiome" /> are null and the caller should
            /// leave existing material bindings alone.
            /// </remarks>
            public bool Skipped;
            /// <summary>
            /// Human-readable reason for skipping.
            /// </summary>
            /// <remarks>
            /// Empty string when <see cref="Skipped" /> is false.
            /// </remarks>
            public string SkipReason;
        }

        /// <summary>
        /// Runs the bake against the given body.
        /// </summary>
        /// <param name="pqsData">PQSData with heightMapInfo, biome mask, and a surface material with raw heightmap + small-normal bindings.</param>
        /// <param name="radius">Body radius in meters.</param>
        /// <returns>The per-biome bake outputs, or a skipped <see cref="Result" /> when preconditions are not met.</returns>
        public static Result Bake(PQSData pqsData, float radius)
        {
            if (pqsData == null)
                return Skip("PQSData was null");

            var surfaceMaterial = pqsData.materialSettings?.surfaceMaterial;
            if (surfaceMaterial == null)
                return Skip("no surface material");

            var heightMapInfo = pqsData.heightMapInfo;
            if (heightMapInfo == null)
                return Skip("no heightMapInfo");

            var biomeMask = heightMapInfo.mask;
            if (biomeMask == null)
                return Skip("biome mask missing");
            if (!biomeMask.isReadable)
                return Skip("biome mask not Read/Write enabled");

            var smallNormalArray = surfaceMaterial.GetTexture("_SmallNormalArray") as Texture2DArray;
            var hasMidPreconditions = smallNormalArray != null && HasAnyBiomeLayers(surfaceMaterial);

            var hasLargePreconditions = HasAnyGradience(heightMapInfo);

            if (!hasMidPreconditions && !hasLargePreconditions)
                return Skip("no small normal array and no gradience heightmaps");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
            if (compute == null)
                return Skip($"could not load compute shader at {ComputeShaderPath}");

            int midSide = ResolveMidResolution(heightMapInfo);
            int largeSide = ResolveLargeResolution(heightMapInfo);

            var anchorPixels = ComputeAnchorPixels(biomeMask, out var anchorTotalWeights);
            var anchorUVs = ComputeAnchorUVs(anchorPixels, biomeMask.width, biomeMask.height);

            var midResults = new Texture2D[4];
            var largeResults = new Texture2D[4];

            try
            {
                if (hasMidPreconditions)
                {
                    BakeMidNormals(compute, surfaceMaterial, heightMapInfo, smallNormalArray,
                        biomeMask, radius, midSide, anchorPixels, anchorTotalWeights, midResults);
                }

                if (hasLargePreconditions)
                {
                    BakeLargeNormals(compute, surfaceMaterial, heightMapInfo, radius, largeSide,
                        anchorUVs, anchorTotalWeights, largeResults);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                DisposeArray(midResults);
                DisposeArray(largeResults);
                return Skip($"{ex.GetType().Name}: {ex.Message}");
            }

            return new Result
            {
                MidPerBiome = hasMidPreconditions ? midResults : null,
                LargePerBiome = hasLargePreconditions ? largeResults : null,
                Skipped = false,
                SkipReason = string.Empty,
            };
        }

        private static Result Skip(string reason) => new() { Skipped = true, SkipReason = reason };

        private static bool HasAnyBiomeLayers(Material mat)
        {
            for (int b = 0; b < 4; b++)
            {
                var slices = mat.GetVector($"_SmallBiome{PlanetAuthoringNaming.BiomeChannels[b]}");
                if (slices.x >= 0f || slices.y >= 0f || slices.z >= 0f || slices.w >= 0f) return true;
            }
            return false;
        }

        private static bool HasAnyGradience(PQSData.HeightMapInfo hmi)
        {
            // Either large or mid is enough to run the Large bake. The bake gracefully handles
            // missing channels by sampling the flat-black fallback (no gradient contribution).
            if (hmi.largeR?.heightMap != null || hmi.largeG?.heightMap != null ||
                hmi.largeB?.heightMap != null || hmi.largeA?.heightMap != null) return true;
            if (hmi.mediumR?.heightMap != null || hmi.mediumG?.heightMap != null ||
                hmi.mediumB?.heightMap != null || hmi.mediumA?.heightMap != null) return true;
            return false;
        }

        private static int ResolveMidResolution(PQSData.HeightMapInfo hmi)
        {
            int side = 0;
            side = Mathf.Max(side, TextureSide(hmi.mediumR?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.mediumG?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.mediumB?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.mediumA?.heightMap));
            return side > 0 ? side : FallbackResolution;
        }

        private static int ResolveLargeResolution(PQSData.HeightMapInfo hmi)
        {
            int side = 0;
            side = Mathf.Max(side, TextureSide(hmi.largeR?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.largeG?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.largeB?.heightMap));
            side = Mathf.Max(side, TextureSide(hmi.largeA?.heightMap));
            return side > 0 ? side : FallbackResolution;
        }

        private static int TextureSide(Texture2D tex) => tex != null ? Mathf.Max(tex.width, tex.height) : 0;

        /// <summary>
        /// Returns the Large raw heightmap UV scale for the given biome.
        /// </summary>
        /// <param name="hmi">The body's height-map info container, or null.</param>
        /// <param name="b">Biome index (0=R, 1=G, 2=B, 3=A).</param>
        /// <returns>The biome's Large region uvScale, or 1 when the region or container is unassigned.</returns>
        /// <remarks>
        /// Single scalar because <c>PQSData.HeightRegion</c> carries a single uvScale value rather than a per-axis pair.
        /// Used by <see cref="Tools.BodySurfaceBakerOperation" /> when writing the <c>_LargeNormal*UVParams</c> bindings so
        /// the runtime tile rate matches the bake's authoring rate.
        /// </remarks>
        internal static float LargeUvScaleForBiome(PQSData.HeightMapInfo hmi, int b)
        {
            if (hmi == null) return 1f;
            if (b == 0) return hmi.largeR?.uvScale ?? 1f;
            if (b == 1) return hmi.largeG?.uvScale ?? 1f;
            if (b == 2) return hmi.largeB?.uvScale ?? 1f;
            return            hmi.largeA?.uvScale ?? 1f;
        }

        // True if biome b has at least one of large/mid raw heightmaps assigned. Used to gate
        // per-biome Large bakes - skip biomes where the source would just be flat-black.
        private static bool HasGradienceForBiome(PQSData.HeightMapInfo hmi, int b)
        {
            if (b == 0) return hmi.largeR?.heightMap != null || hmi.mediumR?.heightMap != null;
            if (b == 1) return hmi.largeG?.heightMap != null || hmi.mediumG?.heightMap != null;
            if (b == 2) return hmi.largeB?.heightMap != null || hmi.mediumB?.heightMap != null;
            return            hmi.largeA?.heightMap != null || hmi.mediumA?.heightMap != null;
        }

        // Pixel-weighted centroid of each biome channel in the mask. Returns the pixel coords
        // for use as the anchor in compute. anchorTotalWeights[b] = sum of biome b across the
        // whole mask, used to detect empty biomes that should output a flat-normal asset.
        private static Vector2Int[] ComputeAnchorPixels(Texture2D mask, out float[] anchorTotalWeights)
        {
            var anchors = new Vector2Int[4];
            anchorTotalWeights = new float[4];

            var pixels = mask.GetPixels();
            int w = mask.width;
            int h = mask.height;
            var sumW = new double[4];
            var sumX = new double[4];
            var sumY = new double[4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var c = pixels[y * w + x];
                    sumW[0] += c.r; sumX[0] += c.r * x; sumY[0] += c.r * y;
                    sumW[1] += c.g; sumX[1] += c.g * x; sumY[1] += c.g * y;
                    sumW[2] += c.b; sumX[2] += c.b * x; sumY[2] += c.b * y;
                    sumW[3] += c.a; sumX[3] += c.a * x; sumY[3] += c.a * y;
                }
            }

            for (int b = 0; b < 4; b++)
            {
                anchorTotalWeights[b] = (float)sumW[b];
                if (sumW[b] > BiomeEmptyEpsilon)
                {
                    int cx = Mathf.Clamp(Mathf.RoundToInt((float)(sumX[b] / sumW[b])), 0, w - 1);
                    int cy = Mathf.Clamp(Mathf.RoundToInt((float)(sumY[b] / sumW[b])), 0, h - 1);
                    anchors[b] = new Vector2Int(cx, cy);
                }
                else
                {
                    anchors[b] = Vector2Int.zero;
                }
            }
            return anchors;
        }

        private static Vector2[] ComputeAnchorUVs(Vector2Int[] anchorPixels, int maskW, int maskH)
        {
            var uvs = new Vector2[4];
            for (int b = 0; b < 4; b++)
            {
                uvs[b] = new Vector2(
                    (anchorPixels[b].x + 0.5f) / maskW,
                    (anchorPixels[b].y + 0.5f) / maskH);
            }
            return uvs;
        }

        // Dispatches CSBakeMid four times, once per biome. Each dispatch produces one biome's
        // Mid normal texture in midResults[b].
        private static void BakeMidNormals(ComputeShader compute, Material mat,
            PQSData.HeightMapInfo hmi, Texture2DArray smallNormalArray, Texture2D biomeMask,
            float radius, int side, Vector2Int[] anchorPixels, float[] anchorTotalWeights,
            Texture2D[] midResults)
        {
            int kernel = compute.FindKernel("CSBakeMid");

            // Shared raw heightmap + global heightmap bindings (also used by CSMain - already in shader).
            BindSharedHeightStack(compute, kernel, mat, hmi);
            compute.SetTexture(kernel, "_BiomeMaskTex", biomeMask);
            compute.SetTexture(kernel, "_SmallNormalArray", smallNormalArray);
            compute.SetFloat("_Radius", radius);
            compute.SetFloat("_HeightScale", hmi.heightMapScale);
            compute.SetInt("_MidOutWidth", side);
            compute.SetInt("_MidOutHeight", side);

            AnalyticScaledSpaceSampler.BindPerLayerArrays(compute, mat);

            int gx = (side + 7) / 8;
            int gy = (side + 7) / 8;

            for (int b = 0; b < 4; b++)
            {
                if (anchorTotalWeights[b] <= BiomeEmptyEpsilon)
                {
                    midResults[b] = null;
                    continue;
                }

                var rt = AnalyticScaledSpaceSampler.CreateRwRT(side, side, RenderTextureFormat.ARGB32);
                try
                {
                    compute.SetTexture(kernel, "_OutMidNormal", rt);
                    compute.SetInt("_MidBakeBiomeIndex", b);
                    compute.SetInts("_MidBakeAnchorPixel", anchorPixels[b].x, anchorPixels[b].y);
                    compute.Dispatch(kernel, gx, gy, 1);
                    midResults[b] = AnalyticScaledSpaceSampler.ReadbackToTexture(rt, TextureFormat.RGBA32, linear: true);
                }
                finally
                {
                    AnalyticScaledSpaceSampler.ReleaseRT(rt);
                }
            }
        }

        // Dispatches CSBakeLarge four times, once per biome.
        private static void BakeLargeNormals(ComputeShader compute, Material mat,
            PQSData.HeightMapInfo hmi, float radius, int side,
            Vector2[] anchorUVs, float[] anchorTotalWeights, Texture2D[] largeResults)
        {
            int kernel = compute.FindKernel("CSBakeLarge");

            // Raw heightmap textures and per-biome scales (UV + height) shared with CSBakeMid.
            BindHeightmapsToKernel(compute, kernel, hmi);
            BindHeightmapUniformsToKernel(compute, hmi);

            compute.SetFloat("_Radius", radius);
            compute.SetInt("_LargeOutWidth", side);
            compute.SetInt("_LargeOutHeight", side);
            compute.SetFloat("_LargeHeightWeight", 1.0f);
            compute.SetFloat("_MidHeightWeight", 1.0f);

            int gx = (side + 7) / 8;
            int gy = (side + 7) / 8;

            for (int b = 0; b < 4; b++)
            {
                if (anchorTotalWeights[b] <= BiomeEmptyEpsilon || !HasGradienceForBiome(hmi, b))
                {
                    largeResults[b] = null;
                    continue;
                }

                // Tile-span in equirect UV = one heightmap tile's worth of surface. The Large
                // raw heightmap is the dominant signal we're sampling here, so its uvScale is
                // the natural tile rate of the output. Mid is a supplementary higher-frequency
                // overlay, so its uvScale governs its own per-pixel sample step but not the
                // output span.
                float heightmapUvScale = LargeUvScaleForBiome(hmi, b);
                heightmapUvScale = Mathf.Max(heightmapUvScale, 0.01f);
                var tileSpan = new Vector2(1f / heightmapUvScale, 1f / heightmapUvScale);

                var rt = AnalyticScaledSpaceSampler.CreateRwRT(side, side, RenderTextureFormat.ARGB32);
                try
                {
                    compute.SetTexture(kernel, "_OutLargeNormal", rt);
                    compute.SetInt("_LargeBakeBiomeIndex", b);
                    compute.SetVector("_LargeBakeAnchorUV", anchorUVs[b]);
                    compute.SetVector("_LargeBakeTileSpanUV", tileSpan);
                    compute.Dispatch(kernel, gx, gy, 1);
                    largeResults[b] = AnalyticScaledSpaceSampler.ReadbackToTexture(rt, TextureFormat.RGBA32, linear: true);
                }
                finally
                {
                    AnalyticScaledSpaceSampler.ReleaseRT(rt);
                }
            }
        }

        // Binds the global heightmap + per-biome raw heightmaps + their UV/height scales.
        // CSBakeMid and CSBakeLarge both consume this stack via ComposedHeightAt / the height
        // samplers, so both kernels need it.
        private static void BindSharedHeightStack(ComputeShader compute, int kernel, Material mat,
            PQSData.HeightMapInfo hmi)
        {
            compute.SetTexture(kernel, "_GlobalHeightMap", FlatOr(hmi.globalHeightMap));
            BindHeightmapsToKernel(compute, kernel, hmi);
            BindHeightmapUniformsToKernel(compute, hmi);
        }

        // Binds the 8 per-biome raw heightmaps (Large R/G/B/A + Mid R/G/B/A) to one kernel.
        private static void BindHeightmapsToKernel(ComputeShader compute, int kernel, PQSData.HeightMapInfo hmi)
        {
            compute.SetTexture(kernel, "_LargeHeightR", FlatOr(hmi.largeR?.heightMap));
            compute.SetTexture(kernel, "_LargeHeightG", FlatOr(hmi.largeG?.heightMap));
            compute.SetTexture(kernel, "_LargeHeightB", FlatOr(hmi.largeB?.heightMap));
            compute.SetTexture(kernel, "_LargeHeightA", FlatOr(hmi.largeA?.heightMap));
            compute.SetTexture(kernel, "_MidHeightR",   FlatOr(hmi.mediumR?.heightMap));
            compute.SetTexture(kernel, "_MidHeightG",   FlatOr(hmi.mediumG?.heightMap));
            compute.SetTexture(kernel, "_MidHeightB",   FlatOr(hmi.mediumB?.heightMap));
            compute.SetTexture(kernel, "_MidHeightA",   FlatOr(hmi.mediumA?.heightMap));
        }

        // Binds the 4 per-biome scalar uniforms (UV scale + height scale, large + mid) shared
        // across kernels. Not kernel-local, so no kernel index needed.
        private static void BindHeightmapUniformsToKernel(ComputeShader compute, PQSData.HeightMapInfo hmi)
        {
            compute.SetVector("_LargeHeightMapUVScales", new Vector4(
                hmi.largeR?.uvScale ?? 1f,
                hmi.largeG?.uvScale ?? 1f,
                hmi.largeB?.uvScale ?? 1f,
                hmi.largeA?.uvScale ?? 1f));
            compute.SetVector("_MediumHeightMapUVScales", new Vector4(
                hmi.mediumR?.uvScale ?? 1f,
                hmi.mediumG?.uvScale ?? 1f,
                hmi.mediumB?.uvScale ?? 1f,
                hmi.mediumA?.uvScale ?? 1f));
            compute.SetVector("_LargeHeightScales", new Vector4(
                hmi.largeR?.heightScale ?? 0f,
                hmi.largeG?.heightScale ?? 0f,
                hmi.largeB?.heightScale ?? 0f,
                hmi.largeA?.heightScale ?? 0f));
            compute.SetVector("_MidHeightScales", new Vector4(
                hmi.mediumR?.heightScale ?? 0f,
                hmi.mediumG?.heightScale ?? 0f,
                hmi.mediumB?.heightScale ?? 0f,
                hmi.mediumA?.heightScale ?? 0f));
        }

        // Falls back to a 1x1 black texture when a raw heightmap slot is unbound, matching the
        // existing scaled-bake's behavior (unbound slots contribute zero to the composed height).
        private static Texture FlatOr(Texture tex)
        {
            if (tex != null) return tex;
            return GetFlatBlackFallback();
        }

        private static Texture2D _flatBlackFallback;

        private static Texture2D GetFlatBlackFallback()
        {
            if (_flatBlackFallback != null) return _flatBlackFallback;
            _flatBlackFallback = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "BiomeNormalBakerFlatBlack",
            };
            _flatBlackFallback.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            _flatBlackFallback.Apply();
            return _flatBlackFallback;
        }

        private static void DisposeArray(Texture2D[] arr)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) UnityEngine.Object.DestroyImmediate(arr[i]);
                arr[i] = null;
            }
        }
    }
}
