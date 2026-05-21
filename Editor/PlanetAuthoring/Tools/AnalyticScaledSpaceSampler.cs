using System;
using System.IO;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// GPU compute baker that produces a body's scaled-space albedo, normal, packed, and emission textures by analytically sampling the local PQS shader's inputs.
    /// </summary>
    /// <remarks>
    /// Replaces the camera-driven <c>ScaledSpaceTextureGenerator</c>. The compute pass walks every output pixel of an equirectangular projection, supersamples N x N times, and per-subsample evaluates the macro normal (height gradient), macro AO (horizon-mapping the heightmap), biome mask, and the per-(biome, layer) trapezoid windows that drive the small-tile cascade. Per-tile pattern phase is intentionally dropped: it is sub-pixel at scaled distance and would only contribute noise.
    /// </remarks>
    public static class AnalyticScaledSpaceSampler
    {
        private const string ComputeShaderPath = "Assets/Modules/KSP2UnityTools/Assets/Shaders/PlanetAuthoring/AnalyticScaledSpaceBake.compute";

        // Must match SUBMEAN_GRID_SIZE in AnalyticScaledSpaceBake.compute. Submeans are now
        // sized to actual sliceCount via StructuredBuffer (no fixed MAX_SLICES cap).
        private const int SubmeanGridSize = 4;
        private const int SubmeansPerSlice = SubmeanGridSize * SubmeanGridSize;

        /// <summary>
        /// Per-bake settings for the analytic scaled-space sampler.
        /// </summary>
        public struct Settings
        {
            /// <summary>
            /// Output side length in pixels.
            /// </summary>
            /// <remarks>
            /// Outputs are always square so they can also be assigned back onto the PQS surface material's scaled-tex slots.
            /// </remarks>
            public int Resolution;
            /// <summary>
            /// N x N supersamples per output pixel.
            /// </summary>
            /// <remarks>
            /// 4 is a good default. Higher trades cost for cleaner biome and window transitions.
            /// </remarks>
            public int SubsampleN;
            /// <summary>
            /// Number of horizon rays for the macro AO march.
            /// </summary>
            /// <remarks>
            /// 8 is a good default.
            /// </remarks>
            public int AORingCount;
            /// <summary>
            /// Strength of the macro variation in the 0..1 range.
            /// </summary>
            /// <remarks>
            /// 0 always samples the central submean (uniform), 1 is full noise-driven travel across the 4x4 submean grid.
            /// </remarks>
            public float VariationStrength;
            /// <summary>
            /// Cycles per planet of the noise field that drives variation UV.
            /// </summary>
            /// <remarks>
            /// Lower values give larger continuous color patches.
            /// </remarks>
            public float VariationFrequency;
        }

        /// <summary>
        /// The four baked textures.
        /// </summary>
        /// <remarks>
        /// Caller owns disposal of every non-null entry.
        /// </remarks>
        public struct Result
        {
            /// <summary>
            /// Albedo texture, linear RGBA intended to import as sRGB.
            /// </summary>
            public Texture2D Albedo;
            /// <summary>
            /// DXT5nm-packed normal texture intended to import as a normal map.
            /// </summary>
            public Texture2D Normal;
            /// <summary>
            /// Packed PBR channels with R = metallic, G = macro AO, B = emission strength, A = smoothness.
            /// </summary>
            /// <remarks>
            /// Linear color space.
            /// </remarks>
            public Texture2D Packed;
            /// <summary>
            /// Emission color RGB intended to import as sRGB.
            /// </summary>
            public Texture2D Emission;
        }

        /// <summary>
        /// Returns sensible defaults for a 4096-square bake at four supersamples, eight AO rings, and moderate noise-driven variation.
        /// </summary>
        /// <returns>The default per-bake settings.</returns>
        public static Settings DefaultSettings() => new()
        {
            Resolution = 4096,
            SubsampleN = 4,
            AORingCount = 8,
            VariationStrength = 0.85f,
            VariationFrequency = 12f,
        };

        /// <summary>
        /// Runs the analytic bake against the given body and returns the four scaled-space textures.
        /// </summary>
        /// <param name="pqsData">The body's PQSData. Must have a globalHeightMap and a surfaceMaterial with the small-tile arrays bound.</param>
        /// <param name="radius">Body radius in meters.</param>
        /// <param name="settings">Per-bake settings. See <see cref="DefaultSettings" />.</param>
        /// <returns>The four baked textures wrapped in a <see cref="Result" />, owned by the caller.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pqsData" /> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="radius" /> or any <paramref name="settings" /> field is non-positive.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the PQSData is missing a required input (heightMapInfo, globalHeightMap, biome mask, surfaceMaterial, or any of the small-tile arrays).</exception>
        /// <exception cref="System.IO.FileNotFoundException">Thrown when the analytic-bake compute shader asset cannot be loaded.</exception>
        public static Result Sample(PQSData pqsData, float radius, Settings settings)
        {
            if (pqsData == null) throw new ArgumentNullException(nameof(pqsData));
            if (radius <= 0f) throw new ArgumentException("Radius must be positive.", nameof(radius));
            if (settings.Resolution <= 0) throw new ArgumentException("Resolution must be positive.", nameof(settings));
            if (settings.SubsampleN <= 0) throw new ArgumentException("SubsampleN must be positive.", nameof(settings));
            if (settings.AORingCount <= 0) throw new ArgumentException("AORingCount must be positive.", nameof(settings));

            var heightMapInfo = pqsData.heightMapInfo
                ?? throw new InvalidOperationException("PQSData has no heightMapInfo.");
            var globalHeightMap = heightMapInfo.globalHeightMap
                ?? throw new InvalidOperationException("PQSData has no globalHeightMap.");
            var biomeMask = heightMapInfo.mask
                ?? throw new InvalidOperationException("PQSData has no biome mask.");

            var surfaceMaterial = pqsData.materialSettings?.surfaceMaterial
                ?? throw new InvalidOperationException("PQSData has no surfaceMaterial.");

            var albedoArray = surfaceMaterial.GetTexture("_SmallAlbedoArray") as Texture2DArray
                ?? throw new InvalidOperationException("Surface material has no _SmallAlbedoArray.");
            var normalArray = surfaceMaterial.GetTexture("_SmallNormalArray") as Texture2DArray
                ?? throw new InvalidOperationException("Surface material has no _SmallNormalArray.");
            var metalArray = surfaceMaterial.GetTexture("_SmallMetalArray") as Texture2DArray
                ?? throw new InvalidOperationException("Surface material has no _SmallMetalArray.");

            // Per-biome large + mid raw heightmaps contribute to the composed altitude that
            // drives macro normal and AO. Pulled directly from PQSData (NOT from the surface
            // material's _LargeGradience*/_MidGradience* slots, which hold baked gradient
            // textures under Redux mode and would be dimensionally wrong as height inputs).
            // Missing slots fall back to flat-black so the bake runs on bodies that haven't
            // authored every biome's heightmap.
            var largeR = ResolveHeightmapOrFlat(heightMapInfo.largeR?.heightMap);
            var largeG = ResolveHeightmapOrFlat(heightMapInfo.largeG?.heightMap);
            var largeB = ResolveHeightmapOrFlat(heightMapInfo.largeB?.heightMap);
            var largeA = ResolveHeightmapOrFlat(heightMapInfo.largeA?.heightMap);
            var midR   = ResolveHeightmapOrFlat(heightMapInfo.mediumR?.heightMap);
            var midG   = ResolveHeightmapOrFlat(heightMapInfo.mediumG?.heightMap);
            var midB   = ResolveHeightmapOrFlat(heightMapInfo.mediumB?.heightMap);
            var midA   = ResolveHeightmapOrFlat(heightMapInfo.mediumA?.heightMap);

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputeShaderPath)
                ?? throw new FileNotFoundException($"Could not load compute shader at '{ComputeShaderPath}'.");

            // Outputs are square so they can also be assigned back onto the PQS surface material's
            // _AlbedoScaledTex / _NormalScaledTex / etc slots, which the local-view distance
            // crossfade samples directly.
            int side = settings.Resolution;

            // Precompute per-slice 4x4 submean grids via Graphics.Blit reduction. Each slice gets
            // chain-blitted down to a 4x4 RT; we read those 16 colors back as the submean grid.
            // Bound to the compute shader as a StructuredBuffer (no fixed slice cap).
            int sliceCount = albedoArray.depth;
            int submeanEntryCount = sliceCount * SubmeansPerSlice;
            var albedoSubmeans = new Vector4[submeanEntryCount];
            var normalSubmeans = new Vector4[submeanEntryCount];
            var metalSubmeans  = new Vector4[submeanEntryCount];
            ComputeSliceSubmeans(albedoArray, sliceCount, albedoSubmeans);
            ComputeSliceSubmeans(normalArray, sliceCount, normalSubmeans);
            ComputeSliceSubmeans(metalArray,  sliceCount, metalSubmeans);

            var albedoBuffer = new ComputeBuffer(submeanEntryCount, sizeof(float) * 4);
            var normalBuffer = new ComputeBuffer(submeanEntryCount, sizeof(float) * 4);
            var metalBuffer  = new ComputeBuffer(submeanEntryCount, sizeof(float) * 4);
            albedoBuffer.SetData(albedoSubmeans);
            normalBuffer.SetData(normalSubmeans);
            metalBuffer.SetData(metalSubmeans);

            var rtAlbedo = CreateRwRT(side, side, RenderTextureFormat.ARGB32);
            var rtNormal = CreateRwRT(side, side, RenderTextureFormat.ARGB32);
            var rtPacked = CreateRwRT(side, side, RenderTextureFormat.ARGB32);
            var rtEmission = CreateRwRT(side, side, RenderTextureFormat.ARGBHalf);

            try
            {
                int kernel = compute.FindKernel("CSMain");

                compute.SetTexture(kernel, "_OutAlbedo", rtAlbedo);
                compute.SetTexture(kernel, "_OutNormal", rtNormal);
                compute.SetTexture(kernel, "_OutPacked", rtPacked);
                compute.SetTexture(kernel, "_OutEmission", rtEmission);
                compute.SetTexture(kernel, "_BiomeMaskTex", biomeMask);
                compute.SetTexture(kernel, "_GlobalHeightMap", globalHeightMap);

                compute.SetTexture(kernel, "_LargeHeightR", largeR);
                compute.SetTexture(kernel, "_LargeHeightG", largeG);
                compute.SetTexture(kernel, "_LargeHeightB", largeB);
                compute.SetTexture(kernel, "_LargeHeightA", largeA);
                compute.SetTexture(kernel, "_MidHeightR",   midR);
                compute.SetTexture(kernel, "_MidHeightG",   midG);
                compute.SetTexture(kernel, "_MidHeightB",   midB);
                compute.SetTexture(kernel, "_MidHeightA",   midA);

                compute.SetVector("_LargeHeightMapUVScales",  surfaceMaterial.GetVector("_LargeHeightMapUVScales"));
                compute.SetVector("_MediumHeightMapUVScales", surfaceMaterial.GetVector("_MediumHeightMapUVScales"));
                // Per-region authored heightScales (matches the runtime PQS height stack).
                compute.SetVector("_LargeHeightScales", new Vector4(
                    heightMapInfo.largeR?.heightScale ?? 0f,
                    heightMapInfo.largeG?.heightScale ?? 0f,
                    heightMapInfo.largeB?.heightScale ?? 0f,
                    heightMapInfo.largeA?.heightScale ?? 0f));
                compute.SetVector("_MidHeightScales", new Vector4(
                    heightMapInfo.mediumR?.heightScale ?? 0f,
                    heightMapInfo.mediumG?.heightScale ?? 0f,
                    heightMapInfo.mediumB?.heightScale ?? 0f,
                    heightMapInfo.mediumA?.heightScale ?? 0f));

                compute.SetInt("_OutWidth", side);
                compute.SetInt("_OutHeight", side);
                compute.SetInt("_SubsampleN", settings.SubsampleN);
                compute.SetInt("_AORingCount", settings.AORingCount);
                compute.SetFloat("_Radius", radius);
                compute.SetFloat("_HeightScale", heightMapInfo.heightMapScale);
                compute.SetFloat("_VariationStrength", settings.VariationStrength);
                compute.SetFloat("_VariationFrequency", settings.VariationFrequency);

                compute.SetBuffer(kernel, "_SliceAlbedoSubmeans", albedoBuffer);
                compute.SetBuffer(kernel, "_SliceNormalSubmeans", normalBuffer);
                compute.SetBuffer(kernel, "_SliceMetalSubmeans",  metalBuffer);

                BindPerLayerArrays(compute, surfaceMaterial);

                int gx = (side + 7) / 8;
                int gy = (side + 7) / 8;
                compute.Dispatch(kernel, gx, gy, 1);

                return new Result
                {
                    Albedo = ReadbackToTexture(rtAlbedo, TextureFormat.RGBA32, linear: false),
                    Normal = ReadbackToTexture(rtNormal, TextureFormat.RGBA32, linear: true),
                    Packed = ReadbackToTexture(rtPacked, TextureFormat.RGBA32, linear: true),
                    Emission = ReadbackToTexture(rtEmission, TextureFormat.RGBA32, linear: false),
                };
            }
            finally
            {
                ReleaseRT(rtAlbedo);
                ReleaseRT(rtNormal);
                ReleaseRT(rtPacked);
                ReleaseRT(rtEmission);
                albedoBuffer.Release();
                normalBuffer.Release();
                metalBuffer.Release();
            }
        }

        // Reduces each slice of a Texture2DArray to a 4x4 grid of submeans. For each slice:
        // (1) extract the slice into a 2D RT via Graphics.Blit's array-slice overload;
        // (2) chain bilinear half-resolution blits down to 4x4;
        // (3) read back 16 colors into dst at offset (slice * SubmeansPerSlice).
        // Independent of source mip state - works on any Texture2DArray.
        private static void ComputeSliceSubmeans(Texture2DArray array, int sliceCount, Vector4[] dst)
        {
            int srcW = array.width;
            int srcH = array.height;
            int targetW = Mathf.Max(SubmeanGridSize, 1);
            int targetH = Mathf.Max(SubmeanGridSize, 1);

            for (int s = 0; s < sliceCount; s++)
            {
                var slice = RenderTexture.GetTemporary(srcW, srcH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                slice.filterMode = FilterMode.Bilinear;
                Graphics.Blit(array, slice, sourceDepthSlice: s, destDepthSlice: 0);

                var current = slice;
                int curW = srcW;
                int curH = srcH;
                while (curW > targetW || curH > targetH)
                {
                    int nextW = Mathf.Max(targetW, curW / 2);
                    int nextH = Mathf.Max(targetH, curH / 2);
                    var next = RenderTexture.GetTemporary(nextW, nextH, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                    next.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(current, next);
                    if (current != slice) RenderTexture.ReleaseTemporary(current);
                    current = next;
                    curW = nextW;
                    curH = nextH;
                }

                ReadSubmeanGrid(current, dst, s * SubmeansPerSlice);

                if (current != slice) RenderTexture.ReleaseTemporary(current);
                RenderTexture.ReleaseTemporary(slice);
            }
        }

        // Reads a 4x4 RT into 16 consecutive Vector4 entries of dst starting at baseIndex.
        // Layout: idx = row * 4 + col with (col, row) = (0, 0) at the RT's bottom-left
        // (Unity's ReadPixels convention).
        private static void ReadSubmeanGrid(RenderTexture rt, Vector4[] dst, int baseIndex)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBAFloat, mipChain: false, linear: true);
            try
            {
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                int w = Mathf.Min(rt.width, SubmeanGridSize);
                int h = Mathf.Min(rt.height, SubmeanGridSize);
                for (int row = 0; row < SubmeanGridSize; row++)
                {
                    int sampleY = Mathf.Min(row, h - 1);
                    for (int col = 0; col < SubmeanGridSize; col++)
                    {
                        int sampleX = Mathf.Min(col, w - 1);
                        var c = tex.GetPixel(sampleX, sampleY);
                        dst[baseIndex + row * SubmeanGridSize + col] = new Vector4(c.r, c.g, c.b, c.a);
                    }
                }
            }
            finally
            {
                RenderTexture.active = prev;
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// Polar-blends the top and bottom <paramref name="totalLines" /> rows of <paramref name="normal" /> against
        /// a constant pole-tangent normal so the equirectangular pole streaks become smooth.
        /// </summary>
        /// <remarks>
        /// Equirectangular projections collapse the +Y / -Y poles to single points along the top and bottom rows.
        /// Without polar blending, a UV sphere mesh sampling those rows produces a visible pinwheel where many
        /// vertices at the same world-space pole position pick up different texture columns.
        /// </remarks>
        /// <param name="normal">The DXT5nm-packed normal texture to blend in place. Null is a no-op.</param>
        /// <param name="totalLines">Number of rows from each pole to touch.</param>
        /// <param name="blendLines">Of the <paramref name="totalLines" /> rows, how many to soft-blend toward the captured row pixels. The remainder are hard-set to the pole color.</param>
        public static void BlendPolarNormals(Texture2D normal, int totalLines, int blendLines)
        {
            if (normal == null) return;
            int w = normal.width;
            int h = normal.height;

            // Constant "facing outward" pole-tangent normal in source-PNG convention
            // (R = encoded nx, G = encoded ny, B = encoded nz). The NormalMap importer
            // swizzles R into the DXT5nm output's alpha at import time.
            var poleColor = new Color(0.5f, 0.5f, 1f, 1f);

            for (int y = 0; y < totalLines; y++)
            {
                bool inFade = y > totalLines - blendLines - 1;
                float t = inFade ? (y - (totalLines - blendLines)) / (float)blendLines : 0f;
                for (int x = 0; x < w; x++)
                {
                    if (inFade)
                    {
                        var topPixel = normal.GetPixel(x, y);
                        var botPixel = normal.GetPixel(x, h - 1 - y);
                        normal.SetPixel(x, y,         Color.Lerp(poleColor, topPixel, t));
                        normal.SetPixel(x, h - 1 - y, Color.Lerp(poleColor, botPixel, t));
                    }
                    else
                    {
                        normal.SetPixel(x, y,         poleColor);
                        normal.SetPixel(x, h - 1 - y, poleColor);
                    }
                }
            }

            normal.Apply();
        }

        /// <summary>
        /// Polar-blends the top and bottom <paramref name="totalLines" /> rows of <paramref name="texture" /> toward each row's mean color so the equirectangular pole pinwheel collapses to a smooth disc.
        /// </summary>
        /// <param name="texture">The texture to blend in place. Null is a no-op.</param>
        /// <param name="totalLines">Number of rows from each pole to touch.</param>
        /// <param name="blendLines">Of the <paramref name="totalLines" /> rows, how many to soft-blend toward the per-row pixels. The remainder are hard-set to the row's mean color.</param>
        public static void BlendPolarRowsTowardRowMean(Texture2D texture, int totalLines, int blendLines)
        {
            if (texture == null) return;
            int w = texture.width;
            int h = texture.height;

            for (int y = 0; y < totalLines; y++)
            {
                Color topMean = RowMean(texture, y, w);
                Color botMean = RowMean(texture, h - 1 - y, w);
                bool inFade = y > totalLines - blendLines - 1;
                float t = inFade ? (y - (totalLines - blendLines)) / (float)blendLines : 0f;
                for (int x = 0; x < w; x++)
                {
                    if (inFade)
                    {
                        texture.SetPixel(x, y,         Color.Lerp(topMean, texture.GetPixel(x, y),         t));
                        texture.SetPixel(x, h - 1 - y, Color.Lerp(botMean, texture.GetPixel(x, h - 1 - y), t));
                    }
                    else
                    {
                        texture.SetPixel(x, y,         topMean);
                        texture.SetPixel(x, h - 1 - y, botMean);
                    }
                }
            }
            texture.Apply();
        }

        private static Color RowMean(Texture2D texture, int y, int w)
        {
            float r = 0f, g = 0f, b = 0f, a = 0f;
            for (int x = 0; x < w; x++)
            {
                var c = texture.GetPixel(x, y);
                r += c.r; g += c.g; b += c.b; a += c.a;
            }
            float inv = 1f / w;
            return new Color(r * inv, g * inv, b * inv, a * inv);
        }

        /// <summary>
        /// Averages the leftmost and rightmost columns of <paramref name="texture" /> and writes the average back to both, so the bilinear-filtered Repeat wrap at u=0/u=1 returns the same value as either side and the seam reads identical to any interior pixel transition.
        /// </summary>
        /// <param name="texture">The texture to make wrap-tileable in place. Null is a no-op.</param>
        public static void EnforceSeamTileability(Texture2D texture)
        {
            if (texture == null) return;
            int w = texture.width;
            int h = texture.height;
            int xR = w - 1;
            for (int y = 0; y < h; y++)
            {
                Color l = texture.GetPixel(0, y);
                Color r = texture.GetPixel(xR, y);
                Color avg = new(
                    (l.r + r.r) * 0.5f,
                    (l.g + r.g) * 0.5f,
                    (l.b + r.b) * 0.5f,
                    (l.a + r.a) * 0.5f);
                texture.SetPixel(0, y, avg);
                texture.SetPixel(xR, y, avg);
            }
            texture.Apply();
        }

        // 1x1 black texture used as a "no contribution" fallback for unassigned gradience slots.
        // The composed-height function reads the texture as `sample.x * heightScale * mask.c` -
        // a black sample contributes exactly zero regardless of the authored heightScale, so an
        // unbound gradience slot can't accidentally lift macro normals or AO.
        private static Texture2D _flatBlackFallback;

        private static Texture2D GetFlatBlackFallback()
        {
            if (_flatBlackFallback != null) return _flatBlackFallback;
            _flatBlackFallback = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false, linear: true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                name = "AnalyticScaledSpaceFlatBlackFallback",
            };
            _flatBlackFallback.SetPixel(0, 0, new Color(0f, 0f, 0f, 0f));
            _flatBlackFallback.Apply();
            return _flatBlackFallback;
        }

        private static Texture ResolveHeightmapOrFlat(Texture heightmap) => heightmap != null ? heightmap : GetFlatBlackFallback();

        /// <summary>
        /// Creates a linear random-write <see cref="RenderTexture" /> sized for a compute dispatch.
        /// </summary>
        /// <param name="w">Width in pixels.</param>
        /// <param name="h">Height in pixels.</param>
        /// <param name="format">Render texture format for the backing storage.</param>
        /// <returns>A created, random-write enabled <see cref="RenderTexture" /> in linear color space.</returns>
        /// <remarks>
        /// Caller is responsible for releasing the result via <see cref="ReleaseRT" />.
        /// </remarks>
        internal static RenderTexture CreateRwRT(int w, int h, RenderTextureFormat format)
        {
            var rt = new RenderTexture(w, h, 0, format, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true,
            };
            rt.Create();
            return rt;
        }

        /// <summary>
        /// Releases a <see cref="RenderTexture" /> created via <see cref="CreateRwRT" /> and destroys its native handle.
        /// </summary>
        /// <param name="rt">The render texture to release. Null is a no-op.</param>
        internal static void ReleaseRT(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// Reads back a <see cref="RenderTexture" /> into a CPU-side <see cref="Texture2D" /> of the requested format and color space.
        /// </summary>
        /// <param name="rt">The source render texture. Must be the active target during read.</param>
        /// <param name="format">Pixel format for the destination <see cref="Texture2D" />.</param>
        /// <param name="linear">True to mark the destination texture as linear, false to mark it as sRGB.</param>
        /// <returns>A newly allocated <see cref="Texture2D" /> holding the readback pixels, owned by the caller.</returns>
        internal static Texture2D ReadbackToTexture(RenderTexture rt, TextureFormat format, bool linear)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, format, mipChain: false, linear: linear);
            try
            {
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
            }
            finally
            {
                RenderTexture.active = prev;
            }
            return tex;
        }

        /// <summary>
        /// Binds the surface material's per-biome and per-layer shader properties onto the analytic-bake compute shader.
        /// </summary>
        /// <param name="compute">The compute shader to bind the per-layer arrays onto.</param>
        /// <param name="mat">The surface material whose per-biome <c>Vector4</c> properties supply the packed values.</param>
        /// <remarks>
        /// Scalar arrays (one float per (biome, layer) slot) are packed as <c>Vector4[4]</c>, one Vector4 per biome
        /// and four layers per Vector4, because <c>ComputeShader.SetFloats</c> only populates the first element of an
        /// HLSL <c>float arr[N]</c> cbuffer declaration. The packed layout matches the surface material's per-biome
        /// Vector4 shader properties exactly, so <c>mat.GetVector(...)</c> results pass through with no flattening.
        /// Float4-per-slot arrays (<c>HeightParams</c>, <c>SlopeParams</c>, <c>Tint</c>, <c>EmissionColor</c>) flatten to
        /// <c>Vector4[16]</c> via <c>SetVectorArray</c>, which works because each HLSL element already occupies a full
        /// vec4 slot.
        /// </remarks>
        internal static void BindPerLayerArrays(ComputeShader compute, Material mat)
        {
            var biomeIdxPack         = new Vector4[4];
            var enablePack           = new Vector4[4];
            var heightWeightPack     = new Vector4[4];
            var heightEnablePack     = new Vector4[4];
            var slopeEnablePack      = new Vector4[4];
            var brightnessPack       = new Vector4[4];
            var contrastPack         = new Vector4[4];
            var saturationPack       = new Vector4[4];
            var glossStrengthPack    = new Vector4[4];
            var metallicStrengthPack = new Vector4[4];
            var aoStrengthPack       = new Vector4[4];
            var emissionStrengthPack = new Vector4[4];

            var heightParams  = new Vector4[16];
            var slopeParams   = new Vector4[16];
            var tint          = new Vector4[16];
            var emissionColor = new Vector4[16];

            for (int b = 0; b < 4; b++)
            {
                var c = PlanetAuthoringNaming.BiomeChannels[b];

                biomeIdxPack[b]         = mat.GetVector($"_SmallBiome{c}");
                enablePack[b]           = mat.GetVector($"_SmallEnable{c}");
                heightWeightPack[b]     = mat.GetVector($"_SmallHeightWeight{c}");
                heightEnablePack[b]     = mat.GetVector($"_SmallBiomeHeightEnable{c}");
                slopeEnablePack[b]      = mat.GetVector($"_SmallBiomeSlopeEnable{c}");
                brightnessPack[b]       = mat.GetVector($"_SmallBrightness{c}");
                contrastPack[b]         = mat.GetVector($"_SmallContrast{c}");
                saturationPack[b]       = mat.GetVector($"_SmallSaturation{c}");
                glossStrengthPack[b]    = mat.GetVector($"_SmallGlossStrength{c}");
                metallicStrengthPack[b] = mat.GetVector($"_SmallMetallicStrength{c}");
                aoStrengthPack[b]       = mat.GetVector($"_SmallAOStrength{c}");
                emissionStrengthPack[b] = mat.GetVector($"_SmallEmissionStrength{c}");

                for (int l = 0; l < 4; l++)
                {
                    int idx = b * 4 + l;
                    heightParams[idx]  = mat.GetVector($"_SmallBiome{c}HeightParams{l + 1}");
                    slopeParams[idx]   = mat.GetVector($"_SmallBiome{c}SlopeParams{l + 1}");
                    tint[idx]          = mat.GetColor($"_SmallTint{c}{l + 1}");
                    emissionColor[idx] = mat.GetColor($"_SmallEmissionColor{c}{l + 1}");
                }
            }

            compute.SetVectorArray("_SmallBiomeIdxPack",         biomeIdxPack);
            compute.SetVectorArray("_SmallEnablePack",           enablePack);
            compute.SetVectorArray("_SmallHeightWeightPack",     heightWeightPack);
            compute.SetVectorArray("_SmallHeightEnablePack",     heightEnablePack);
            compute.SetVectorArray("_SmallSlopeEnablePack",      slopeEnablePack);
            compute.SetVectorArray("_SmallBrightnessPack",       brightnessPack);
            compute.SetVectorArray("_SmallContrastPack",         contrastPack);
            compute.SetVectorArray("_SmallSaturationPack",       saturationPack);
            compute.SetVectorArray("_SmallGlossStrengthPack",    glossStrengthPack);
            compute.SetVectorArray("_SmallMetallicStrengthPack", metallicStrengthPack);
            compute.SetVectorArray("_SmallAOStrengthPack",       aoStrengthPack);
            compute.SetVectorArray("_SmallEmissionStrengthPack", emissionStrengthPack);

            compute.SetVectorArray("_SmallHeightParams",  heightParams);
            compute.SetVectorArray("_SmallSlopeParams",   slopeParams);
            compute.SetVectorArray("_SmallTint",          tint);
            compute.SetVectorArray("_SmallEmissionColor", emissionColor);
        }
    }
}
