using System.Collections.Generic;
using System.IO;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// The textures of an ocean material the SDK generates.
    /// </summary>
    public enum OceanTextureKind
    {
        /// <summary>Light focused onto the seabed.</summary>
        Caustics,

        /// <summary>The foam shape seen up close.</summary>
        Foam,

        /// <summary>The foam shape seen from further away.</summary>
        FarFoam,

        /// <summary>Small ripples.</summary>
        DetailNormal,

        /// <summary>Broad swells, used for both large normal layers.</summary>
        LargeNormal,
    }

    /// <summary>
    /// Generates an ocean material's caustics, foam shapes and normal maps on the GPU and saves them beside it.
    /// </summary>
    /// <remarks>
    /// Stock's ocean textures live in the game's bundles, where a project material cannot reference them, so a body
    /// gets its own. Each is procedural noise that repeats across the texture, so it tiles without a seam. They are saved
    /// as PNG with stock's import settings, linear and repeating, so they can also be touched up in an image editor.
    /// </remarks>
    public static class OceanTextureBaker
    {
        private const string COMPUTE_SHADER_PATH = "/Assets/Shaders/PlanetAuthoring/Ocean/OceanTextures.compute";
        private const int GROUP_SIZE = 8;

        // Unity started with -nographics, as CI does, runs on the null device. It still reports compute shader support,
        // but compiles no kernels, so finding one throws.
        private static bool CanDispatch => SystemInfo.supportsComputeShaders && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;

        /// <summary>
        /// Gets a value indicating whether this machine can run the bake.
        /// </summary>
        public static bool IsSupported => CanDispatch && LoadComputeShader() != null;

        /// <summary>
        /// Generates a caustics texture.
        /// </summary>
        /// <param name="settings">The texture's settings.</param>
        /// <returns>The texels, rows running up from the bottom, or null when the bake cannot run.</returns>
        public static Color[] GenerateCaustics(OceanCausticsSettings settings)
        {
            return Run("BakeCaustics", settings.Resolution, shader =>
            {
                shader.SetInt("_Seed", settings.Seed);
                shader.SetInts("_Cells", settings.Cells, settings.Cells);
                shader.SetFloat("_Warp", settings.Warp);
                shader.SetFloat("_Sharpness", settings.Sharpness);
                shader.SetFloat("_Fringe", settings.Fringe);
            });
        }

        /// <summary>
        /// Generates a foam shape texture.
        /// </summary>
        /// <remarks>
        /// The pattern is centred on the requested coverage around its own average, so coverage means the same however
        /// the pattern is shaped.
        /// </remarks>
        /// <param name="settings">The texture's settings.</param>
        /// <returns>The texels, rows running up from the bottom, or null when the bake cannot run.</returns>
        public static Color[] GenerateFoam(OceanFoamSettings settings)
        {
            Color[] texels = Run("BakeFoam", settings.Resolution, shader =>
            {
                shader.SetInt("_Seed", settings.Seed);
                shader.SetInts("_Cells", settings.Cells, settings.Cells);
                shader.SetInt("_Octaves", Mathf.Clamp(settings.Octaves, 1, 8));
                shader.SetFloat("_Roughness", settings.Roughness);
                shader.SetFloat("_Warp", settings.Warp);
            });
            if (texels == null)
                return null;

            double sum = 0.0;
            foreach (Color texel in texels)
            {
                sum += texel.r;
            }

            float average = (float)(sum / texels.Length);
            for (int i = 0; i < texels.Length; i++)
            {
                float value = Mathf.Clamp01(settings.Coverage + (texels[i].r - average) * settings.Contrast);
                texels[i] = new Color(value, value, value, 1f);
            }

            return texels;
        }

        /// <summary>
        /// Generates the unit normals of a normal map.
        /// </summary>
        /// <remarks>
        /// The noise's slopes are scaled so the normals' sideways components spread by the requested bumpiness, which
        /// keeps the strength independent of the pattern's scale and octaves.
        /// </remarks>
        /// <param name="settings">The map's settings.</param>
        /// <returns>The normals, rows running up from the bottom, or null when the bake cannot run.</returns>
        public static Vector3[] GenerateNormals(OceanNormalSettings settings)
        {
            Color[] slopes = Run("BakeSlopes", settings.Resolution, shader =>
            {
                shader.SetInt("_Seed", settings.Seed);
                shader.SetInts("_Cells", Mathf.Max(1, settings.CellsX), Mathf.Max(1, settings.CellsY));
                shader.SetInt("_Octaves", Mathf.Clamp(settings.Octaves, 1, 8));
                shader.SetFloat("_Roughness", settings.Roughness);
                shader.SetFloat("_Warp", settings.Warp);
            });
            if (slopes == null)
                return null;

            double sumSquares = 0.0;
            foreach (Color slope in slopes)
            {
                sumSquares += slope.r * slope.r + slope.g * slope.g;
            }

            float spread = Mathf.Sqrt((float)(sumSquares / (2.0 * slopes.Length)));
            float scale = spread > 1e-6f ? settings.Bumpiness / spread : 0f;
            var normals = new Vector3[slopes.Length];
            for (int i = 0; i < slopes.Length; i++)
            {
                normals[i] = new Vector3(-slopes[i].r * scale, -slopes[i].g * scale, 1f).normalized;
            }

            return normals;
        }

        /// <summary>
        /// Generates one of an ocean material's textures from the settings in its sidecar, saves it beside the material
        /// and assigns it.
        /// </summary>
        /// <param name="material">The ocean material, a project asset.</param>
        /// <param name="kind">Which texture to generate.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the texture was generated and assigned, false otherwise.</returns>
        public static bool TryBake(Material material, OceanTextureKind kind, out string message)
        {
            string materialPath = material != null ? AssetDatabase.GetAssetPath(material) : null;
            if (string.IsNullOrEmpty(materialPath))
            {
                message = "Save the ocean material as an asset first.";
                return false;
            }

            if (!IsSupported)
            {
                message = "This machine cannot run the ocean texture bake's compute shader.";
                return false;
            }

            OceanMaterialAuthoring sidecar = AuthoringSidecars.GetOrCreateOcean(material);
            int resolution;
            Color32[] pixels;
            bool isNormal = kind is OceanTextureKind.DetailNormal or OceanTextureKind.LargeNormal;
            if (isNormal)
            {
                OceanNormalSettings settings = kind == OceanTextureKind.DetailNormal ? sidecar.DetailNormal : sidecar.LargeNormal;
                resolution = ClampResolution(settings.Resolution);
                settings.Resolution = resolution;
                pixels = EncodeNormals(GenerateNormals(settings));
            }
            else
            {
                Color[] colors;
                if (kind == OceanTextureKind.Caustics)
                {
                    sidecar.Caustics.Resolution = resolution = ClampResolution(sidecar.Caustics.Resolution);
                    colors = GenerateCaustics(sidecar.Caustics);
                }
                else
                {
                    OceanFoamSettings settings = kind == OceanTextureKind.Foam ? sidecar.Foam : sidecar.FarFoam;
                    settings.Resolution = resolution = ClampResolution(settings.Resolution);
                    colors = GenerateFoam(settings);
                }

                pixels = Encode(colors);
            }

            EditorUtility.SetDirty(sidecar);
            string folder = Path.GetDirectoryName(materialPath)?.Replace('\\', '/');
            string path = $"{folder}/{material.name}_{kind}.png";
            Texture2D saved = Save(pixels, resolution, path, isNormal);

            Undo.RecordObject(material, "Generate Ocean Texture");
            foreach (string property in PropertiesOf(kind))
            {
                material.SetTexture(property, saved);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            message = $"Generated {Path.GetFileName(path)} at {resolution} by {resolution}.";
            return true;
        }

        /// <summary>
        /// Generates every texture of an ocean material.
        /// </summary>
        /// <param name="material">The ocean material, a project asset.</param>
        /// <param name="message">A status line describing the outcome or the first failure.</param>
        /// <returns>True if every texture was generated, false otherwise.</returns>
        public static bool TryBakeAll(Material material, out string message)
        {
            var made = new List<string>();
            foreach (OceanTextureKind kind in new[]
                     {
                         OceanTextureKind.Caustics, OceanTextureKind.Foam, OceanTextureKind.FarFoam,
                         OceanTextureKind.DetailNormal, OceanTextureKind.LargeNormal,
                     })
            {
                if (!TryBake(material, kind, out message))
                    return false;

                made.Add(kind.ToString());
            }

            message = $"Generated {string.Join(", ", made)}.";
            return true;
        }

        /// <summary>
        /// Gets the material properties a generated texture is assigned to.
        /// </summary>
        /// <param name="kind">The texture.</param>
        /// <returns>The property names.</returns>
        public static string[] PropertiesOf(OceanTextureKind kind) => kind switch
        {
            OceanTextureKind.Caustics => new[] { "_CausticsTexture" },
            OceanTextureKind.Foam => new[] { "_FoamShapeTexture" },
            OceanTextureKind.FarFoam => new[] { "_FarFoamShapeTexture" },
            OceanTextureKind.DetailNormal => new[] { "_DetailNormalMap" },
            _ => new[] { "_LargeNormalMap_1", "_LargeNormalMap_2" },
        };

        private static Color[] Run(string kernelName, int resolution, System.Action<ComputeShader> bind)
        {
            ComputeShader shader = LoadComputeShader();
            if (shader == null || !CanDispatch)
                return null;

            resolution = ClampResolution(resolution);
            int kernel = shader.FindKernel(kernelName);
            var texels = new Color[resolution * resolution];
            using var result = new ComputeBuffer(texels.Length, sizeof(float) * 4);
            shader.SetInt("_Resolution", resolution);
            shader.SetBuffer(kernel, "_Result", result);
            bind(shader);
            int groups = Mathf.CeilToInt(resolution / (float)GROUP_SIZE);
            shader.Dispatch(kernel, groups, groups, 1);
            result.GetData(texels);
            return texels;
        }

        private static int ClampResolution(int resolution) => Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 16, 4096));

        private static Color32[] Encode(Color[] colors)
        {
            var pixels = new Color32[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                pixels[i] = colors[i];
            }

            return pixels;
        }

        // An ordinary tangent-space normal map. The importer packs it into the layout the ocean shader unpacks.
        private static Color32[] EncodeNormals(Vector3[] normals)
        {
            var pixels = new Color32[normals.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                Vector3 n = normals[i] * 0.5f + new Vector3(0.5f, 0.5f, 0.5f);
                pixels[i] = new Color(n.x, n.y, n.z, 1f);
            }

            return pixels;
        }

        private static Texture2D Save(Color32[] pixels, int resolution, string path, bool isNormal)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.maxTextureSize = resolution;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static ComputeShader LoadComputeShader() =>
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SDKConfiguration.BasePath + COMPUTE_SHADER_PATH);
    }
}
