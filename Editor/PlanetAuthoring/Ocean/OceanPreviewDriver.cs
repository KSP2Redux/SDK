using System;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// Draws a body's ocean in the SceneView during a planet preview, through the game's own <see cref="WaterManager" />
    /// and the PQS renderer's ocean.
    /// </summary>
    /// <remarks>
    /// The manager is created for the editor and ticked from the session's camera hook, running the same per-frame
    /// steps the game's late update runs. The underwater effect is not drawn, since only the game camera draws it.
    /// </remarks>
    public class OceanPreviewDriver : IPreviewDriver
    {
        private const int MAX_CONSECUTIVE_FAILURES = 3;

        // A SceneView that sat idle would otherwise jump the waves forward by however long it was left.
        private const float MAX_FRAME_SECONDS = 0.1f;

        // A dragged slider edits the spectrum or material every frame, and a restart rebuilds the wave data.
        private const double MIN_RESTART_SECONDS = 0.25;

        private readonly CoreCelestialBodyData _body;
        private readonly PQS _pqs;

        private OceanPreviewEnvironment _environment;
        private double _lastTickTime;
        private double _lastRestartTime;
        private int _assetVersion;
        private int _consecutiveFailures;

        /// <summary>
        /// Creates a driver for the ocean of the body that owns <paramref name="pqs" />.
        /// </summary>
        /// <param name="body">The previewed body.</param>
        /// <param name="pqs">The previewed body's PQS, whose renderer carries the ocean.</param>
        public OceanPreviewDriver(CoreCelestialBodyData body, PQS pqs)
        {
            _body = body;
            _pqs = pqs;
        }

        /// <inheritdoc />
        public bool Enabled { get; set; } = true;

        /// <inheritdoc />
        public bool Booted { get; private set; }

        /// <inheritdoc />
        public string Status { get; private set; } = "not attached";

        /// <summary>
        /// Gets the water depth the active preview's ocean renders, which the atmosphere and clouds read to sit behind the
        /// water rather than over it, or null when no ocean is drawing.
        /// </summary>
        public static RenderTexture ActiveWaterDepth
        {
            get
            {
                OceanPreviewDriver driver = PlanetAuthoringSession.Active?.OceanDriver;
                WaterManager manager = WaterManager.Instance();
                return driver is { Booted: true, Enabled: true } && manager is { IsEditorHosted: true }
                    ? manager.WaterDepthTexture
                    : null;
            }
        }

        /// <inheritdoc />
        public bool Attach()
        {
            if (Booted)
                return true;

            PQSRenderer renderer = _pqs != null ? _pqs.PQSRenderer : null;
            if (renderer == null || _body == null || _body.Data == null || !_body.Data.hasOcean)
            {
                Status = "no ocean on this body";
                return true;
            }

            if (renderer._oceanSpectrum == null || (renderer.OceanWaterMaterial == null && renderer.OceanLavaMaterial == null))
            {
                Status = "Has Ocean is on, but the PQS renderer has no spectrum or material. Use Add Ocean.";
                return true;
            }

            _environment = new OceanPreviewEnvironment();
            WaterManager.CreateForEditor(_environment);
            if (!WaterManager.Instance().IsAssetLoaded())
            {
                Status = "The ocean meshes did not load. Run 'ThunderKit > Import Ksp2 To Editor'.";
                TearDown(renderer);
                return false;
            }

            if (!renderer.SetupOceanForEditor())
            {
                Status = "The ocean did not start. Check that its material is on the ocean shader or its SDK stand-in.";
                TearDown(renderer);
                return false;
            }

            Booted = true;
            _consecutiveFailures = 0;
            _lastTickTime = EditorApplication.timeSinceStartup;
            _assetVersion = AssetVersion(renderer);
            Status = string.Empty;
            return true;
        }

        /// <inheritdoc />
        public void Detach()
        {
            if (!Booted)
                return;

            TearDown(_pqs != null ? _pqs.PQSRenderer : null);
            Booted = false;
            Status = "not attached";
        }

        /// <inheritdoc />
        public void Pump(Camera camera)
        {
            if (!Booted || camera == null)
                return;

            // The manager's command buffers stay on the camera and replay their last frame until they come off.
            if (!Enabled)
            {
                WaterManager.ReleaseCameraForEditor();
                return;
            }

            try
            {
                double now = EditorApplication.timeSinceStartup;
                RestartOnEdit(now);
                _environment.Camera = camera;
                _environment.DeltaTime = Mathf.Clamp((float)(now - _lastTickTime), 0f, MAX_FRAME_SECONDS);
                _lastTickTime = now;
                WaterManager.TickForEditor();
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;
                Debug.LogError(
                    $"[OceanPreview] Pump failed on '{_pqs.name}' ({_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}): {e}"
                );
                if (_consecutiveFailures < MAX_CONSECUTIVE_FAILURES)
                    return;

                WaterManager.ReleaseCameraForEditor();
                Enabled = false;
                Status = "disabled after repeated failures";
            }
        }

        // The renderer reads the spectrum into wave data and draws with a copy of the material, both made when the ocean
        // starts, so an edit to either shows only once the ocean restarts.
        private void RestartOnEdit(double now)
        {
            PQSRenderer renderer = _pqs.PQSRenderer;
            int version = AssetVersion(renderer);
            if (version == _assetVersion || now - _lastRestartTime < MIN_RESTART_SECONDS)
                return;

            _assetVersion = version;
            _lastRestartTime = now;
            renderer.ShutdownOceanForEditor();
            Status = renderer.SetupOceanForEditor()
                ? string.Empty
                : "The ocean did not restart after an edit. Check that its material is on the ocean shader or its SDK stand-in.";
        }

        // Changes whenever the renderer is given other ocean assets or one of them is edited.
        private static int AssetVersion(PQSRenderer renderer)
        {
            if (renderer == null)
                return 0;

            return HashCode.Combine(
                renderer._oceanSpectrum,
                renderer.OceanWaterMaterial,
                renderer.OceanLavaMaterial,
                renderer._oceanSpectrum != null ? EditorUtility.GetDirtyCount(renderer._oceanSpectrum) : 0,
                renderer.OceanWaterMaterial != null ? EditorUtility.GetDirtyCount(renderer.OceanWaterMaterial) : 0,
                renderer.OceanLavaMaterial != null ? EditorUtility.GetDirtyCount(renderer.OceanLavaMaterial) : 0
            );
        }

        // The renderer lets go of the manager's callbacks before the manager goes.
        private void TearDown(PQSRenderer renderer)
        {
            if (renderer != null)
            {
                renderer.ShutdownOceanForEditor();
            }

            WaterManager.DestroyForEditor();
            _environment = null;
        }
    }
}
