using System.IO;
using KSP.VolumeCloud;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Bakes a cloud layer's scaled clouds, the cubemaps the game draws the layer from once the body switches to scaled
    /// space.
    /// </summary>
    /// <remarks>
    /// Runs the game's own <see cref="VolumeCloudBakeCubemap" /> on a hidden camera at the planet's center, with the
    /// cloud shaders of the stock flight camera, and saves its color, underside and normal cubemaps beside the
    /// configuration. The layer then points at them, and the scaled configuration is re-synced.
    /// </remarks>
    public static class ScaledCloudBaker
    {
        private const string FLIGHT_CAMERA_PREFAB_KEY =
            "Assets/Scripts/Simulation Scripts/View/Cameras/prefabs/FlightCameraAssembly_Physics.prefab";

        private const int FACE_SIZE = 2048;

        // The stock renderer's shader fields and the baker's fields they fill.
        private static readonly (string Renderer, string Baker)[] SHADER_FIELDS =
        {
            ("_cumulusCloudShader", "cumulusCloudShader"),
            ("_cloudsComputeShader", "cloudsComputeShader"),
            ("_rayCheckGetTile", "rayCheckGetTile"),
            ("_listCreationShader", "listCreationShader"),
            ("_listRenderingShader", "listRenderingShader"),
            ("_listDepthShader", "listDepthShader"),
        };

        /// <summary>
        /// Bakes one layer's scaled clouds and points the layer at them.
        /// </summary>
        /// <param name="configuration">The configuration the layer belongs to.</param>
        /// <param name="layerIndex">The layer's index in the configuration's layer list.</param>
        /// <param name="message">A status line describing the outcome or the reason for failure.</param>
        /// <returns>True if the layer was baked, false otherwise.</returns>
        public static bool TryBake(VolumeCloudConfiguration configuration, int layerIndex, out string message)
        {
            string configurationPath = AssetDatabase.GetAssetPath(configuration);
            if (string.IsNullOrEmpty(configurationPath) || layerIndex < 0 || layerIndex >= configuration.cumulusList.Count)
            {
                message = "No layer to bake.";
                return false;
            }

            VolumeCloudConfiguration.CumulusData layer = configuration.cumulusList[layerIndex];
            if (layer.distributionMap == null || layer.baseTexture == null || layer.detailTexture == null)
            {
                message = "The layer needs its distribution map and both noise volumes before it can be baked.";
                return false;
            }

            if (!EditorPqsBootstrap.EnsureStockCatalog())
            {
                message = "The base-game catalog is not registered. Run 'ThunderKit > Import Ksp2 To Editor'.";
                return false;
            }

            AsyncOperationHandle<GameObject> flightCameraHandle = Addressables.LoadAssetAsync<GameObject>(FLIGHT_CAMERA_PREFAB_KEY);
            GameObject host = null;
            RenderTexture target = null;
            try
            {
                GameObject flightCamera = flightCameraHandle.WaitForCompletion();
                VolumeCloudRenderer stockRenderer = flightCamera != null
                    ? flightCamera.GetComponentInChildren<VolumeCloudRenderer>(true)
                    : null;
                if (stockRenderer == null)
                {
                    message = "The stock cloud renderer was not found. Run 'ThunderKit > Import Ksp2 To Editor'.";
                    return false;
                }

                target = new RenderTexture(FACE_SIZE, FACE_SIZE, 24, RenderTextureFormat.ARGBHalf) { hideFlags = HideFlags.HideAndDontSave };
                host = CreateHost(stockRenderer, target, out VolumeCloudBakeCubemap baker);
                EditorUtility.DisplayProgressBar("Bake Scaled Clouds", $"Baking {layer.layerName}", 0.5f);
                if (!baker.BakeForEditor(configuration, layer, out Cubemap color, out Cubemap bottom, out Cubemap normals))
                {
                    message = "The bake did not finish. Check the console for shader errors.";
                    return false;
                }

                string folder = Path.GetDirectoryName(configurationPath)?.Replace('\\', '/');
                string stem = $"{(string.IsNullOrEmpty(configuration.bodyName) ? configuration.name : configuration.bodyName)}_{SafeName(layer.layerName, layerIndex)}";
                Undo.RecordObject(configuration, "Bake Scaled Clouds");
                layer.bakedScaledTexture = Save(color, $"{folder}/{stem}_ScaledClouds.asset");
                layer.bakedBottomScaledTexture = Save(bottom, $"{folder}/{stem}_ScaledClouds_Bottom.asset");
                layer.cloudNormalMap = Save(normals, $"{folder}/{stem}_ScaledClouds_Normal.asset");
                EditorUtility.SetDirty(configuration);
                CloudSetup.SyncScaled(configuration);
                AssetDatabase.SaveAssets();
                message = $"Baked {stem}'s scaled clouds.";
                return true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                if (host != null)
                {
                    Object.DestroyImmediate(host);
                }

                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }

                Addressables.Release(flightCameraHandle);
            }
        }

        // A 90 degree square camera at the origin, which is where the bake puts the planet's center, drawing nothing
        // of the scene so the clouds are raymarched against an empty depth buffer.
        private static GameObject CreateHost(VolumeCloudRenderer stockRenderer, RenderTexture target, out VolumeCloudBakeCubemap baker)
        {
            var host = new GameObject("ScaledCloudBake") { hideFlags = HideFlags.HideAndDontSave };
            host.SetActive(false);
            var camera = host.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 90f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1e7f;
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = true;
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.targetTexture = target;

            baker = host.AddComponent<VolumeCloudBakeCubemap>();
            var from = new SerializedObject(stockRenderer);
            var to = new SerializedObject(baker);
            foreach ((string rendererField, string bakerField) in SHADER_FIELDS)
            {
                to.FindProperty(bakerField).objectReferenceValue = from.FindProperty(rendererField).objectReferenceValue;
            }

            to.ApplyModifiedPropertiesWithoutUndo();
            baker.InitializeForEditor();
            host.SetActive(true);
            return host;
        }

        private static Cubemap Save(Cubemap cubemap, string path)
        {
            cubemap.name = Path.GetFileNameWithoutExtension(path);
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

        private static string SafeName(string layerName, int index)
        {
            if (string.IsNullOrEmpty(layerName))
                return $"Layer{index + 1}";

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                layerName = layerName.Replace(invalid, '_');
            }

            return layerName.Replace(' ', '_');
        }
    }
}
