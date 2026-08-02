using System;
using AwesomeTechnologies.VegetationStudio;
using AwesomeTechnologies.VegetationSystem;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Uber.Scatter;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Runs the Vegetation Studio scatter pipeline in edit mode against a previewed body, so authored
    /// scatter renders in the SceneView through the same spawn path the game uses.
    /// </summary>
    /// <remarks>
    /// Owned by <see cref="PlanetAuthoringSession" /> and pumped from the session's own render hook,
    /// so the driver's lifetime is exactly the session's. A driver holding its own
    /// <see cref="Camera.onPreCull" /> subscription outlives the session across a domain reload and
    /// then ticks against a torn-down PQSDecalController, throwing once per frame and burying the
    /// console.
    ///
    /// Nothing here is reachable from the EditMode suite, which runs headless with no scene. Boot
    /// ordering and teardown ordering are verified by running a preview.
    /// </remarks>
    public sealed class ScatterPreviewDriver
    {
        /// <summary>
        /// Consecutive pump failures tolerated before the driver switches itself off.
        /// </summary>
        /// <remarks>
        /// A driver that throws every frame produces thousands of identical entries and hides whatever
        /// broke first. Stopping loudly is more useful than continuing quietly.
        /// </remarks>
        private const int MaxConsecutiveFailures = 3;

        private readonly PQS _pqs;

        private VegetationSystemPro _system;
        private PqsTerrain _terrain;
        private QualityManager _ownedQualityManager;
        private int _consecutiveFailures;

        /// <summary>
        /// Creates a driver for the scatter system belonging to <paramref name="pqs" />.
        /// </summary>
        /// <param name="pqs">The previewed body's PQS. The scatter system and terrain bridge are looked up from it.</param>
        public ScatterPreviewDriver(PQS pqs)
        {
            _pqs = pqs;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the driver pumps the scatter system.
        /// </summary>
        /// <remarks>
        /// Turning this off leaves the system booted but idle, so toggling it back on does not pay
        /// another boot. Bodies with no scatter system are unaffected either way.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets a value indicating whether the scatter system has been booted by this driver.
        /// </summary>
        public bool Booted { get; private set; }

        /// <summary>
        /// Gets the scatter system being driven, or <c>null</c> when the body has none.
        /// </summary>
        public VegetationSystemPro System => _system;

        /// <summary>
        /// Gets the terrain bridge the scatter system samples, or <c>null</c> when the body has none.
        /// </summary>
        public PqsTerrain Terrain => _terrain;

        /// <summary>
        /// Gets the quality manager the preview is running at, or <c>null</c> when nothing is booted.
        /// </summary>
        /// <remarks>
        /// Worth having a preview-side control over, because the density multiplier is type selective
        /// and scales the sample grid rather than a spawn chance. A quality level of LOW quarters the
        /// grass, plants and objects while leaving trees and boulders untouched, so an author tuning at
        /// HIGH has no way to see what most players will get.
        /// </remarks>
        public QualityManager QualityManager =>
            Booted && _system != null ? _system.GetQualityManager() : null;

        /// <summary>
        /// Applies a density quality preset to the running preview.
        /// </summary>
        /// <remarks>
        /// Disposing the cells is what makes the change visible. Density feeds the sample grid at spawn
        /// time, so already-spawned cells keep their old spacing until they are rebuilt, and
        /// <c>SetDensityQualityLevel</c> handles that itself.
        /// </remarks>
        /// <param name="index">Index into the manager's density presets.</param>
        public void SetDensityQualityLevel(int index)
        {
            QualityManager manager = QualityManager;
            if (manager == null || index < 0 || index >= manager.DensityQualityLevelList.Count)
                return;

            manager.SetDensityQualityLevel(index);
        }

        /// <summary>
        /// Applies a draw distance quality preset to the running preview.
        /// </summary>
        /// <param name="index">Index into the manager's draw distance presets.</param>
        public void SetDrawDistanceQualityLevel(int index)
        {
            QualityManager manager = QualityManager;
            if (manager == null || index < 0 || index >= manager.DrawDistanceQualityLevelList.Count)
                return;

            manager.SetDrawDistanceQualityLevel(index);
            _system.SetVegetationStudioCamerasDirty();
        }

        /// <summary>
        /// Gets a short human-readable account of anything wrong with the driver, or an empty string
        /// when it is running normally.
        /// </summary>
        /// <remarks>
        /// Only the cases an author can act on produce text.
        /// </remarks>
        public string Status { get; private set; } = "not attached";

        /// <summary>
        /// Resolves the body's scatter system and boots it, terrain bridge first.
        /// </summary>
        /// <remarks>
        /// Safe to call on a body with no scatter system, which is the common case. That reports
        /// success with nothing booted rather than failing, because "this body has no scatter" is not
        /// an error.
        ///
        /// Boot order is load bearing. The scatter spawner samples the terrain bridge on its first
        /// tick, so <see cref="PqsTerrain" /> has to own its compute resources before
        /// <see cref="VegetationSystemPro" /> starts.
        /// </remarks>
        /// <returns>
        /// True if the body booted cleanly or has no scatter system to boot, false otherwise.
        /// </returns>
        public bool Attach()
        {
            if (Booted)
                return true;

            if (!Resolve())
            {
                Status = "no scatter system on this body";
                return true;
            }

            try
            {
                if (_terrain != null)
                {
                    _terrain.BootForEditor();
                }

                ClaimQualityManager();
                _system.BootForEditor();
            }
            catch (Exception e)
            {
                // Leave nothing half booted. A partially initialized system leaks native containers
                // and the next Attach would find Booted false but resources already allocated.
                Debug.LogError($"[ScatterPreview] Boot failed on '{_pqs.name}', tearing back down: {e}");
                SafeShutdown();
                Status = "boot failed, see console";
                return false;
            }

            Booted = true;
            _consecutiveFailures = 0;
            Status = string.Empty;
            return true;
        }

        /// <summary>
        /// Shuts the scatter system down, scatter first and terrain bridge second.
        /// </summary>
        /// <remarks>
        /// Must run before the previewed sphere is deactivated. The scatter system holds compute
        /// buffers built from the terrain bridge, and the bridge's resources are derived from a live
        /// PQS, so tearing down in the other order disposes what the scatter side is still holding.
        ///
        /// Idempotent, because the session ends from several paths including domain reload and play
        /// mode entry.
        /// </remarks>
        public void Detach()
        {
            if (!Booted)
                return;

            SafeShutdown();
            Booted = false;
            Status = "not attached";
        }

        /// <summary>
        /// Advances the scatter system for the frame <paramref name="camera" /> is about to render.
        /// </summary>
        /// <remarks>
        /// Must be called from a render hook, not from an editor tick.
        /// <c>Graphics.DrawMeshInstancedIndirect</c> submits per frame and only takes effect inside
        /// the render loop, so pumping from <c>EditorApplication.update</c> issues the draw where
        /// nothing consumes it and the field silently never appears. This is the same constraint that
        /// makes the session drive the PQS from onPreCull.
        /// </remarks>
        /// <param name="camera">The camera about to cull, which becomes the scatter system's view.</param>
        public void Pump(Camera camera)
        {
            if (!Enabled || !Booted || camera == null || _system == null)
                return;

            if (!_system.InitDone)
                return;

            BindSceneViewCamera(camera);

            try
            {
                _system.UpdateForEditor();
                _system.LateUpdateForEditor();
                _consecutiveFailures = 0;
            }
            catch (Exception e)
            {
                _consecutiveFailures++;
                Debug.LogError(
                    $"[ScatterPreview] Pump failed on '{_pqs.name}' "
                    + $"({_consecutiveFailures}/{MaxConsecutiveFailures}): {e}"
                );

                if (_consecutiveFailures >= MaxConsecutiveFailures)
                {
                    Enabled = false;
                    Status = "disabled after repeated failures";
                    Debug.LogWarning(
                        $"[ScatterPreview] Disabling the scatter preview on '{_pqs.name}' after "
                        + $"{MaxConsecutiveFailures} consecutive failures. Re-enable it from Preview Controls."
                    );
                }
            }
        }

        /// <summary>
        /// Finds the scatter system and terrain bridge belonging to the previewed body.
        /// </summary>
        /// <returns>True if a scatter system was found, false otherwise.</returns>
        private bool Resolve()
        {
            if (_pqs == null)
                return false;

            // Explicit null checks rather than the null-coalescing operator, which bypasses Unity's
            // overloaded equality and so does not see a destroyed object as null.
            if (_system == null)
            {
                _system = ScatterSystemLocator.Find(_pqs);
            }

            if (_terrain == null)
            {
                _terrain = ScatterSystemLocator.FindTerrain(_pqs);
            }

            return _system != null;
        }

        /// <summary>
        /// Points the scatter system's SceneView camera at <paramref name="camera" />.
        /// </summary>
        /// <remarks>
        /// Done every pump rather than once at boot. The SceneView camera is destroyed and recreated
        /// by layout changes, and the fork's own camera assignment hook is an empty stub, so a cached
        /// reference goes stale with no signal. Culling has to run against the same view the draw
        /// targets or the visible set belongs to a camera that no longer exists.
        /// </remarks>
        /// <param name="camera">The camera to bind.</param>
        private void BindSceneViewCamera(Camera camera)
        {
            foreach (VegetationStudioCamera studioCamera in _system.VegetationStudioCameraList)
            {
                if (studioCamera.VegetationStudioCameraType == VegetationStudioCameraType.SceneView)
                {
                    studioCamera.SelectedCamera = camera;
                }
            }
        }

        /// <summary>
        /// Makes sure a <see cref="QualityManager" /> exists before boot, and that it will not be saved.
        /// </summary>
        /// <remarks>
        /// <c>LoadSettingsFromQualityManager</c> adds one during boot if the system has none, with
        /// default hide flags and no matching removal in <c>ShutdownForEditor</c>, which leaves a real
        /// component on the scatter system to be written into the scene and gives the quality dropdown
        /// a stray component to write its selected preset into.
        ///
        /// Creating it here means the fork's <c>GetComponent</c> finds one and never adds its own. A
        /// manager the author placed deliberately is left alone and not destroyed on teardown.
        /// </remarks>
        private void ClaimQualityManager()
        {
            if (_system == null || _system.GetComponent<QualityManager>() != null)
                return;

            _ownedQualityManager = _system.gameObject.AddComponent<QualityManager>();

            // Matches SurfacePrefabPreviewSync, the suite's existing idiom for transient editor
            // objects. Hidden as well as unsaved, because a component that appears mid-session and
            // vanishes afterwards is more confusing in the inspector than absent.
            _ownedQualityManager.hideFlags = HideFlags.HideAndDontSave;
        }

        private void ReleaseQualityManager()
        {
            if (_ownedQualityManager == null)
                return;

            UnityEngine.Object.DestroyImmediate(_ownedQualityManager);
            _ownedQualityManager = null;
        }

        /// <summary>
        /// Tears down whatever is booted without throwing, in the reverse of boot order.
        /// </summary>
        private void SafeShutdown()
        {
            try
            {
                if (_system != null)
                {
                    _system.ShutdownForEditor();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScatterPreview] Scatter shutdown threw on '{_pqs.name}': {e}");
            }

            try
            {
                if (_terrain != null)
                {
                    _terrain.ShutdownForEditor();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScatterPreview] Terrain shutdown threw on '{_pqs.name}': {e}");
            }

            // Last, because the system holds a reference to it through VegetationSettings and reads it
            // while shutting down.
            ReleaseQualityManager();
        }
    }
}
