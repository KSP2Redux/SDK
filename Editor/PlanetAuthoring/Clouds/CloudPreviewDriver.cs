using System;
using KSP.Rendering.Planets;
using KSP.VolumeCloud;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// Draws a body's volumetric clouds in the SceneView during a planet preview, through the game's own
    /// <see cref="VolumeCloudRenderer" />.
    /// </summary>
    /// <remarks>
    /// The configuration comes from the <see cref="CloudRenderHelper" /> on the PQS GameObject, at High quality,
    /// loaded through the same addressable reference the game loads. The renderer is hosted on the SceneView camera
    /// with the shaders of the stock flight camera, and reads the preview's sun, PQS and atmosphere through a
    /// <see cref="CloudPreviewEnvironment" />.
    /// </remarks>
    public class CloudPreviewDriver : IPreviewDriver
    {
        private const string FLIGHT_CAMERA_PREFAB_KEY =
            "Assets/Scripts/Simulation Scripts/View/Cameras/prefabs/FlightCameraAssembly_Physics.prefab";

        private const int MAX_CONSECUTIVE_FAILURES = 3;

        private readonly PQS _pqs;
        private readonly AtmospherePreviewDriver _atmosphere;

        private CloudRenderHelper _helper;
        private AsyncOperationHandle<VolumeCloudConfiguration> _configurationHandle;
        private AsyncOperationHandle<GameObject> _flightCameraHandle;
        private VolumeCloudRenderer _stockRenderer;
        private VolumeCloudRenderer _renderer;
        private CloudPreviewEnvironment _environment;
        private Camera _hostCamera;
        private int _consecutiveFailures;

        /// <summary>
        /// Creates a driver for the clouds of the body that owns <paramref name="pqs" />.
        /// </summary>
        /// <param name="pqs">The previewed body's PQS, which carries the cloud helper.</param>
        /// <param name="atmosphere">The preview's atmosphere driver, whose model the clouds composite inside.</param>
        public CloudPreviewDriver(PQS pqs, AtmospherePreviewDriver atmosphere)
        {
            _pqs = pqs;
            _atmosphere = atmosphere;
        }

        /// <inheritdoc />
        public bool Enabled { get; set; } = true;

        /// <inheritdoc />
        public bool Booted { get; private set; }

        /// <inheritdoc />
        public string Status { get; private set; } = "not attached";

        /// <summary>
        /// Gets the configuration being previewed, or <c>null</c> when nothing is booted.
        /// </summary>
        public VolumeCloudConfiguration Configuration => Booted ? _helper.CloudConfiguration : null;

        /// <inheritdoc />
        public bool Attach()
        {
            if (Booted)
                return true;

            if (_pqs == null || !_pqs.TryGetComponent(out _helper))
            {
                Status = "no clouds on this body";
                return true;
            }

            AssetReferenceT<VolumeCloudConfiguration> reference = _helper.HighQualityCloudConfiguration;
            if (reference == null || !reference.RuntimeKeyIsValid())
            {
                Status = "the cloud helper has no High quality configuration";
                return true;
            }

            _configurationHandle = Addressables.LoadAssetAsync<VolumeCloudConfiguration>(reference.RuntimeKey);
            VolumeCloudConfiguration configuration = _configurationHandle.WaitForCompletion();
            if (configuration == null)
            {
                Status = "the High quality cloud configuration did not load";
                ReleaseHandles();
                return false;
            }

            _flightCameraHandle = Addressables.LoadAssetAsync<GameObject>(FLIGHT_CAMERA_PREFAB_KEY);
            GameObject flightCamera = _flightCameraHandle.WaitForCompletion();
            _stockRenderer = flightCamera != null ? flightCamera.GetComponentInChildren<VolumeCloudRenderer>(true) : null;
            if (_stockRenderer == null)
            {
                Status = "The stock cloud renderer was not found. Run 'ThunderKit > Import Ksp2 To Editor'.";
                ReleaseHandles();
                return false;
            }

            _helper.InitializeForEditor(configuration);
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

            // The helper goes first, so the renderer drops the configuration before the renderer itself goes.
            _helper.ShutdownForEditor();
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

            // A hosted renderer keeps replaying its last commands until it is taken off the camera.
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

                _environment.Camera = camera;
                _renderer.RenderForEditor();
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;
                Debug.LogError(
                    $"[CloudPreview] Pump failed on '{_pqs.name}' ({_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}): {e}"
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
            _environment = new CloudPreviewEnvironment(_pqs, _atmosphere, camera);
            _renderer = camera.gameObject.AddComponent<VolumeCloudRenderer>();
            // Carries over the stock shader and compute references along with the stock render settings.
            EditorUtility.CopySerialized(_stockRenderer, _renderer);
            _renderer.hideFlags = HideFlags.HideAndDontSave;
            _renderer.InitializeForEditor(_environment);
            _hostCamera = camera;
        }

        private void Unhost()
        {
            if (_renderer != null)
            {
                _renderer.ShutdownForEditor();
                Object.DestroyImmediate(_renderer);
            }

            _renderer = null;
            _environment = null;
            _hostCamera = null;
        }

        private void ReleaseHandles()
        {
            if (_configurationHandle.IsValid())
            {
                Addressables.Release(_configurationHandle);
            }

            if (_flightCameraHandle.IsValid())
            {
                Addressables.Release(_flightCameraHandle);
            }

            _configurationHandle = default;
            _flightCameraHandle = default;
            _stockRenderer = null;
        }
    }
}
