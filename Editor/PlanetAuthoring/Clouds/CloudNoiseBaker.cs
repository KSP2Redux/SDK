using System.IO;
using KSP.VolumeCloud;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Bakes a body's own cloud noise volumes on the GPU and gives them to every layer.
    /// </summary>
    /// <remarks>
    /// The kernel is a port of the game's CPU noise generators: the base volume is
    /// <see cref="PerlinNoiseGenerator.OctaveNoise" /> with the defaults of <see cref="BasicShape3dGenerator" />, and the
    /// detail volume is <see cref="WorleyNoiseGenerator.OctaveNoise" /> with those of <see cref="Texture3dGenerator" />.
    /// The cloud shaders read only the first channel of each, so the volumes are single-channel, at the sizes stock's own
    /// single-channel volumes ship at. Stock's own kernel is not shipped, so a bake approximates stock's noise rather
    /// than reproducing it.
    /// </remarks>
    public static class CloudNoiseBaker
    {
        /// <summary>
        /// The noise a kernel produces.
        /// </summary>
        public enum NoiseKind
        {
            /// <summary>Octave Perlin noise, used for the base volume.</summary>
            Perlin,
            /// <summary>Octave Worley noise, used for the detail volume.</summary>
            Worley,
        }

        /// <summary>
        /// The resolution of the base volume.
        /// </summary>
        public const int BASE_RESOLUTION = 128;

        /// <summary>
        /// The resolution of the detail volume.
        /// </summary>
        public const int DETAIL_RESOLUTION = 64;

        private const string COMPUTE_SHADER_PATH = "/Assets/Shaders/PlanetAuthoring/Clouds/CloudNoise.compute";
        private const int THREAD_GROUP_SIZE = 4;
        private const float PERSISTENCE = 0.5f;

        // BasicShape3dGenerator's Perlin channel and Texture3dGenerator's first Worley channel.
        private const int BASE_PERIOD = 16;
        private const int BASE_OCTAVES = 4;
        private const int DETAIL_PERIOD = 16;
        private const int DETAIL_OCTAVES = 3;

        /// <summary>
        /// Gets a value indicating whether this machine can run the bake.
        /// </summary>
        public static bool IsSupported => SystemInfo.supportsComputeShaders && LoadComputeShader() != null;

        /// <summary>
        /// Generates a tileable noise volume.
        /// </summary>
        /// <param name="kind">The noise to generate.</param>
        /// <param name="resolution">The volume's edge length in voxels.</param>
        /// <param name="period">The noise's base period across the volume.</param>
        /// <param name="octaves">The number of octaves.</param>
        /// <returns>The voxel values, x fastest then y then z, or null when the bake cannot run.</returns>
        public static float[] Generate(NoiseKind kind, int resolution, int period, int octaves)
        {
            ComputeShader shader = LoadComputeShader();
            if (shader == null || !SystemInfo.supportsComputeShaders)
                return null;

            int kernel = shader.FindKernel(kind == NoiseKind.Perlin ? "BakePerlin" : "BakeWorley");
            var values = new float[resolution * resolution * resolution];
            using var buffer = new ComputeBuffer(values.Length, sizeof(float));
            shader.SetBuffer(kernel, "_Result", buffer);
            shader.SetInt("_Resolution", resolution);
            shader.SetInt("_Period", period);
            shader.SetInt("_Octaves", octaves);
            shader.SetFloat("_Persistence", PERSISTENCE);
            int groups = Mathf.CeilToInt(resolution / (float)THREAD_GROUP_SIZE);
            shader.Dispatch(kernel, groups, groups, groups);
            buffer.GetData(values);
            return values;
        }

        /// <summary>
        /// Bakes the body's base and detail noise beside its configuration and gives them to every layer.
        /// </summary>
        /// <param name="configuration">The configuration whose layers get the noise.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the noise was baked and assigned, false otherwise.</returns>
        public static bool TryBake(VolumeCloudConfiguration configuration, out string message)
        {
            string configurationPath = AssetDatabase.GetAssetPath(configuration);
            if (string.IsNullOrEmpty(configurationPath))
            {
                message = "The configuration is not an asset.";
                return false;
            }

            if (!IsSupported)
            {
                message = "This machine cannot run the noise bake's compute shader.";
                return false;
            }

            string folder = Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
            string stem = string.IsNullOrEmpty(configuration.bodyName) ? configuration.name : configuration.bodyName;
            Texture3D baseNoise = SaveVolume(
                $"{folder}/{stem}_CloudNoise_Base.asset",
                Generate(NoiseKind.Perlin, BASE_RESOLUTION, BASE_PERIOD, BASE_OCTAVES),
                BASE_RESOLUTION
            );
            Texture3D detailNoise = SaveVolume(
                $"{folder}/{stem}_CloudNoise_Detail.asset",
                Generate(NoiseKind.Worley, DETAIL_RESOLUTION, DETAIL_PERIOD, DETAIL_OCTAVES),
                DETAIL_RESOLUTION
            );

            Undo.RecordObject(configuration, "Bake Cloud Noise");
            foreach (VolumeCloudConfiguration.CumulusData layer in configuration.cumulusList)
            {
                layer.baseTexture = baseNoise;
                layer.detailTexture = detailNoise;
            }

            EditorUtility.SetDirty(configuration);
            AssetDatabase.SaveAssets();
            message = $"Baked {baseNoise.name} and {detailNoise.name} and gave them to every layer.";
            return true;
        }

        private static Texture3D SaveVolume(string path, float[] values, int resolution)
        {
            var bytes = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[i]) * 255f);
            }

            var volume = new Texture3D(resolution, resolution, resolution, GraphicsFormat.R8_UNorm, TextureCreationFlags.MipChain)
            {
                name = Path.GetFileNameWithoutExtension(path),
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };
            volume.SetPixelData(bytes, 0);
            volume.Apply(true, false);

            var existing = AssetDatabase.LoadAssetAtPath<Texture3D>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(volume, existing);
                Object.DestroyImmediate(volume);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(volume, path);
            return volume;
        }

        private static ComputeShader LoadComputeShader() =>
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SDKConfiguration.BasePath + COMPUTE_SHADER_PATH);
    }
}
