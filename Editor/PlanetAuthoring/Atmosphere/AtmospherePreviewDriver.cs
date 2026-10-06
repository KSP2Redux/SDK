using System;
using KSP;
using KSP.Rendering;
using KSP.Rendering.Planets;
using KSP.Rendering.Utility;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// Draws a body's Bruneton atmosphere in the SceneView during a planet preview, through the same
    /// post shader and binding the game uses.
    /// </summary>
    /// <remarks>
    /// The model comes from the scaled prefab's <see cref="AtmosphereDataModelComponent" />, resolved
    /// through its addressable key in the project's own Addressables settings. Its lookup tables are
    /// computed on the GPU at attach and again whenever a table input changes, and handed to the
    /// model as realtime textures, so baked tables on disk are neither needed nor touched.
    ///
    /// Solid bodies only. The scaled shells sit inside the metre-scale PQS in the authoring scene,
    /// so the post effect is what shows the atmosphere from every altitude.
    /// </remarks>
    public class AtmospherePreviewDriver : IPreviewDriver
    {
        private const string POST_SHADER_NAME = "Hidden/KSP2_Atmosphere/AtmospherePost";
        private const int MAX_CONSECUTIVE_FAILURES = 3;

        // A full rebake costs about 100 ms of GPU time, so a field being dragged waits until it has
        // held still this long.
        private const double REBAKE_SETTLE_SECONDS = 0.3;

        private readonly CoreCelestialBodyData _body;
        private readonly PQS _pqs;

        private AtmosphereModel _model;
        private Material _material;
        private Mesh _quad;
        private int _bakedHash;
        private int _pendingHash;
        private double _pendingSince;
        private int _consecutiveFailures;

        /// <summary>
        /// Creates a driver for the atmosphere of <paramref name="body" />.
        /// </summary>
        /// <param name="body">The previewed body, on the scaled prefab that carries the atmosphere component.</param>
        /// <param name="pqs">The previewed body's PQS, whose position is the planet center.</param>
        public AtmospherePreviewDriver(CoreCelestialBodyData body, PQS pqs)
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
        /// Gets the model being previewed, or <c>null</c> when nothing is booted.
        /// </summary>
        public AtmosphereModel Model => Booted ? _model : null;

        /// <inheritdoc />
        public bool Attach()
        {
            if (Booted)
                return true;

            if (_body == null || !_body.TryGetComponent(out AtmosphereDataModelComponent component))
            {
                Status = "no atmosphere on this body";
                return true;
            }

            _model = AtmosphereSetup.FindModel(component.AtmosphereModelKey, out string problem);
            if (_model == null)
            {
                Status = problem;
                return true;
            }

            Shader shader = Shader.Find(POST_SHADER_NAME);
            if (shader == null)
            {
                Status = $"{POST_SHADER_NAME} not found. Run 'ThunderKit > Import Ksp2 To Editor'.";
                return false;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _quad = AtmosphereScatterManager.CreateFullscreenQuadMesh();
            _quad.hideFlags = HideFlags.HideAndDontSave;
            if (!Rebake())
            {
                ReleaseResources();
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

            ReleaseResources();
            Booted = false;
            Status = "not attached";
        }

        /// <inheritdoc />
        public void Pump(Camera camera)
        {
            if (!Enabled || !Booted || camera == null || _model == null || _pqs == null)
                return;

            try
            {
                RebakeWhenSettled();
                Light sun = SunCoupling.CurrentSun;
                Vector3 sunDirection = sun != null ? -sun.transform.forward : Vector3.up;

                // Transition 0 keeps the post effect at full strength. In game it fades out with
                // distance in favour of the scaled shells, which the authoring scene cannot show.
                AtmosphereScatterManager.BindPostAtmosphere(
                    camera,
                    _model,
                    _material,
                    _pqs.transform.position * AtmosphereConstants.LENGTH_UNIT_IN_KILOMETERS,
                    sunDirection,
                    0f,
                    _pqs.data.heightMapInfo.DitheringScale,
                    Ocean.OceanPreviewDriver.ActiveWaterDepth
                );
                AtmosphereScatterManager.DrawPostAtmosphere(camera, _material, _quad);
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;
                Debug.LogError(
                    $"[AtmospherePreview] Pump failed on '{_body.name}' "
                    + $"({_consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}): {e}"
                );
                if (_consecutiveFailures < MAX_CONSECUTIVE_FAILURES)
                    return;

                Enabled = false;
                Status = "disabled after repeated failures";
            }
        }

        // Edits arrive through the inspector, which does not repaint the SceneView, so a pending
        // rebake keeps the view repainting until the inputs settle.
        private void RebakeWhenSettled()
        {
            int hash = BrunetonLutBaker.ComputeLutInputHash(_model);
            if (hash == _bakedHash)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (hash != _pendingHash)
            {
                _pendingHash = hash;
                _pendingSince = now;
            }

            if (now - _pendingSince < REBAKE_SETTLE_SECONDS)
            {
                SceneView.RepaintAll();
                return;
            }

            Rebake();
        }

        private bool Rebake()
        {
            // Recorded up front so a failed bake is not retried every settle until the inputs change.
            _bakedHash = BrunetonLutBaker.ComputeLutInputHash(_model);
            _pendingHash = _bakedHash;
            if (!BrunetonLutBaker.BakeRealtime(
                    _model,
                    out RenderTexture transmittance,
                    out RenderTexture irradiance,
                    out RenderTexture scattering,
                    out string error
                ))
            {
                Status = $"LUT bake failed: {error}";
                Debug.LogError($"[AtmospherePreview] {Status}");
                return false;
            }

            _model.SetRealtimeTextures(transmittance, irradiance, scattering);
            Status = string.Empty;
            return true;
        }

        private void ReleaseResources()
        {
            // Hands the model back its baked tables, so nothing realtime is left for the game or a
            // later bake to find.
            if (_model != null)
            {
                _model.ClearRealtimeTextures();
            }

            if (_material != null)
            {
                Object.DestroyImmediate(_material);
            }

            if (_quad != null)
            {
                Object.DestroyImmediate(_quad);
            }

            _material = null;
            _quad = null;
            _bakedHash = 0;
            _pendingHash = 0;
        }
    }
}
