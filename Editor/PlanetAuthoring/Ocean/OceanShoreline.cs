using System.IO;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// How a body's shoreline mask stands against the terrain and sea level it was made from.
    /// </summary>
    public enum ShorelineState
    {
        /// <summary>The ocean has no mask.</summary>
        Missing,

        /// <summary>The terrain or sea level changed since the mask was made.</summary>
        Stale,

        /// <summary>The mask matches the terrain and sea level.</summary>
        Current,
    }

    /// <summary>
    /// Makes the shoreline mask an ocean calms its waves and fades its animation near the coast with.
    /// </summary>
    /// <remarks>
    /// Stock calls it a height SDF, but its masks are equirectangular sea masks: 1 over sea, 0 over land, with an edge a
    /// texel or two wide. This one is built from the global heightmap against sea level, the way the scaled albedo's
    /// ocean is painted. The material's sidecar records a fingerprint of those inputs, so a mask left behind by a terrain
    /// or sea level change is reported as stale.
    /// </remarks>
    public static class OceanShoreline
    {
        /// <summary>
        /// The material property the ocean reads the mask from.
        /// </summary>
        public const string PROPERTY = "_ShorelineSDFTexture";

        private const int MAX_RESOLUTION = 4096;

        /// <summary>
        /// Builds a shoreline mask from a global heightmap: 1 over sea, 0 over land, with an anti-aliased coast.
        /// </summary>
        /// <remarks>
        /// Each texel averages four samples of the bilinearly filtered heightmap, so the coast falls between texels
        /// smoothly. Texel corners map to the heightmap the way the scaled albedo's ocean composite maps them.
        /// </remarks>
        /// <param name="heights">The heightmap's texels, rows running up from the bottom, height in the red channel.</param>
        /// <param name="heightWidth">The heightmap's width.</param>
        /// <param name="heightHeight">The heightmap's height.</param>
        /// <param name="seaLevel">Sea level as a normalized heightmap value.</param>
        /// <param name="width">The mask's width.</param>
        /// <param name="height">The mask's height.</param>
        /// <returns>The mask's values, rows running up from the bottom.</returns>
        public static float[] BuildMask(Color[] heights, int heightWidth, int heightHeight, float seaLevel, int width, int height)
        {
            var mask = new float[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sea = 0;
                    for (int sy = 0; sy < 2; sy++)
                    {
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float u = (x - 0.25f + 0.5f * sx) / (width - 1);
                            float v = (y - 0.25f + 0.5f * sy) / (height - 1);
                            if (SampleBilinear(heights, heightWidth, heightHeight, u, v) <= seaLevel)
                            {
                                sea++;
                            }
                        }
                    }

                    mask[y * width + x] = sea / 4f;
                }
            }

            return mask;
        }

        /// <summary>
        /// Hashes what a body's shoreline mask is made from: its global heightmap, height scale and sea level.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The fingerprint, or an empty string when the body has no heightmap to make a mask from.</returns>
        public static string ComputeFingerprint(CoreCelestialBodyData body)
        {
            PQSData data = FindPqsData(body);
            var info = data != null ? data.heightMapInfo : null;
            if (info == null || info.globalHeightMap == null || body.Data == null)
                return string.Empty;

            return ComputeFingerprint(info.globalHeightMap, info.heightMapScale, body.Data.oceanAltitude);
        }

        /// <summary>
        /// Hashes what a shoreline mask is made from.
        /// </summary>
        /// <param name="heightMap">The global heightmap. Its asset and import timestamp go into the hash.</param>
        /// <param name="heightScale">The heightmap's scale, in meters.</param>
        /// <param name="oceanAltitude">The body's ocean altitude, in meters.</param>
        /// <returns>The fingerprint.</returns>
        public static string ComputeFingerprint(Texture2D heightMap, float heightScale, double oceanAltitude)
        {
            var hash = new Hash128();
            BodySurfaceBakerOperation.AppendTextureHash(ref hash, heightMap);
            hash.Append(heightScale);
            hash.Append(oceanAltitude.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return hash.ToString();
        }

        /// <summary>
        /// Gets how a body's ocean material's shoreline mask stands against the terrain and sea level.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="material">The ocean material.</param>
        /// <returns>The mask's state.</returns>
        public static ShorelineState GetState(CoreCelestialBodyData body, Material material)
        {
            if (material == null || !material.HasProperty(PROPERTY) || material.GetTexture(PROPERTY) == null)
                return ShorelineState.Missing;

            OceanMaterialAuthoring sidecar = AuthoringSidecars.FindOcean(material);
            return sidecar != null && sidecar.ShorelineFingerprint == ComputeFingerprint(body)
                ? ShorelineState.Current
                : ShorelineState.Stale;
        }

        /// <summary>
        /// Makes a body's shoreline mask, saves it beside the ocean material and assigns it.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <param name="material">The ocean material, a project asset.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the mask was made and assigned, false otherwise.</returns>
        public static bool TryBake(CoreCelestialBodyData body, Material material, out string message)
        {
            string materialPath = material != null ? AssetDatabase.GetAssetPath(material) : null;
            if (string.IsNullOrEmpty(materialPath))
            {
                message = "Save the ocean material as an asset first.";
                return false;
            }

            PQSData data = FindPqsData(body);
            var info = data != null ? data.heightMapInfo : null;
            Texture2D heightMap = info != null ? info.globalHeightMap : null;
            if (heightMap == null || body.Data == null || info.heightMapScale <= 0f)
            {
                message = "The body has no global heightmap to make a shoreline from.";
                return false;
            }

            if (!heightMap.isReadable)
            {
                message = $"{heightMap.name} is not Read/Write enabled, so the shoreline cannot be read from it.";
                return false;
            }

            int width = Mathf.Min(heightMap.width, MAX_RESOLUTION);
            int height = Mathf.Min(heightMap.height, MAX_RESOLUTION);
            float seaLevel = (float)(body.Data.oceanAltitude / info.heightMapScale);
            float[] mask = BuildMask(heightMap.GetPixels(), heightMap.width, heightMap.height, seaLevel, width, height);

            string path = $"{Path.GetDirectoryName(materialPath)?.Replace('\\', '/')}/{material.name}_Shoreline.png";
            Texture2D saved = Save(mask, width, height, path);

            Undo.RecordObject(material, "Generate Ocean Shoreline");
            material.SetTexture(PROPERTY, saved);
            EditorUtility.SetDirty(material);
            OceanMaterialAuthoring sidecar = AuthoringSidecars.GetOrCreateOcean(material);
            sidecar.ShorelineFingerprint = ComputeFingerprint(body);
            EditorUtility.SetDirty(sidecar);
            AssetDatabase.SaveAssets();
            message = $"Generated {Path.GetFileName(path)} at {width} by {height}.";
            return true;
        }

        private static PQSData FindPqsData(CoreCelestialBodyData body)
        {
            PQS pqs = body != null ? BodyResolver.FindPqsIncludingAsset(body) : null;
            return pqs != null ? pqs.data : null;
        }

        // Wraps across the date line and clamps at the poles, as an equirectangular map does.
        private static float SampleBilinear(Color[] pixels, int width, int height, float u, float v)
        {
            u -= Mathf.Floor(u);
            v = Mathf.Clamp01(v);
            float fx = u * (width - 1);
            float fy = v * (height - 1);
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = (x0 + 1) % width;
            int y1 = Mathf.Min(y0 + 1, height - 1);
            float tx = fx - x0;
            float ty = fy - y0;
            float bottom = Mathf.Lerp(pixels[y0 * width + x0].r, pixels[y0 * width + x1].r, tx);
            float top = Mathf.Lerp(pixels[y1 * width + x0].r, pixels[y1 * width + x1].r, tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        // Single channel, as stock's is, and linear, since it is a mask rather than a colour.
        private static Texture2D Save(float[] mask, int width, int height, string path)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                var pixels = new Color32[mask.Length];
                for (int i = 0; i < mask.Length; i++)
                {
                    byte value = (byte)Mathf.RoundToInt(mask[i] * 255f);
                    pixels[i] = new Color32(value, value, value, 255);
                }

                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = MAX_RESOLUTION;
            TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = TextureImporterFormat.BC4;
            importer.SetPlatformTextureSettings(platform);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
