using System;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Bakes per-biome raw heightmaps (and the global heightmap, in Redux mode) into the gradience textures the surface shader samples.
    /// </summary>
    /// <remarks>
    /// The output encoding follows the surface material's REDUX_GRADIENCE keyword state. Stock
    /// mode produces 4-channel signed-split textures whose <c>length(xy - zw)</c> equals
    /// <c>slope_deg / 90</c> for the source alone. Redux mode produces 2-channel signed-around-0.5
    /// textures whose <c>(rg - 0.5) * 2</c> equals the true gradient (dh/du, dh/dv) in m/m, so the
    /// runtime sums per-source gradients as vectors and computes the composed surface slope via
    /// <c>atan(length(sum))</c>. The U axis is flipped in both encodings to match the stock UV
    /// convention.
    /// </remarks>
    public static class GradienceBaker
    {
        private const string ComputeShaderPath = "Assets/Modules/KSP2UnityTools/Assets/Shaders/PlanetAuthoring/GradienceBake.compute";

        /// <summary>
        /// Per-biome bake outputs.
        /// </summary>
        /// <remarks>
        /// Caller owns disposal of every non-null entry in <see cref="LargePerBiome" />, <see cref="MidPerBiome" />, and <see cref="GlobalGradience" />.
        /// </remarks>
        public struct Result
        {
            /// <summary>
            /// Large gradience textures, indexed by biome (0=R, 1=G, 2=B, 3=A).
            /// </summary>
            /// <remarks>
            /// Null where the biome's raw heightmap was unassigned. Callers writing the textures to disk should skip null entries.
            /// </remarks>
            public Texture2D[] LargePerBiome;
            /// <summary>
            /// Mid gradience textures, indexed by biome (0=R, 1=G, 2=B, 3=A).
            /// </summary>
            /// <remarks>
            /// Null where the biome's raw heightmap was unassigned. Callers writing the textures to disk should skip null entries.
            /// </remarks>
            public Texture2D[] MidPerBiome;
            /// <summary>
            /// Global gradience texture covering the whole planet.
            /// </summary>
            /// <remarks>
            /// Only baked when the surface material's REDUX_GRADIENCE keyword is enabled. Null otherwise.
            /// </remarks>
            public Texture2D GlobalGradience;
            /// <summary>
            /// True when the bake produced Redux-encoded output instead of stock-encoded.
            /// </summary>
            /// <remarks>
            /// Redux is the 2-channel signed-around-0.5 encoding. Stock is the 4-channel signed-split encoding. Downstream writers consume this to pick the matching importer config.
            /// </remarks>
            public bool ReduxEncoding;
            /// <summary>
            /// True when the bake skipped wholesale (no surface material, compute shader missing, etc).
            /// </summary>
            public bool Skipped;
            /// <summary>
            /// Human-readable reason for skipping.
            /// </summary>
            /// <remarks>
            /// Empty when <see cref="Skipped" /> is false.
            /// </remarks>
            public string SkipReason;
        }

        /// <summary>
        /// Runs the gradience bake against the given body.
        /// </summary>
        /// <param name="pqsData">PQSData carrying the heightMapInfo with per-biome raw heightmaps and height scales.</param>
        /// <param name="radius">Body radius in meters, used to convert the gradient to a true slope tangent.</param>
        /// <returns>Per-biome baked gradience textures, or a skipped <see cref="Result" /> when preconditions are not met.</returns>
        public static Result Bake(PQSData pqsData, float radius)
        {
            if (pqsData == null)
                return Skip("PQSData was null");

            var hmi = pqsData.heightMapInfo;
            if (hmi == null)
                return Skip("no heightMapInfo");

            if (!HasAnyRawHeightmap(hmi))
                return Skip("no biome raw heightmaps assigned");

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath);
            if (compute == null)
                return Skip($"could not load compute shader at {ComputeShaderPath}");

            int kernel = compute.FindKernel("CSBakeGradience");

            // Match the bake encoding to the surface material's keyword state. When
            // REDUX_GRADIENCE is on, store true (dh/du, dh/dv) so per-source diffs sum
            // as vectors. When off, store slope_deg / 90 (the stock-compatible scalar).
            var surfaceMaterial = pqsData.materialSettings?.surfaceMaterial;
            bool reduxGradience = surfaceMaterial != null && surfaceMaterial.IsKeywordEnabled("REDUX_GRADIENCE");
            var reduxKeyword = new LocalKeyword(compute, "REDUX_GRADIENCE");
            if (reduxGradience)
                compute.EnableKeyword(reduxKeyword);
            else
                compute.DisableKeyword(reduxKeyword);

            var largeResults = new Texture2D[4];
            var midResults = new Texture2D[4];
            Texture2D globalResult = null;

            try
            {
                for (int b = 0; b < 4; b++)
                {
                    var largeRegion = GetLargeRegion(hmi, b);
                    if (largeRegion?.heightMap != null)
                    {
                        largeResults[b] = BakeOne(compute, kernel, largeRegion.heightMap,
                            largeRegion.heightScale, largeRegion.uvScale, radius);
                    }

                    var midRegion = GetMidRegion(hmi, b);
                    if (midRegion?.heightMap != null)
                    {
                        midResults[b] = BakeOne(compute, kernel, midRegion.heightMap,
                            midRegion.heightScale, midRegion.uvScale, radius);
                    }
                }

                // Global gradience only exists under the Redux variant. The runtime samples
                // _GlobalGradienceTex only when REDUX_GRADIENCE is on, so baking it for
                // stock-mode planets would be wasted work.
                if (reduxGradience && hmi.globalHeightMap != null)
                {
                    globalResult = BakeOne(compute, kernel, hmi.globalHeightMap,
                        hmi.heightMapScale, uvScale: 1, radius);
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                DisposeArray(largeResults);
                DisposeArray(midResults);
                if (globalResult != null) UnityEngine.Object.DestroyImmediate(globalResult);
                return Skip($"{ex.GetType().Name}: {ex.Message}");
            }

            return new Result
            {
                LargePerBiome = largeResults,
                MidPerBiome = midResults,
                GlobalGradience = globalResult,
                ReduxEncoding = reduxGradience,
                Skipped = false,
                SkipReason = string.Empty,
            };
        }

        private static Result Skip(string reason) => new() { Skipped = true, SkipReason = reason };

        private static bool HasAnyRawHeightmap(PQSData.HeightMapInfo hmi)
        {
            if (hmi.largeR?.heightMap != null || hmi.largeG?.heightMap != null ||
                hmi.largeB?.heightMap != null || hmi.largeA?.heightMap != null) return true;
            if (hmi.mediumR?.heightMap != null || hmi.mediumG?.heightMap != null ||
                hmi.mediumB?.heightMap != null || hmi.mediumA?.heightMap != null) return true;
            return false;
        }

        private static PQSData.HeightRegion GetLargeRegion(PQSData.HeightMapInfo hmi, int b) => b switch
        {
            0 => hmi.largeR,
            1 => hmi.largeG,
            2 => hmi.largeB,
            _ => hmi.largeA,
        };

        private static PQSData.HeightRegion GetMidRegion(PQSData.HeightMapInfo hmi, int b) => b switch
        {
            0 => hmi.mediumR,
            1 => hmi.mediumG,
            2 => hmi.mediumB,
            _ => hmi.mediumA,
        };

        // Bakes one biome's raw heightmap into a half-res gradience Texture2D.
        private static Texture2D BakeOne(ComputeShader compute, int kernel,
            Texture2D raw, float heightScale, int uvScale, float radius)
        {
            int srcW = raw.width;
            int srcH = raw.height;
            int outW = Mathf.Max(1, srcW / 2);
            int outH = Mathf.Max(1, srcH / 2);
            // Source too small to produce a meaningful central-diff gradient. Skipping is
            // more honest than filling a fallback-sized output with zeros.
            if (outW < 2 || outH < 2)
            {
                Debug.LogWarning($"[GradienceBaker] Skipped '{raw.name}': source {srcW}x{srcH} too small for a half-res gradience bake (need at least 4x4).");
                return null;
            }

            compute.SetTexture(kernel, "_RawHeightmap", raw);
            compute.SetFloat("_HeightScaleMeters", heightScale);
            compute.SetFloat("_BodyRadius", radius);
            compute.SetInt("_SrcWidth", srcW);
            compute.SetInt("_SrcHeight", srcH);
            compute.SetInt("_OutWidth", outW);
            compute.SetInt("_OutHeight", outH);
            compute.SetInt("_UVScale", Mathf.Max(1, uvScale));

            var rt = AnalyticScaledSpaceSampler.CreateRwRT(outW, outH, RenderTextureFormat.ARGB32);
            try
            {
                compute.SetTexture(kernel, "_OutGradience", rt);
                int gx = (outW + 7) / 8;
                int gy = (outH + 7) / 8;
                compute.Dispatch(kernel, gx, gy, 1);
                return AnalyticScaledSpaceSampler.ReadbackToTexture(rt, TextureFormat.RGBA32, linear: true);
            }
            finally
            {
                AnalyticScaledSpaceSampler.ReleaseRT(rt);
            }
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
