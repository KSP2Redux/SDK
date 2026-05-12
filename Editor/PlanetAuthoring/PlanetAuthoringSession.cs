using System;
using System.Collections.Generic;
using System.Linq;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Redux;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Lifecycle owner for editor-mode planet preview.
    /// </summary>
    /// <remarks>
    /// One active session at a time. Hosts the PQS update tick, scene-view binding, and domain-reload teardown.
    /// </remarks>
    public sealed class PlanetAuthoringSession
    {
        /// <summary>
        /// Classification of the body for preview purposes.
        /// </summary>
        /// <remarks>
        /// Selects between PQS-driven solid-surface preview and the scaled-space-only path used by gas giants and stars.
        /// </remarks>
        public enum BodyClass
        {
            /// <summary>Unable to classify the body (missing or invalid CoreCelestialBodyData).</summary>
            Unknown,
            /// <summary>Solid-surface body, driven by the PQS pipeline.</summary>
            SolidSurface,
            /// <summary>Gas giant, scaled-space prefab only with no PQS.</summary>
            GasGiant,
            /// <summary>Star, scaled-space prefab only with no PQS.</summary>
            Star,
        }

        /// <summary>
        /// Identifies a specific readiness failure so UI can match on identity instead of error message text.
        /// </summary>
        /// <remarks>
        /// Add new entries as new readiness checks land.
        /// </remarks>
        public enum ReadinessErrorCode
        {
            /// <summary>The body itself is missing.</summary>
            NoBody,
            /// <summary>Solid-surface body has no PQS component in its hierarchy.</summary>
            NoPqsComponent,
            /// <summary>PQS has no PQSData asset assigned.</summary>
            NoPqsData,
            /// <summary>PQSData has no surface material assigned.</summary>
            NoSurfaceMaterial,
            /// <summary>PQSData has no global height map assigned.</summary>
            NoGlobalHeightMap,
            /// <summary>PQSData has no biome mask assigned.</summary>
            NoBiomeMask,
            /// <summary>The shipped PQSGlobalSettings asset is unavailable.</summary>
            PqsGlobalSettingsUnavailable,
        }

        /// <summary>
        /// One readiness failure.
        /// </summary>
        /// <remarks>
        /// Code is for UI matching. Message is for display.
        /// </remarks>
        public readonly struct ReadinessError
        {
            /// <summary>
            /// Creates a readiness error with the given code and display message.
            /// </summary>
            /// <param name="code">The error code.</param>
            /// <param name="message">The display message.</param>
            public ReadinessError(ReadinessErrorCode code, string message)
            {
                Code = code;
                Message = message;
            }

            /// <summary>
            /// Gets the readiness error code.
            /// </summary>
            public ReadinessErrorCode Code { get; }

            /// <summary>
            /// Gets the display message describing the error.
            /// </summary>
            public string Message { get; }
        }

        /// <summary>
        /// Result of <see cref="CheckReadiness" />.
        /// </summary>
        /// <remarks>
        /// Carries the body classification and any reasons the preview cannot start.
        /// </remarks>
        public readonly struct ReadinessReport
        {
            /// <summary>
            /// Creates a readiness report with the given classification and error list.
            /// </summary>
            /// <param name="bodyClass">The body classification.</param>
            /// <param name="errors">The readiness errors. Empty when ready.</param>
            public ReadinessReport(BodyClass bodyClass, IReadOnlyList<ReadinessError> errors)
            {
                Class = bodyClass;
                Errors = errors;
            }

            /// <summary>Gets the body classification.</summary>
            public BodyClass Class { get; }
            /// <summary>Gets the reasons preview cannot start, or an empty list when ready.</summary>
            public IReadOnlyList<ReadinessError> Errors { get; }
            /// <summary>Gets a value indicating whether preview can start.</summary>
            public bool IsReady => Errors.Count == 0;
        }

        // Throttle for Tick's bind/sync work only. The render pipeline runs unthrottled from
        // OnCameraPreCull, so this doesn't gate visible drawing.
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
        /// <summary>Gets the per-session preview state readout, or null for non-solid bodies.</summary>
        public PlanetPreviewState PreviewState { get; }

        private double _lastTickTime;
        private bool _hasSnapshot;
        private Quaternion _initialBodyRotation;

        // Whitelist of PQS fields the boot harness writes. RevertPropertyOverride at End clears
        // the resulting prefab-instance modifications, leaving user edits to other fields alone.
        // Stringly-typed because most of these fields are private. Renames silently no-op the
        // revert, so update this list whenever boot-harness mutations change.
        // Geometry-derived fields (radius math, target distance/altitude, visRad, halfChord) are
        // recomputed every Update so they are intentionally omitted.
        private static readonly string[] BootHarnessPqsFields =
        {
            "settings",
            "isAlive",
            "isStarted",
            "isSubdivisionEnabled",
            "quadAllowBuild",
            "parentSphere",
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
            foreach (var field in fields)
            {
                var prop = so.FindProperty(field);
                if (prop != null)
                {
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                }
            }
        }

        // Clears a framing-induced prefab override on the body transform's local rotation. Mirrors
        // the End() restore path so a prior crash mid-rotation doesn't leak a non-identity rotation
        // forward into TryBootPqs's _initialBodyRotation snapshot.
        private static void RevertBodyRotationOverride(CoreCelestialBodyData body)
        {
            if (body == null) return;
            if (!PrefabUtility.IsPartOfPrefabInstance(body.transform)) return;
            var so = new SerializedObject(body.transform);
            var prop = so.FindProperty("m_LocalRotation");
            if (prop != null)
            {
                PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
            }
        }

        private PlanetAuthoringSession(CoreCelestialBodyData body, PQS pqs, BodyClass bodyClass)
        {
            Body = body;
            Pqs = pqs;
            Class = bodyClass;
            CameraDriver = pqs != null ? new PreviewCameraDriver(pqs) : null;
            PreviewState = pqs != null ? new PlanetPreviewState(body, pqs) : null;
        }

        /// <summary>
        /// Reports whether <paramref name="body" /> can start a preview session.
        /// </summary>
        /// <remarks>
        /// Validates the body hierarchy, PQSData wiring, and editor-mode availability of the global PQS settings.
        /// </remarks>
        /// <param name="body">The body to check.</param>
        /// <returns>The readiness report.</returns>
        public static ReadinessReport CheckReadiness(CoreCelestialBodyData body)
        {
            var errors = new List<ReadinessError>();

            if (body == null)
            {
                errors.Add(new ReadinessError(ReadinessErrorCode.NoBody, "No CoreCelestialBodyData provided."));
                return new ReadinessReport(BodyClass.Unknown, errors);
            }

            var bodyClass = ClassifyBody(body);

            if (bodyClass == BodyClass.SolidSurface)
            {
                var pqs = body.GetComponentInChildren<PQS>(true);
                if (pqs == null)
                {
                    errors.Add(new ReadinessError(ReadinessErrorCode.NoPqsComponent, "No PQS component found in the body hierarchy."));
                }
                else
                {
                    if (pqs.data == null)
                    {
                        errors.Add(new ReadinessError(ReadinessErrorCode.NoPqsData, "PQS has no PQSData asset assigned."));
                    }
                    else
                    {
                        if (pqs.data.materialSettings == null || pqs.data.materialSettings.surfaceMaterial == null)
                            errors.Add(new ReadinessError(ReadinessErrorCode.NoSurfaceMaterial, "PQSData has no surface material assigned."));
                        if (pqs.data.heightMapInfo == null || pqs.data.heightMapInfo.globalHeightMap == null)
                            errors.Add(new ReadinessError(ReadinessErrorCode.NoGlobalHeightMap, "PQSData has no global height map assigned."));
                        if (pqs.data.heightMapInfo != null && pqs.data.heightMapInfo.mask == null)
                            errors.Add(new ReadinessError(ReadinessErrorCode.NoBiomeMask, "PQSData has no biome mask assigned."));
                    }
                }

                if (pqs != null && pqs.settings == null && EditorPqsBootstrap.PQSGlobalSettings == null)
                    errors.Add(new ReadinessError(ReadinessErrorCode.PqsGlobalSettingsUnavailable, "PQSGlobalSettings unavailable. Run 'ThunderKit > Import Ksp2 To Editor' so the base-game catalog is registered."));
            }
            // Gas giants and stars use the scaled-space prefab path, no PQS to validate.

            return new ReadinessReport(bodyClass, errors);
        }

        /// <summary>
        /// Starts a preview session for <paramref name="body" />.
        /// </summary>
        /// <remarks>
        /// Tears down any active session, validates readiness, boots the PQS pipeline against the SceneView camera, and subscribes to the per-frame render hook that drives subdivision and drawing.
        /// </remarks>
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

            var report = CheckReadiness(body);
            if (!report.IsReady)
            {
                ShowReadinessFailure(body, report);
                return null;
            }

            var pqs = report.Class == BodyClass.SolidSurface
                ? body.GetComponentInChildren<PQS>(true)
                : null;

            var session = new PlanetAuthoringSession(body, pqs, report.Class);

            if (pqs != null && !session.TryBootPqs())
            {
                ShowBootFailure(body);
                return null;
            }

            session.Subscribe();
            session.IsAlive = true;
            Active = session;
            // Notify subscribers that PlanetPreviewState.Active flipped from null to non-null.
            PlanetPreviewState.RaiseActiveChanged();
            return session;
        }

        private bool TryBootPqs()
        {
            // Clear stale overrides from any previous run that crashed before End. A leaked
            // CreateColliders=false override would skip the collider native containers and NRE.
            RevertBootHarnessOverrides(Pqs);
            _hasSnapshot = true;
            // Revert any leaked body rotation override BEFORE snapshotting so a previous crashed
            // session doesn't bake a framing-induced rotation in as the new "initial". The sun's
            // baseline lives in SunCoupling itself (captured when Preview Controls registers the
            // Light), so the session just calls ResetToBaseline on End.
            RevertBodyRotationOverride(Body);
            _initialBodyRotation = Body != null ? Body.transform.rotation : Quaternion.identity;

            if (Pqs.settings == null)
                Pqs.settings = EditorPqsBootstrap.PQSGlobalSettings;
            Pqs.SetCoreCelestialBodyData(Body);

            PrepareRendererForBoot(Pqs);
            BindInitialSceneView();
            // Initialize the decal controller's NativeArray so the first PQS subdivision job does not NRE on it.
            Tools.DecalControllerHelper.Resolve(Pqs);

            if (!Pqs.BootForEditor())
            {
                // Renderer already booted - tear it down so its NativeLists don't leak.
                Pqs.PQSRenderer?.ShutdownForEditor();
                RevertBootHarnessOverrides(Pqs);
                return false;
            }

            return true;
        }

        private static void PrepareRendererForBoot(PQS pqs)
        {
            if (pqs.PQSRenderer == null)
                return;

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

        private void BindInitialSceneView()
        {
            // Bind a camera target before BootForEditor so the initial UpdateQuads runs with
            // HasPrimaryTarget true and its TempJob allocations dispose normally.
            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                CameraDriver?.Bind(sv);
            }
        }

        private static void ShowReadinessFailure(CoreCelestialBodyData body, ReadinessReport report)
        {
            var messages = string.Join("\n- ", report.Errors.Select(e => e.Message));
            EditorUtility.DisplayDialog(
                "Cannot start planet preview",
                $"Preview is unavailable for '{body.name}':\n\n- " + messages,
                "OK"
            );
        }

        private static void ShowBootFailure(CoreCelestialBodyData body)
        {
            EditorUtility.DisplayDialog(
                "Cannot start planet preview",
                $"PQS failed to initialize on '{body.name}'. Check the console for details.",
                "OK"
            );
        }

        /// <summary>
        /// Stops the session.
        /// </summary>
        /// <remarks>
        /// Tears down render hooks, releases the SceneView binding, deactivates the PQS, and reverts boot-harness prefab-instance overrides so the scene/prefab stays clean.
        /// </remarks>
        public void End()
        {
            if (!IsAlive)
                return;

            Unsubscribe();
            CameraDriver?.Unbind();
            // Force-complete the in-flight PQS subdivision job before any path that disables the
            // decal controller. PQSDecalController.OnDisable disposes its native arrays, and the
            // subdivision job is still reading them otherwise.
            if (Pqs != null)
            {
                DecalBaker.CompletePendingSurfaceJob(Pqs);
            }
            if (Pqs != null && Pqs.isActive)
                Pqs.DeactivateSphere();
            if (Pqs != null && _hasSnapshot)
            {
                RevertBootHarnessOverrides(Pqs);
                Pqs.isActive = true;
                _hasSnapshot = false;
            }
            // Revert body rotation. If the body is a prefab instance, the rotation may have been
            // written as a prefab override during framing. RevertPropertyOverride clears that so the
            // prefab stays clean. Otherwise just write the snapshot back.
            if (Body != null)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(Body.transform))
                {
                    var so = new SerializedObject(Body.transform);
                    var prop = so.FindProperty("m_LocalRotation");
                    if (prop != null)
                    {
                        PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                    }
                }
                else
                {
                    Body.transform.rotation = _initialBodyRotation;
                }
            }
            // Sun baseline is owned by SunCoupling (captured when the Light was registered) so the
            // restore is a single call.
            SunCoupling.ResetToBaseline();
            PreviewState?.Clear();
            IsAlive = false;
            if (Active == this)
                Active = null;
            // Active just flipped to null, fire so subscribers can clear their UI markers.
            PlanetPreviewState.RaiseActiveChanged();
        }

        private void Subscribe()
        {
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // OnRenderObject does not fire for the PQS in the editor scene context. Drive DrawPlanet
            // from onPreCull instead. onPreRender and onPostRender are both too late.
            Camera.onPreCull += OnCameraPreCull;
        }

        private void Unsubscribe()
        {
            EditorApplication.update -= Tick;
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
                Debug.LogWarning("[PlanetAuthoringSession] Body or PQS went away mid-session. Ending preview.");
                End();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            if (now - _lastTickTime < TickIntervalSeconds)
                return;
            _lastTickTime = now;

            var sv = SceneView.lastActiveSceneView;
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

            if (PreviewState != null && PreviewState.Update(sv.camera))
                PlanetPreviewState.RaiseActiveChanged();
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
            return data switch
            {
                null => BodyClass.Unknown,
                { isStar: true } => BodyClass.Star,
                { hasSolidSurface: false } => BodyClass.GasGiant,
                _ => BodyClass.SolidSurface,
            };
        }
    }
}
