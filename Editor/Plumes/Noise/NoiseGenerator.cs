using System.Collections.Generic;
using Redux.VFX.Plume;
using Redux.VFX.Plume.Services;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Ksp2UnityTools.Editor.Plumes.Noise
{
    public class NoiseGenerator : MonoBehaviour
    {
        private static IPlumeLogger Logger => ServiceProvider.GetService<IPlumeLogger>();

        private const int ComputeThreadGroupSize = 8;
        private const string DetailNoiseName = "DetailNoise";
        private const string ShapeNoiseName = "ShapeNoise";

        public enum CloudNoiseType
        {
            Shape,
            Detail
        }

        public enum TextureChannel
        {
            R,
            G,
            B,
            A
        }

        [Header("Editor Settings")] public CloudNoiseType ActiveTextureType;
        public TextureChannel ActiveChannel;
        public bool AutoUpdate;
        public bool LogComputeTime;

        [Header("Noise Settings")] public int ShapeResolution = 132;
        public int DetailResolution = 32;

        public WorleyNoiseSettings[] ShapeSettings;
        public WorleyNoiseSettings[] DetailSettings;
        public ComputeShader NoiseCompute;
        public ComputeShader Copy;

        [Header("Viewer Settings")] public bool ViewerEnabled;
        public bool ViewerGreyscale = true;
        public bool ViewerShowAllChannels;
        [Range(0, 1)] public float ViewerSliceDepth;
        [Range(1, 5)] public float ViewerTileAmount = 1;
        [Range(0, 1)] public float ViewerSize = 1;

        // Internal
        private List<ComputeBuffer> _buffersToRelease;
        private bool _updateNoise;

        [HideInInspector] public bool ShowSettingsEditor = true;
        [SerializeField] [HideInInspector] public RenderTexture ShapeTexture;
        [SerializeField] [HideInInspector] public RenderTexture DetailTexture;

        private void UpdateNoise()
        {
            ValidateParamaters();
            CreateTexture(ref ShapeTexture, ShapeResolution, ShapeNoiseName);
            CreateTexture(ref DetailTexture, DetailResolution, DetailNoiseName);

            if (!_updateNoise || !NoiseCompute)
            {
                return;
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();

            _updateNoise = false;
            WorleyNoiseSettings activeSettings = ActiveSettings;
            if (activeSettings == null)
            {
                return;
            }

            _buffersToRelease = new List<ComputeBuffer>();

            int activeTextureResolution = ActiveTexture.width;

            // Set values:
            NoiseCompute.SetFloat("persistence", activeSettings.Persistence);
            NoiseCompute.SetInt("resolution", activeTextureResolution);
            NoiseCompute.SetVector("channelMask", ChannelMask);

            // Set noise gen kernel data:
            NoiseCompute.SetTexture(0, "Result", ActiveTexture);
            ComputeBuffer minMaxBuffer = CreateBuffer(new int[] { int.MaxValue, 0 }, sizeof(int), "minMax", 0);
            UpdateWorley(ActiveSettings);
            NoiseCompute.SetTexture(0, "Result", ActiveTexture);
            //var noiseValuesBuffer = CreateBuffer (activeNoiseValues, sizeof (float) * 4, "values");

            // Dispatch noise gen kernel
            int numThreadGroups = Mathf.CeilToInt(activeTextureResolution / (float)ComputeThreadGroupSize);
            NoiseCompute.Dispatch(0, numThreadGroups, numThreadGroups, numThreadGroups);

            // Set normalization kernel data:
            NoiseCompute.SetBuffer(1, "minMax", minMaxBuffer);
            NoiseCompute.SetTexture(1, "Result", ActiveTexture);
            // Dispatch normalization kernel
            NoiseCompute.Dispatch(1, numThreadGroups, numThreadGroups, numThreadGroups);

            if (LogComputeTime)
            {
                // Get minmax data just to force main thread to wait until compute shaders are finished.
                // This allows us to measure the execution time.
                int[] minMax = new int[2];
                minMaxBuffer.GetData(minMax);

                Logger.LogInfo($"Noise Generation: {timer.ElapsedMilliseconds}ms");
            }

            // Release buffers
            foreach (ComputeBuffer buffer in _buffersToRelease)
            {
                buffer.Release();
            }
        }

        private void Load(string saveName, RenderTexture target)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            saveName = sceneName + "_" + saveName;
            var savedTex = (Texture3D)Resources.Load(saveName);
            if (savedTex != null && savedTex.width == target.width)
            {
                Copy.SetTexture(0, "tex", savedTex);
                Copy.SetTexture(0, "renderTex", target);
                int numThreadGroups = Mathf.CeilToInt(savedTex.width / 8f);
                Copy.Dispatch(0, numThreadGroups, numThreadGroups, numThreadGroups);
            }
        }

        public RenderTexture ActiveTexture => ActiveTextureType == CloudNoiseType.Shape ? ShapeTexture : DetailTexture;

        public WorleyNoiseSettings ActiveSettings
        {
            get
            {
                WorleyNoiseSettings[] settings =
                    ActiveTextureType == CloudNoiseType.Shape ? ShapeSettings : DetailSettings;
                int activeChannelIndex = (int)ActiveChannel;
                return activeChannelIndex >= settings.Length ? null : settings[activeChannelIndex];
            }
        }

        public Vector4 ChannelMask
        {
            get
            {
                var channelWeight = new Vector4(
                    ActiveChannel == TextureChannel.R ? 1 : 0,
                    ActiveChannel == TextureChannel.G ? 1 : 0,
                    ActiveChannel == TextureChannel.B ? 1 : 0,
                    ActiveChannel == TextureChannel.A ? 1 : 0
                );
                return channelWeight;
            }
        }

        private void UpdateWorley(WorleyNoiseSettings settings)
        {
            var prng = new System.Random(settings.Seed);
            CreateWorleyPointsBuffer(prng, settings.NumDivisionsA, "pointsA");
            CreateWorleyPointsBuffer(prng, settings.NumDivisionsB, "pointsB");
            CreateWorleyPointsBuffer(prng, settings.NumDivisionsC, "pointsC");

            NoiseCompute.SetInt("numCellsA", settings.NumDivisionsA);
            NoiseCompute.SetInt("numCellsB", settings.NumDivisionsB);
            NoiseCompute.SetInt("numCellsC", settings.NumDivisionsC);
            NoiseCompute.SetBool("invertNoise", settings.Invert);
            NoiseCompute.SetInt("tile", settings.Tile);
        }

        private void CreateWorleyPointsBuffer(System.Random prng, int numCellsPerAxis, string bufferName)
        {
            var points = new Vector3[numCellsPerAxis * numCellsPerAxis * numCellsPerAxis];
            float cellSize = 1f / numCellsPerAxis;

            for (int x = 0; x < numCellsPerAxis; x++)
            {
                for (int y = 0; y < numCellsPerAxis; y++)
                {
                    for (int z = 0; z < numCellsPerAxis; z++)
                    {
                        float randomX = (float)prng.NextDouble();
                        float randomY = (float)prng.NextDouble();
                        float randomZ = (float)prng.NextDouble();
                        Vector3 randomOffset = new Vector3(randomX, randomY, randomZ) * cellSize;
                        Vector3 cellCorner = new Vector3(x, y, z) * cellSize;

                        int index = x + numCellsPerAxis * (y + z * numCellsPerAxis);
                        points[index] = cellCorner + randomOffset;
                    }
                }
            }

            CreateBuffer(points, sizeof(float) * 3, bufferName);
        }

        // Create buffer with some data, and set in shader. Also add to list of buffers to be released
        private ComputeBuffer CreateBuffer(System.Array data, int stride, string bufferName, int kernel = 0)
        {
            var buffer = new ComputeBuffer(data.Length, stride, ComputeBufferType.Structured);
            _buffersToRelease.Add(buffer);
            buffer.SetData(data);
            NoiseCompute.SetBuffer(kernel, bufferName, buffer);
            return buffer;
        }

        private void CreateTexture(ref RenderTexture texture, int resolution, string textureName)
        {
            const GraphicsFormat format = GraphicsFormat.R16G16B16A16_UNorm;
            if (texture == null ||
                !texture.IsCreated() ||
                texture.width != resolution ||
                texture.height != resolution ||
                texture.volumeDepth != resolution ||
                texture.graphicsFormat != format)
            {
                //Logger.LogInfo ("Create tex: update noise: " + updateNoise);
                if (texture != null)
                {
                    texture.Release();
                }

                texture = new RenderTexture(resolution, resolution, 0)
                {
                    graphicsFormat = format,
                    volumeDepth = resolution,
                    enableRandomWrite = true,
                    dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                    name = textureName
                };

                texture.Create();
                Load(textureName, texture);
            }

            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
        }

        public void ManualUpdate()
        {
            _updateNoise = true;
            UpdateNoise();
        }

        public void ActiveNoiseSettingsChanged()
        {
            if (AutoUpdate)
            {
                _updateNoise = true;
            }
        }

        private void ValidateParamaters()
        {
            DetailResolution = Mathf.Max(1, DetailResolution);
            ShapeResolution = Mathf.Max(1, ShapeResolution);
        }
    }
}