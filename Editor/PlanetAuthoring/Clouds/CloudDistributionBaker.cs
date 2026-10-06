using System.IO;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Generates a cloud layer's distribution map, the cubemap of where its clouds form, from warped noise on the GPU.
    /// </summary>
    /// <remarks>
    /// Noise is evaluated in 3D at each texel's direction, so the map is seamless across faces and even at the poles.
    /// The cloud shaders only read a distribution map's first channel, so the map is single-channel and can be stored as
    /// BC4.
    /// </remarks>
    public static class CloudDistributionBaker
    {
        private const string COMPUTE_SHADER_PATH = "/Assets/Shaders/PlanetAuthoring/Clouds/CloudNoise.compute";
        private const int LATITUDE_SAMPLES = 64;
        private const int FACE_COUNT = 6;
        private const int FACE_GROUP_SIZE = 8;
        private const int LINEAR_GROUP_SIZE = 64;

        /// <summary>
        /// Gets a value indicating whether this machine can run the bake.
        /// </summary>
        public static bool IsSupported => SystemInfo.supportsComputeShaders && LoadComputeShader() != null;

        /// <summary>
        /// Generates every face of a distribution map.
        /// </summary>
        /// <param name="settings">The map's settings.</param>
        /// <param name="bodyRadiusMeters">The radius the feature size is measured against, in meters.</param>
        /// <param name="resolution">The edge length of each face, in texels.</param>
        /// <returns>The texel values, face by face in Unity's cube face order, rows running down each face, or null when the bake cannot run.</returns>
        public static float[] Generate(CloudDistributionSettings settings, double bodyRadiusMeters, int resolution)
        {
            ComputeShader shader = LoadComputeShader();
            if (shader == null || !SystemInfo.supportsComputeShaders)
                return null;

            int kernel = shader.FindKernel("BakeDistribution");
            var values = new float[FACE_COUNT * resolution * resolution];
            using var result = new ComputeBuffer(values.Length, sizeof(float));
            using ComputeBuffer latitude = BindSettings(shader, kernel, settings, bodyRadiusMeters);
            shader.SetInt("_Resolution", resolution);
            shader.SetBuffer(kernel, "_Result", result);
            int groups = Mathf.CeilToInt(resolution / (float)FACE_GROUP_SIZE);
            shader.Dispatch(kernel, groups, groups, FACE_COUNT);
            result.GetData(values);
            return values;
        }

        /// <summary>
        /// Evaluates the distribution at given directions, without baking a map.
        /// </summary>
        /// <param name="settings">The map's settings.</param>
        /// <param name="bodyRadiusMeters">The radius the feature size is measured against, in meters.</param>
        /// <param name="directions">The directions from the planet center to evaluate.</param>
        /// <returns>The coverage at each direction, or null when the bake cannot run.</returns>
        public static float[] Evaluate(CloudDistributionSettings settings, double bodyRadiusMeters, Vector3[] directions)
        {
            ComputeShader shader = LoadComputeShader();
            if (shader == null || !SystemInfo.supportsComputeShaders)
                return null;

            int kernel = shader.FindKernel("EvaluateDistribution");
            using ComputeBuffer latitude = BindSettings(shader, kernel, settings, bodyRadiusMeters);
            return RunOverDirections(shader, kernel, directions, null);
        }

        /// <summary>
        /// Samples a cubemap's first channel at given directions, the way the cloud shaders read a distribution map.
        /// </summary>
        /// <param name="cubemap">The cubemap to sample.</param>
        /// <param name="directions">The directions to sample.</param>
        /// <returns>The first channel at each direction, or null when the bake cannot run.</returns>
        public static float[] Probe(Cubemap cubemap, Vector3[] directions)
        {
            ComputeShader shader = LoadComputeShader();
            if (shader == null || !SystemInfo.supportsComputeShaders)
                return null;

            return RunOverDirections(shader, shader.FindKernel("ProbeCubemap"), directions, cubemap);
        }

        /// <summary>
        /// Builds a single-channel cubemap from generated texel values.
        /// </summary>
        /// <param name="values">The texel values from <see cref="Generate" />.</param>
        /// <param name="resolution">The edge length of each face, in texels.</param>
        /// <param name="compress">True to store the map as BC4, false to keep it uncompressed.</param>
        /// <returns>The cubemap, with mipmaps, since the cloud shaders pick a mip by distance.</returns>
        public static Cubemap CreateCubemap(float[] values, int resolution, bool compress)
        {
            var cubemap = new Cubemap(resolution, TextureFormat.R8, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
            };
            int faceTexels = resolution * resolution;
            var face = new byte[faceTexels];
            for (int faceIndex = 0; faceIndex < FACE_COUNT; faceIndex++)
            {
                for (int i = 0; i < faceTexels; i++)
                {
                    face[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(values[faceIndex * faceTexels + i]) * 255f);
                }

                cubemap.SetPixelData(face, 0, (CubemapFace)faceIndex);
            }

            cubemap.Apply(true, false);
            if (compress)
            {
                EditorUtility.CompressCubemapTexture(cubemap, TextureFormat.BC4, (int)TextureCompressionQuality.Normal);
            }

            return cubemap;
        }

        /// <summary>
        /// Generates a layer's distribution map from its stored settings, saves it beside the configuration and gives it
        /// to the layer.
        /// </summary>
        /// <param name="configuration">The configuration the layer belongs to.</param>
        /// <param name="layerIndex">The layer's index in the configuration's layer list.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the map was generated and assigned, false otherwise.</returns>
        public static bool TryBake(VolumeCloudConfiguration configuration, int layerIndex, out string message)
        {
            string configurationPath = AssetDatabase.GetAssetPath(configuration);
            if (string.IsNullOrEmpty(configurationPath) || layerIndex < 0 || layerIndex >= configuration.cumulusList.Count)
            {
                message = "No layer to generate a distribution map for.";
                return false;
            }

            if (!IsSupported)
            {
                message = "This machine cannot run the distribution bake's compute shader.";
                return false;
            }

            VolumeCloudConfiguration.CumulusData layer = configuration.cumulusList[layerIndex];
            CloudDistributionSettings settings = AuthoringSidecars.GetOrCreate(configuration).GetDistribution(layer.layerName);
            int resolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(settings.Resolution, 64, 4096));
            float[] values = Generate(settings, configuration.planetRadius, resolution);
            Cubemap cubemap = CreateCubemap(values, resolution, settings.Compress);

            string folder = Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
            string body = string.IsNullOrEmpty(configuration.bodyName) ? configuration.name : configuration.bodyName;
            string layerName = string.IsNullOrEmpty(layer.layerName) ? $"Layer{layerIndex + 1}" : layer.layerName.Replace(' ', '_');
            string path = $"{folder}/{body}_{layerName}_Distribution.asset";
            cubemap.name = Path.GetFileNameWithoutExtension(path);
            Cubemap saved = Save(cubemap, path);

            Undo.RecordObject(configuration, "Generate Cloud Distribution");
            layer.distributionMap = saved;
            EditorUtility.SetDirty(configuration);
            AssetDatabase.SaveAssets();
            configuration.OnCloudLayerChanged?.Invoke(configuration);
            message = $"Generated {saved.name} at {resolution} per face.";
            return true;
        }

        private static ComputeBuffer BindSettings(
            ComputeShader shader,
            int kernel,
            CloudDistributionSettings settings,
            double bodyRadiusMeters
        )
        {
            var profile = new float[LATITUDE_SAMPLES];
            for (int i = 0; i < LATITUDE_SAMPLES; i++)
            {
                float position = i / (float)(LATITUDE_SAMPLES - 1);
                profile[i] = settings.LatitudeProfile != null && settings.LatitudeProfile.length > 0
                    ? Mathf.Max(0f, settings.LatitudeProfile.Evaluate(position))
                    : 1f;
            }

            var latitude = new ComputeBuffer(LATITUDE_SAMPLES, sizeof(float));
            latitude.SetData(profile);
            shader.SetBuffer(kernel, "_LatitudeProfile", latitude);
            shader.SetInt("_LatitudeSamples", LATITUDE_SAMPLES);
            shader.SetFloat("_Frequency", (float)(bodyRadiusMeters / (Mathf.Max(1f, settings.FeatureSizeKm) * 1000.0)));
            shader.SetVector("_SeedOffset", SeedOffset(settings.Seed));
            shader.SetInt("_Octaves", Mathf.Clamp(settings.Octaves, 1, 8));
            shader.SetFloat("_Roughness", settings.Roughness);
            shader.SetFloat("_WarpStrength", settings.WarpStrength);
            shader.SetFloat("_Coverage", settings.Coverage);
            shader.SetFloat("_Softness", Mathf.Max(0.001f, settings.Softness));
            return latitude;
        }

        private static float[] RunOverDirections(ComputeShader shader, int kernel, Vector3[] directions, Cubemap probe)
        {
            var values = new float[directions.Length];
            using var input = new ComputeBuffer(directions.Length, sizeof(float) * 3);
            using var result = new ComputeBuffer(directions.Length, sizeof(float));
            input.SetData(directions);
            shader.SetBuffer(kernel, "_Directions", input);
            shader.SetBuffer(kernel, "_Result", result);
            shader.SetInt("_DirectionCount", directions.Length);
            if (probe != null)
            {
                shader.SetTexture(kernel, "_Probe", probe);
            }

            shader.Dispatch(kernel, Mathf.CeilToInt(directions.Length / (float)LINEAR_GROUP_SIZE), 1, 1);
            result.GetData(values);
            return values;
        }

        // A seed picks a different region of the noise field, far enough apart that nearby seeds share nothing.
        private static Vector3 SeedOffset(int seed)
        {
            var random = new System.Random(seed);
            return new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()) * 200f;
        }

        private static Cubemap Save(Cubemap cubemap, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(cubemap, path);
                return cubemap;
            }

            EditorUtility.CopySerialized(cubemap, existing);
            Object.DestroyImmediate(cubemap);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static ComputeShader LoadComputeShader() =>
            AssetDatabase.LoadAssetAtPath<ComputeShader>(SDKConfiguration.BasePath + COMPUTE_SHADER_PATH);
    }
}
