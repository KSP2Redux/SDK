using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Redux;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Lifecycle owner for editor-mode planet preview. One active session at a time. Hosts the PQS
    /// update tick, scene-view binding, and domain-reload teardown.
    /// </summary>
    public sealed class PlanetAuthoringSession
    {
        /// <summary>
        /// Classification of the body for preview purposes. Selects between PQS-driven solid-surface
        /// preview and the scaled-space-only path used by gas giants and stars.
        /// </summary>
        public enum BodyClass
        {
            /// <summary>Unable to classify the body (missing or invalid CoreCelestialBodyData).</summary>
            Unknown,
            /// <summary>Solid-surface body. Driven by the PQS pipeline.</summary>
            SolidSurface,
            /// <summary>Gas giant. Scaled-space prefab only, no PQS.</summary>
            GasGiant,
            /// <summary>Star. Scaled-space prefab only, no PQS.</summary>
            Star,
        }

        /// <summary>
        /// Result of <see cref="CheckReadiness" />. Carries the body classification and any reasons the
        /// preview cannot start.
        /// </summary>
        public readonly struct ReadinessReport
        {
            /// <summary>
            /// Creates a readiness report.
            /// </summary>
            /// <param name="bodyClass">The body classification.</param>
            /// <param name="errors">Reasons preview cannot start. Empty when ready.</param>
            public ReadinessReport(BodyClass bodyClass, IReadOnlyList<string> errors)
            {
                Class = bodyClass;
                Errors = errors;
            }

            /// <summary>Gets the body classification.</summary>
            public BodyClass Class { get; }
            /// <summary>Gets the reasons preview cannot start. Empty when ready.</summary>
            public IReadOnlyList<string> Errors { get; }
            /// <summary>Gets a value indicating whether preview can start.</summary>
            public bool IsReady => Errors.Count == 0;
        }

        // 30 FPS.
        private const double TickIntervalSeconds = 1.0 / 30.0;

        /// <summary>
        /// Gets the currently-active session, or null if no preview is running.
        /// </summary>
        public static PlanetAuthoringSession Active { get; private set; }

        /// <summary>Gets the body this session is previewing.</summary>
        public CoreCelestialBodyData Body { get; }
        /// <summary>Gets the PQS component being driven, or null for non-solid bodies.</summary>
        public PQS Pqs { get; }
        /// <summary>Gets the body classification.</summary>
        public BodyClass Class { get; }
        /// <summary>Gets a value indicating whether the session is currently active.</summary>
        public bool IsAlive { get; private set; }
        /// <summary>Gets the SceneView camera driver bound to the PQS, or null for non-solid bodies.</summary>
        public PreviewCameraDriver CameraDriver { get; }

        private double _lastTickTime;
        private bool _hasSnapshot;

        // Whitelist of PQS fields the boot harness writes. RevertPropertyOverride at End clears the
        // resulting prefab-instance modifications. User edits to other fields are untouched.
        private static readonly string[] BootHarnessPqsFields =
        {
            "settings",
            "isAlive",
            "isStarted",
            "isSubdivisionEnabled",
            "collapseDelta",
            "visRadDelta",
            "visRad",
            "radiusSquared",
            "circumference",
            "radiusMax",
            "radiusMin",
            "halfChord",
            "quadAllowBuild",
            "parentSphere",
            "primaryTargetDistance",
            "primaryTargetAltitude",
        };

        private static readonly string[] BootHarnessPqsRendererFields =
        {
            "Pqs",
            "SourceCamera",
            "CreateColliders",
        };

        private static void RevertBootHarnessOverrides(PQS pqs)
        {
            if (pqs == null)
                return;
            if (PrefabUtility.IsPartOfPrefabInstance(pqs))
                RevertFieldOverrides(pqs, BootHarnessPqsFields);
            if (pqs.PQSRenderer != null && PrefabUtility.IsPartOfPrefabInstance(pqs.PQSRenderer))
                RevertFieldOverrides(pqs.PQSRenderer, BootHarnessPqsRendererFields);
        }

        private static void RevertFieldOverrides(UnityEngine.Object component, string[] fields)
        {
            var so = new SerializedObject(component);
            foreach (string field in fields)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop != null)
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
            }
        }

        private PlanetAuthoringSession(CoreCelestialBodyData body, PQS pqs, BodyClass bodyClass)
        {
            Body = body;
            Pqs = pqs;
            Class = bodyClass;
            CameraDriver = pqs != null ? new PreviewCameraDriver(pqs) : null;
        }

        /// <summary>
        /// Reports whether <paramref name="body" /> can start a preview session. Validates the body
        /// hierarchy, PQSData wiring, and editor-mode availability of the global PQS settings.
        /// </summary>
        /// <param name="body">The body to check.</param>
        /// <returns>The readiness report.</returns>
        public static ReadinessReport CheckReadiness(CoreCelestialBodyData body)
        {
            var errors = new List<string>();

            if (body == null)
            {
                errors.Add("No CoreCelestialBodyData provided.");
                return new ReadinessReport(BodyClass.Unknown, errors);
            }

            BodyClass bodyClass = ClassifyBody(body);

            if (bodyClass == BodyClass.SolidSurface)
            {
                var pqs = body.GetComponentInChildren<PQS>(true);
                if (pqs == null)
                {
                    errors.Add("No PQS component found in the body hierarchy.");
                }
                else
                {
                    if (pqs.data == null)
                    {
                        errors.Add("PQS has no PQSData asset assigned.");
                    }
                    else
                    {
                        if (pqs.data.materialSettings == null || pqs.data.materialSettings.surfaceMaterial == null)
                            errors.Add("PQSData has no surface material assigned.");
                        if (pqs.data.heightMapInfo == null || pqs.data.heightMapInfo.globalHeightMap == null)
                            errors.Add("PQSData has no global height map assigned.");
                        if (pqs.data.heightMapInfo != null && pqs.data.heightMapInfo.mask == null)
                            errors.Add("PQSData has no biome mask assigned.");
                    }
                }

                if (pqs != null && pqs.settings == null && PlanetAuthoringServices.PQSGlobalSettings == null)
                    errors.Add("PQSGlobalSettings unavailable. Run 'ThunderKit > Import Ksp2 To Editor' so the base-game catalog is registered.");
            }
            // Gas giants and stars use the scaled-space prefab path, no PQS to validate.

            return new ReadinessReport(bodyClass, errors);
        }

        /// <summary>
        /// Starts a preview session for <paramref name="body" />. Tears down any active session, validates
        /// readiness, boots the PQS pipeline against the SceneView camera, and subscribes to the per-frame
        /// render hook that drives subdivision and drawing.
        /// </summary>
        /// <param name="body">The body to preview. The scene-instance CoreCelestialBodyData, not the prefab asset.</param>
        /// <returns>The active session, or null if readiness failed or boot threw.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="body" /> is null.</exception>
        public static PlanetAuthoringSession Begin(CoreCelestialBodyData body)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (Active != null)
                Active.End();

            // Install Redux's Resources override and load ksp2.catalog so base-game Resources.Load
            // resolves outside of play mode. Idempotent.
            ReduxAssets.ConfigureEditorAssets();

            ReadinessReport report = CheckReadiness(body);
            if (!report.IsReady)
            {
                EditorUtility.DisplayDialog(
                    "Cannot start planet preview",
                    $"Preview is unavailable for '{body.name}':\n\n- " + string.Join("\n- ", report.Errors),
                    "OK"
                );
                return null;
            }

            PQS pqs = report.Class == BodyClass.SolidSurface
                ? body.GetComponentInChildren<PQS>(true)
                : null;

            bool hasSnapshot = false;
            if (pqs != null)
            {
                // Clear stale overrides from any previous run that crashed before End. Without this a leaked
                // CreateColliders=false override skips the collider native containers and the boot NREs.
                RevertBootHarnessOverrides(pqs);
                hasSnapshot = true;
                if (pqs.settings == null)
                    pqs.settings = PlanetAuthoringServices.PQSGlobalSettings;
                pqs.SetCoreCelestialBodyData(body);
                if (pqs.PQSRenderer != null)
                {
                    if (pqs.PQSRenderer.Pqs == null)
                        pqs.PQSRenderer.Pqs = pqs;
                    // CreateUpdateListsJob.ActiveColliderMap is read regardless of this flag, so the
                    // collider native containers must be allocated for the job to schedule.
                    pqs.PQSRenderer.CreateColliders = true;
                    pqs.PQSRenderer.BootForEditor();
                    if (pqs.PQSRenderer.PqsDecalController == null)
                        pqs.PQSRenderer.PqsDecalController = pqs.PQSRenderer.GetComponent<PQSDecalController>();

                    // GraphicsManager fires this from a cameraPQSChanged message at runtime, no GraphicsManager
                    // in edit mode. Boot's UpdateQuadsInit reads the NativeLists CreateComputeBuffers allocates.
                    pqs.PQSRenderer.InitResources();
                }

                // Bind a camera target before BootForEditor so the initial UpdateQuads runs with
                // HasPrimaryTarget true and its TempJob allocations dispose normally.
                SceneView sv = SceneView.lastActiveSceneView;
                if (sv != null && sv.camera != null)
                {
                    pqs.SetTarget(new PQSTargetProvider(pqs.transform, sv.camera));
                    if (pqs.PQSRenderer != null)
                        pqs.PQSRenderer.SourceCamera = sv.camera;
                    // SceneView cameras default to enabled=false. DrawPQSQuads gates on Camera.isActiveAndEnabled.
                    // CameraDriver.Bind handles this on Tick, but the first BootForEditor draw runs before Tick.
                    sv.camera.enabled = true;
                }
            }

            if (pqs != null && !pqs.BootForEditor())
            {
                RevertBootHarnessOverrides(pqs);
                EditorUtility.DisplayDialog(
                    "Cannot start planet preview",
                    $"PQS failed to initialize on '{body.name}'. Check the console for details.",
                    "OK"
                );
                return null;
            }

            var session = new PlanetAuthoringSession(body, pqs, report.Class)
            {
                _hasSnapshot = hasSnapshot,
            };
            session.Subscribe();
            session.IsAlive = true;
            Active = session;
            return session;
        }

        /// <summary>
        /// Stops the session. Tears down render hooks, releases the SceneView binding, deactivates the
        /// PQS, and reverts boot-harness prefab-instance overrides so the scene/prefab stays clean.
        /// </summary>
        public void End()
        {
            if (!IsAlive)
                return;

            Unsubscribe();
            CameraDriver?.Unbind();
            if (Pqs != null && Pqs.isActive)
                Pqs.DeactivateSphere();
            if (Pqs != null && _hasSnapshot)
            {
                RevertBootHarnessOverrides(Pqs);
                Pqs.isActive = true;
                _hasSnapshot = false;
            }
            IsAlive = false;
            if (Active == this)
                Active = null;
        }

        private void Subscribe()
        {
            EditorApplication.update += Tick;
            SceneView.duringSceneGui += OnSceneGui;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // OnRenderObject does not fire for the PQS in the editor scene context. Drive DrawPlanet
            // from onPreCull instead. onPreRender and onPostRender are both too late.
            Camera.onPreCull += OnCameraPreCull;
        }

        private void Unsubscribe()
        {
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= OnSceneGui;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            Camera.onPreCull -= OnCameraPreCull;
        }

        private void OnCameraPreCull(Camera cam)
        {
            if (!IsAlive || Pqs == null || Pqs.PQSRenderer == null)
                return;
            if (CameraDriver == null || CameraDriver.BoundCamera == null || cam != CameraDriver.BoundCamera)
                return;
            if (!Pqs.isAlive || !Pqs.isActive || !Pqs.HasPrimaryTargetForRendering)
                return;
            // Drive the per-frame pipeline from the camera callback so _drawPlanetQueued is set and
            // consumed in the same frame. A separate tick at a different rate causes flicker.
            Pqs.UpdateSurfaceMaterial();
            Pqs.UpdateSphere();
            Pqs.PQSRenderer.LateUpdateForEditor();
            Pqs.PQSRenderer.DrawPlanet();
        }

        private void Tick()
        {
            if (!IsAlive)
                return;

            // Body went away (deleted, scene closed). Tear down rather than NRE.
            if (Body == null || (Class == BodyClass.SolidSurface && Pqs == null))
            {
                End();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTickTime < TickIntervalSeconds)
                return;
            _lastTickTime = now;

            SceneView sv = SceneView.lastActiveSceneView;
            if (sv == null)
                return;

            if (CameraDriver != null)
            {
                if (CameraDriver.BoundSceneView != sv)
                    CameraDriver.Bind(sv);
                else
                    CameraDriver.Sync();

                // PQS pipeline runs from OnCameraPreCull. Tick only handles bind and repaint requests.
                if (CameraDriver.CameraMoved)
                    sv.Repaint();
            }
        }

        private void OnSceneGui(SceneView sv)
        {
            if (!IsAlive)
                return;

            // Hook for overlay rendering and scene-view handles.
        }

        private void OnBeforeAssemblyReload()
        {
            // Release native resources before domain reload drops static fields.
            End();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Catches the no-domain-reload case. Default play-mode entry already tears the session down.
            if (change == PlayModeStateChange.ExitingEditMode || change == PlayModeStateChange.EnteredPlayMode)
                End();
        }

        private static BodyClass ClassifyBody(CoreCelestialBodyData body)
        {
            var data = body.Core?.data;
            if (data == null)
                return BodyClass.Unknown;
            if (data.isStar)
                return BodyClass.Star;
            if (!data.hasSolidSurface)
                return BodyClass.GasGiant;
            return BodyClass.SolidSurface;
        }
    }
}
