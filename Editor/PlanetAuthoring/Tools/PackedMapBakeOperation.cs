using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Which channel of a source texture carries a single-channel map.
    /// </summary>
    public enum PackedMapChannel
    {
        /// <summary>Red.</summary>
        R,
        /// <summary>Green.</summary>
        G,
        /// <summary>Blue.</summary>
        B,
        /// <summary>Alpha.</summary>
        A,
    }

    /// <summary>
    /// How a source normal map lays its x and y components out in the channels the GPU samples.
    /// </summary>
    public enum PackedMapNormalEncoding
    {
        /// <summary>Infer the encoding from the source's importer settings and graphics format.</summary>
        Auto,
        /// <summary>Plain RGB, with x in red and y in green.</summary>
        Rgb,
        /// <summary>Unity's DXT5nm, with x in alpha and y in green.</summary>
        Dxt5nm,
    }

    /// <summary>
    /// Packs separate normal, smoothness and AO maps into the RGBA layout the small-layer and decal tile arrays sample.
    /// </summary>
    /// <remarks>
    /// The pack runs on the GPU, which is what lets the sources stay block-compressed and
    /// non-readable: the compute pass samples them through a linear-repeat sampler, so mismatched
    /// source sizes resample on the way in and nothing needs Read/Write ticked.
    ///
    /// Output channel order is <c>(smoothness, normalY, AO, normalX)</c>, matching what
    /// <c>DeferredBiome.cginc</c> reads out of <c>_SmallNormalArray</c>.
    /// </remarks>
    public static class PackedMapBakeOperation
    {
        private const string COMPUTE_SHADER_PATH =
            "Assets/Modules/KSP2UnityTools/Assets/Shaders/PlanetAuthoring/PackedMapBake.compute";

        private const string KERNEL_NAME = "CSPack";

        // Must match [numthreads] in PackedMapBake.compute.
        private const int THREAD_GROUP_SIZE = 8;

        /// <summary>
        /// One tile's bake inputs.
        /// </summary>
        /// <remarks>
        /// Only <see cref="Normal" /> is required. A missing smoothness or AO source falls back to
        /// its constant across the whole tile, which is how a flat-AO or fixed-gloss tile is baked
        /// without authoring a solid-color PNG first.
        /// </remarks>
        public sealed class Request
        {
            /// <summary>
            /// Gets the tangent-space normal source.
            /// </summary>
            public Texture2D Normal { get; set; }

            /// <summary>
            /// Gets how the normal source lays out its x and y components.
            /// </summary>
            public PackedMapNormalEncoding NormalEncoding { get; set; } = PackedMapNormalEncoding.Auto;

            /// <summary>
            /// Gets whether the normal's green channel is inverted before packing, for sources authored in the OpenGL convention.
            /// </summary>
            public bool FlipGreen { get; set; }

            /// <summary>
            /// Gets the smoothness or roughness source, or null to use <see cref="SmoothnessConstant" />.
            /// </summary>
            public Texture2D Smoothness { get; set; }

            /// <summary>
            /// Gets the channel of <see cref="Smoothness" /> carrying the value.
            /// </summary>
            public PackedMapChannel SmoothnessChannel { get; set; } = PackedMapChannel.R;

            /// <summary>
            /// Gets whether the smoothness source is a roughness map and is inverted on the way in.
            /// </summary>
            public bool InvertSmoothness { get; set; }

            /// <summary>
            /// Gets the smoothness used where no source is supplied.
            /// </summary>
            public float SmoothnessConstant { get; set; } = 0.5f;

            /// <summary>
            /// Gets the ambient-occlusion source, or null to use <see cref="AmbientOcclusionConstant" />.
            /// </summary>
            public Texture2D AmbientOcclusion { get; set; }

            /// <summary>
            /// Gets the channel of <see cref="AmbientOcclusion" /> carrying the value.
            /// </summary>
            public PackedMapChannel AmbientOcclusionChannel { get; set; } = PackedMapChannel.R;

            /// <summary>
            /// Gets the ambient occlusion used where no source is supplied.
            /// </summary>
            public float AmbientOcclusionConstant { get; set; } = 1f;

            /// <summary>
            /// Gets the output side length in pixels, or 0 to follow the normal source's width.
            /// </summary>
            public int Resolution { get; set; }
        }

        /// <summary>
        /// Returns the dot mask that selects <paramref name="channel" /> out of a sampled RGBA value.
        /// </summary>
        /// <param name="channel">The channel to select.</param>
        /// <returns>A unit vector with 1 in the selected channel and 0 elsewhere.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channel" /> is not a declared value.</exception>
        public static Vector4 ChannelMask(PackedMapChannel channel) => channel switch
        {
            PackedMapChannel.R => new Vector4(1f, 0f, 0f, 0f),
            PackedMapChannel.G => new Vector4(0f, 1f, 0f, 0f),
            PackedMapChannel.B => new Vector4(0f, 0f, 1f, 0f),
            PackedMapChannel.A => new Vector4(0f, 0f, 0f, 1f),
            _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, "Not a packed-map source channel."),
        };

        /// <summary>
        /// Infers how a normal source lays out its x and y components from its importer settings and graphics format.
        /// </summary>
        /// <remarks>
        /// Only a source imported as a normal map is swizzled by Unity, and only into DXT5nm. BC5
        /// keeps x in red, so it reads back like a plain RGB source despite being a normal-map
        /// import. Anything else is taken at face value.
        /// </remarks>
        /// <param name="normal">The normal source to inspect.</param>
        /// <returns>The inferred encoding, never <see cref="PackedMapNormalEncoding.Auto" />.</returns>
        public static PackedMapNormalEncoding DetectNormalEncoding(Texture2D normal)
        {
            if (normal == null)
            {
                return PackedMapNormalEncoding.Rgb;
            }

            string path = AssetDatabase.GetAssetPath(normal);
            if (string.IsNullOrEmpty(path) || AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                return PackedMapNormalEncoding.Rgb;
            }
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                return PackedMapNormalEncoding.Rgb;
            }

            var format = normal.graphicsFormat;
            bool isTwoChannel = format is GraphicsFormat.RG_BC5_UNorm or GraphicsFormat.RG_BC5_SNorm or GraphicsFormat.R8G8_UNorm;
            return isTwoChannel ? PackedMapNormalEncoding.Rgb : PackedMapNormalEncoding.Dxt5nm;
        }

        /// <summary>
        /// Runs the pack and returns the result as a CPU-side texture the caller owns.
        /// </summary>
        /// <param name="request">The tile's bake inputs.</param>
        /// <returns>An RGBA32 linear <see cref="Texture2D" /> holding the packed tile. The caller destroys it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request" /> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the request has no normal source.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the pack compute shader asset cannot be loaded.</exception>
        public static Texture2D Bake(Request request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (request.Normal == null)
            {
                throw new ArgumentException("A packed-map bake needs a normal source.", nameof(request));
            }

            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(COMPUTE_SHADER_PATH);
            if (compute == null)
            {
                throw new FileNotFoundException($"Could not load compute shader at '{COMPUTE_SHADER_PATH}'.");
            }

            int side = request.Resolution > 0 ? request.Resolution : request.Normal.width;
            PackedMapNormalEncoding encoding = request.NormalEncoding == PackedMapNormalEncoding.Auto
                ? DetectNormalEncoding(request.Normal)
                : request.NormalEncoding;

            RenderTexture target = AnalyticScaledSpaceSampler.CreateRwRT(side, side, RenderTextureFormat.ARGB32);
            try
            {
                int kernel = compute.FindKernel(KERNEL_NAME);

                compute.SetTexture(kernel, "_OutPacked", target);
                compute.SetTexture(kernel, "_NormalTex", request.Normal);
                BindOptional(compute, kernel, "_SmoothnessTex", request.Smoothness);
                BindOptional(compute, kernel, "_AoTex", request.AmbientOcclusion);

                compute.SetInts("_OutSize", side, side);

                compute.SetInt("_HasNormal", 1);
                compute.SetInt("_NormalIsDxt5nm", encoding == PackedMapNormalEncoding.Dxt5nm ? 1 : 0);
                compute.SetInt("_FlipGreen", request.FlipGreen ? 1 : 0);

                compute.SetInt("_HasSmoothness", request.Smoothness != null ? 1 : 0);
                compute.SetVector("_SmoothnessMask", ChannelMask(request.SmoothnessChannel));
                compute.SetFloat("_SmoothnessConst", Mathf.Clamp01(request.SmoothnessConstant));
                compute.SetInt("_InvertSmoothness", request.InvertSmoothness ? 1 : 0);

                compute.SetInt("_HasAo", request.AmbientOcclusion != null ? 1 : 0);
                compute.SetVector("_AoMask", ChannelMask(request.AmbientOcclusionChannel));
                compute.SetFloat("_AoConst", Mathf.Clamp01(request.AmbientOcclusionConstant));

                int groups = (side + THREAD_GROUP_SIZE - 1) / THREAD_GROUP_SIZE;
                compute.Dispatch(kernel, groups, groups, 1);

                return AnalyticScaledSpaceSampler.ReadbackToTexture(target, TextureFormat.RGBA32, linear: true);
            }
            finally
            {
                AnalyticScaledSpaceSampler.ReleaseRT(target);
            }
        }

        /// <summary>
        /// Writes a baked tile to a PNG at <paramref name="projectPath" /> and applies the packed-tile import settings.
        /// </summary>
        /// <remarks>
        /// The import settings are fixed rather than exposed because
        /// <c>Texture2DArrayPacker.RepackSmallTiles</c> rejects the whole 16-slice pack when any two
        /// tiles disagree on size, graphics format or mip count. Every tile this baker produces at a
        /// given resolution therefore packs together.
        /// </remarks>
        /// <param name="baked">The packed tile returned by <see cref="Bake" />.</param>
        /// <param name="projectPath">Project-relative path of the PNG to write, including the extension.</param>
        /// <returns>The imported texture asset, or null when Unity returned no importer for the written file.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="baked" /> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="projectPath" /> is null or empty.</exception>
        public static Texture2D WriteAndImport(Texture2D baked, string projectPath)
        {
            if (baked == null)
            {
                throw new ArgumentNullException(nameof(baked));
            }
            if (string.IsNullOrEmpty(projectPath))
            {
                throw new ArgumentException("A packed-map write needs an output path.", nameof(projectPath));
            }

            string directory = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(projectPath, baked.EncodeToPNG());
            AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(projectPath) is not TextureImporter importer)
            {
                return null;
            }

            ConfigurePackedImporter(importer, baked.width);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(projectPath);
        }

        // Matches the import settings on the packed tiles already shipping under
        // Assets/ReduxAssets/Definitions/CelestialBodies: linear, mipped, BC7. Alpha carries
        // normal x, so alphaIsTransparency stays off and the alpha source stays the input.
        private static void ConfigurePackedImporter(TextureImporter importer, int resolution)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = Mathf.Max(resolution, 32);
        }

        // ComputeShader.SetTexture rejects a null binding, so an absent optional source gets a 1x1
        // stand-in. Its samples are multiplied out by the matching _Has flag in the kernel.
        private static void BindOptional(ComputeShader compute, int kernel, string name, Texture2D texture)
        {
            compute.SetTexture(kernel, name, texture != null ? texture : Texture2D.whiteTexture);
        }
    }
}
