using System;
using KSP.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Lighting
{
    /// <summary>
    /// Shows the SceneView the way the flight camera sees it: in HDR, through the game's global flight post-processing.
    /// </summary>
    /// <remarks>
    /// Without it the SceneView renders in LDR with no exposure or tonemapping, so everything past white clips. The post
    /// stack is the stock flight camera's <see cref="PostProcessLayer" /> with the global flight profile from the
    /// Graphics Manager. Star and body volumes are not blended in. The sun itself is lit by <see cref="StarLight" />.
    /// </remarks>
    public class GameLookPreviewDriver : IPreviewDriver
    {
        private const string FLIGHT_CAMERA_PREFAB_KEY =
            "Assets/Scripts/Simulation Scripts/View/Cameras/prefabs/FlightCameraAssembly_Physics.prefab";

        private const string GRAPHICS_MANAGER_PREFAB_KEY = "Graphics Manager.prefab";
        private const int MAX_CONSECUTIVE_FAILURES = 3;

        private AsyncOperationHandle<GameObject> _flightCameraHandle;
        private AsyncOperationHandle<GameObject> _graphicsManagerHandle;
        private PostProcessLayer _stockLayer;
        private PostProcessProfile _flightProfile;
        private PostProcessLayer _layer;
        private PostProcessVolume _volume;
        private Camera _hostCamera;
        private int _consecutiveFailures;

        /// <inheritdoc />
        public bool Enabled { get; set; } = true;

        /// <inheritdoc />
        public bool Booted { get; private set; }

        /// <inheritdoc />
        public string Status { get; private set; } = "not attached";

        /// <summary>
        /// Creates a global volume carrying copies of a profile's effects.
        /// </summary>
        /// <remarks>
        /// <see cref="PostProcessManager.QuickVolume" /> adopts the effect objects it is given, and destroying its
        /// profile destroys them, so it gets copies rather than the profile's own.
        /// </remarks>
        /// <param name="layer">The GameObject layer the volume sits on.</param>
        /// <param name="profile">The profile to copy.</param>
        /// <returns>The volume.</returns>
        public static PostProcessVolume CreateVolume(int layer, PostProcessProfile profile)
        {
            var settings = new PostProcessEffectSettings[profile.settings.Count];
            for (int i = 0; i < settings.Length; i++)
            {
                settings[i] = Object.Instantiate(profile.settings[i]);
                settings[i].hideFlags = HideFlags.HideAndDontSave;
            }

            return PostProcessManager.instance.QuickVolume(layer, 0f, settings);
        }

        /// <summary>
        /// Destroys a volume made by <see cref="CreateVolume" />, along with its copied effects.
        /// </summary>
        /// <param name="volume">The volume.</param>
        public static void DestroyVolume(PostProcessVolume volume)
        {
            if (volume == null)
                return;

            RuntimeUtilities.DestroyVolume(volume, true, true);
        }

        /// <inheritdoc />
        public bool Attach()
        {
            if (Booted)
                return true;

            _flightCameraHandle = Addressables.LoadAssetAsync<GameObject>(FLIGHT_CAMERA_PREFAB_KEY);
            GameObject flightCamera = _flightCameraHandle.WaitForCompletion();
            _stockLayer = flightCamera != null ? flightCamera.GetComponentInChildren<PostProcessLayer>(true) : null;
            _graphicsManagerHandle = Addressables.LoadAssetAsync<GameObject>(GRAPHICS_MANAGER_PREFAB_KEY);
            GameObject graphicsManager = _graphicsManagerHandle.WaitForCompletion();
            PostProcessingSystem postProcessing = graphicsManager != null
                ? graphicsManager.GetComponentInChildren<PostProcessingSystem>(true)
                : null;
            _flightProfile = postProcessing != null ? postProcessing.FlightPostProfile : null;
            if (_stockLayer == null || _flightProfile == null)
            {
                Status = "The stock flight camera or its post-processing was not found. Run 'ThunderKit > Import Ksp2 To Editor'.";
                ReleaseHandles();
                return false;
            }

            Booted = true;
            _consecutiveFailures = 0;
            Status = string.Empty;
            return true;
        }

        /// <inheritdoc />
        public void Detach()
        {
            if (!Booted)
                return;

            Unhost();
            ReleaseHandles();
            Booted = false;
            Status = "not attached";
        }

        /// <inheritdoc />
        public void Pump(Camera camera)
        {
            if (!Booted || camera == null)
                return;

            if (!Enabled)
            {
                Unhost();
                return;
            }

            try
            {
                if (_hostCamera != camera)
                {
                    Unhost();
                    Host(camera);
                }

                // The SceneView resets this on every repaint, after which pre-cull is the last word.
                camera.allowHDR = true;
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;
                Debug.LogError(
                    $"[GameLookPreview] Pump failed ({_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}): {e}"
                );
                if (_consecutiveFailures < MAX_CONSECUTIVE_FAILURES)
                    return;

                Unhost();
                Enabled = false;
                Status = "disabled after repeated failures";
            }
        }

        private void Host(Camera camera)
        {
            _layer = camera.gameObject.AddComponent<PostProcessLayer>();
            // Carries over the stock volume mask, anti-aliasing and the post-processing resources.
            EditorUtility.CopySerialized(_stockLayer, _layer);
            _layer.hideFlags = HideFlags.HideAndDontSave;
            _layer.volumeTrigger = camera.transform;
            _volume = CreateVolume(FirstLayer(_layer.volumeLayer), _flightProfile);
            _hostCamera = camera;
        }

        private void Unhost()
        {
            DestroyVolume(_volume);
            if (_layer != null)
            {
                Object.DestroyImmediate(_layer);
            }

            _volume = null;
            _layer = null;
            _hostCamera = null;
        }

        private void ReleaseHandles()
        {
            if (_flightCameraHandle.IsValid())
            {
                Addressables.Release(_flightCameraHandle);
            }

            if (_graphicsManagerHandle.IsValid())
            {
                Addressables.Release(_graphicsManagerHandle);
            }

            _flightCameraHandle = default;
            _graphicsManagerHandle = default;
            _stockLayer = null;
            _flightProfile = null;
        }

        private static int FirstLayer(LayerMask mask)
        {
            for (int layer = 0; layer < 32; layer++)
            {
                if ((mask.value & (1 << layer)) != 0)
                    return layer;
            }

            return 0;
        }
    }
}
