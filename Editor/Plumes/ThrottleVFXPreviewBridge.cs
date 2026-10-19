using System;
using System.Collections.Generic;
using System.Reflection;
using KSP.Game;
using KSP.Sim.impl;
using KSP.VFX;
using Ksp2UnityTools.Editor;
using Redux.VFX.Plume.Volumetric;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace KSP.Editor
{
    [InitializeOnLoad]
    internal static class ThrottleVFXPreviewBridge
    {
        private const BindingFlags INSTANCE = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<string> QUALITY_NAMES = new() { "Low - Mesh", "Medium - Volumetric", "High - Volumetric" };
        private static readonly Dictionary<Renderer, MaterialPropertyBlock> PropertyBlocks = new();
        private static readonly Dictionary<Object, (SerializedObject Snapshot, HashSet<string> Paths)> PreviewChanges = new();
        private static readonly Dictionary<SceneView, (bool Lighting, bool Skybox, bool Fog, bool Grid)> SceneViews = new();
        private static readonly List<ParticleSystem> PreviewParticles = new();
        private static bool _previewParticles;
        private static bool _comparePreset;
        private static bool _neutralView = true;
        private static float _previewThrottle = 1f;
        private static float _previewAtmosphere = 1f;
        private static int _previewEngineMode;
        private static int _previewQuality = Mathf.Clamp(SessionState.GetInt("Redux.PlumePreview.Quality", 2), 0, 2);
        private static ThrottleVFXManager.FXmodeEvent _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeRunning;
        private static ThrottleVFXManager _activePreviewManager;
        private static PersistentSettings _previousSettings;
        private static int _previousQuality;
        private static bool _previousVolumetricsEnabled;
        private static double _lastParticleUpdateTime;
        private static ThrottleVFXManager _resumeAfterSave;
        public static bool IsPreviewActive => _activePreviewManager != null;
        internal static int PreviewQuality => _previewQuality;
        internal static int PreviewEngineMode => _previewEngineMode;
        internal static bool ComparingPreset => _comparePreset;
        internal static ThrottleVFXManager ActiveManager => _activePreviewManager;

        internal static bool IsPreviewing(ThrottleVFXManager manager) =>
            manager != null && _activePreviewManager == manager;

        static ThrottleVFXPreviewBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorApplication.quitting += StopPreview;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            Undo.undoRedoPerformed += () =>
            {
                if (IsPreviewActive)
                    UpdatePreview();
            };
            PrefabStage.prefabSaving += root =>
            {
                if (_activePreviewManager != null && _activePreviewManager.gameObject.scene == root.scene)
                    SuspendForSave();
            };
            EditorSceneManager.sceneSaving += (scene, _) =>
            {
                if (_activePreviewManager != null && _activePreviewManager.gameObject.scene == scene)
                    SuspendForSave();
            };
            EditorSceneManager.sceneClosing += (scene, _) =>
            {
                if ((_activePreviewManager != null && _activePreviewManager.gameObject.scene == scene) ||
                    (_resumeAfterSave != null && _resumeAfterSave.gameObject.scene == scene))
                    StopPreview();
            };
            Selection.selectionChanged += () =>
            {
                ThrottleVFXManager manager = _activePreviewManager != null ? _activePreviewManager : _resumeAfterSave;
                if (manager == null)
                    return;

                GameObject selected = Selection.activeGameObject;
                if (selected == null || (!selected.transform.IsChildOf(manager.transform) &&
                    !manager.transform.IsChildOf(selected.transform)))
                    StopPreview();
            };
        }

        internal static VisualElement CreatePreviewControls(ThrottleVFXManager manager, bool showWindowButton = true)
        {
            var root = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                SDKConfiguration.BasePath + "/Assets/Windows/Plumes/PlumePreview.uxml").CloneTree();
            var enabled = root.Q<Toggle>("preview-enabled");
            var controls = root.Q<VisualElement>("preview-controls");
            var quality = root.Q<DropdownField>("preview-quality");
            quality.choices = QUALITY_NAMES;
            var mode = root.Q<SliderInt>("engine-mode");
            if (manager != null && manager.FXModeActionEvents != null)
            {
                foreach (ThrottleVFXManager.FXModeActionEvent entry in manager.FXModeActionEvents)
                {
                    if (entry != null)
                        mode.highValue = Mathf.Max(mode.highValue, entry.EngineModeIndex);
                }
            }
            var engineEvent = root.Q<EnumField>("preview-event");
            engineEvent.Init(_previewModeEvent);
            var throttle = root.Q<Slider>("preview-throttle");
            var pressure = root.Q<Slider>("preview-pressure");
            var pressureValue = root.Q<FloatField>("pressure-value");
            var particles = root.Q<Toggle>("preview-particles");
            var compare = root.Q<Toggle>("compare-preset");
            var neutral = root.Q<Toggle>("neutral-view");
            root.Q<Button>("preview-window").style.display = showWindowButton ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Button>("preview-window").clicked += () => PlumePreviewWindow.Open(manager);
            root.Q<Button>("preview-frame").clicked += () => FramePlume(manager, null);
            root.Q<Button>("preview-side").clicked += () => FramePlume(manager, 0);
            root.Q<Button>("preview-nose").clicked += () => FramePlume(manager, 1);
            root.Q<Button>("preview-tail").clicked += () => FramePlume(manager, 2);

            void RefreshControls()
            {
                bool playing = EditorApplication.isPlayingOrWillChangePlaymode;
                bool canPreview = manager != null && !playing && !EditorUtility.IsPersistent(manager);
                var status = root.Q<HelpBox>("preview-status");
                status.text = playing ? "Preview is available outside Play Mode." :
                    "Open the engine in Prefab Mode or place it in a scene to preview. Preview does not save game settings.";
                status.style.display = canPreview ? DisplayStyle.None : DisplayStyle.Flex;
                enabled.SetEnabled(canPreview);
                enabled.SetValueWithoutNotify(IsPreviewing(manager));
                controls.SetEnabled(canPreview && enabled.value);
                quality.SetValueWithoutNotify(QUALITY_NAMES[_previewQuality]);
                mode.style.display = mode.highValue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                mode.SetValueWithoutNotify(_previewEngineMode);
                engineEvent.SetValueWithoutNotify(_previewModeEvent);
                throttle.SetValueWithoutNotify(_previewThrottle);
                pressure.SetValueWithoutNotify(Mathf.Log10(Mathf.Max(0.0001f, _previewAtmosphere)));
                particles.SetValueWithoutNotify(_previewParticles);
                compare.SetValueWithoutNotify(_comparePreset);
                neutral.SetValueWithoutNotify(_neutralView);
                compare.SetEnabled(_previewQuality > 0);
                if (!pressureValue.Contains(root.focusController?.focusedElement as VisualElement))
                    pressureValue.SetValueWithoutNotify(_previewAtmosphere);
                root.Q<HelpBox>("quality-description").text = _previewQuality switch
                {
                    0 => "Low uses this engine's mesh and particle effects.",
                    1 => "Medium uses the shared volumetric appearance with cheaper shading and plume interaction. Surrounding geometry does not deflect it.",
                    _ => "High uses the same appearance with detailed shading, geometry interaction and supported volumetric smoke and vents."
                };
            }

            void Refresh()
            {
                if (IsPreviewing(manager))
                    ActivateAndRefresh(manager);
                RefreshControls();
            }

            enabled.RegisterValueChangedCallback(change =>
            {
                if (change.newValue)
                    ActivateAndRefresh(manager);
                else
                    StopPreview();
                RefreshControls();
            });
            quality.RegisterValueChangedCallback(change => { SetPreviewQuality(quality.choices.IndexOf(change.newValue)); RefreshControls(); });
            mode.RegisterValueChangedCallback(change => { SetPreviewEngineMode(change.newValue); RefreshControls(); });
            engineEvent.RegisterValueChangedCallback(change => { _previewModeEvent = (ThrottleVFXManager.FXmodeEvent)change.newValue; Refresh(); });
            throttle.RegisterValueChangedCallback(change => { _previewThrottle = change.newValue; Refresh(); });
            pressure.RegisterValueChangedCallback(change =>
            {
                _previewAtmosphere = change.newValue <= -3.99f ? 0f : Mathf.Pow(10f, change.newValue);
                Refresh();
            });
            pressureValue.RegisterValueChangedCallback(change =>
            {
                _previewAtmosphere = float.IsFinite(change.newValue) ? Mathf.Clamp(change.newValue, 0f, 100f) : 0f;
                pressureValue.SetValueWithoutNotify(_previewAtmosphere);
                Refresh();
            });
            particles.RegisterValueChangedCallback(change => { SetPreviewParticles(change.newValue); RefreshControls(); });
            compare.RegisterValueChangedCallback(change => { _comparePreset = change.newValue; Refresh(); });
            neutral.RegisterValueChangedCallback(change =>
            {
                _neutralView = change.newValue;
                RestoreSceneViews();
                if (IsPreviewActive && _neutralView)
                    ApplyNeutralView();
            });
            root.Q<Button>("preview-refresh").clicked += Refresh;
            root.Q<Button>("preview-stop").clicked += () => { StopPreview(); RefreshControls(); };
            void BindEvent(string name, ThrottleVFXManager.FXmodeEvent value)
            {
                root.Q<Button>(name).clicked += () => { _previewModeEvent = value; Refresh(); };
            }
            BindEvent("preview-running", ThrottleVFXManager.FXmodeEvent.FXModeRunning);
            BindEvent("preview-ignition", ThrottleVFXManager.FXmodeEvent.FXModeStart);
            BindEvent("preview-shutdown", ThrottleVFXManager.FXmodeEvent.FXModeStop);
            BindEvent("preview-flameout", ThrottleVFXManager.FXmodeEvent.FXModeFlameout);
            RefreshControls();
            root.schedule.Execute(RefreshControls).Every(200);
            return root;
        }

        internal static void SetPreviewQuality(int quality)
        {
            _previewQuality = Mathf.Clamp(quality, 0, 2);
            SessionState.SetInt("Redux.PlumePreview.Quality", _previewQuality);
            if (IsPreviewActive)
                UpdatePreview();
        }

        internal static void SetPreviewParticles(bool enabled)
        {
            _previewParticles = enabled;
            if (IsPreviewActive)
                UpdatePreview();
        }

        internal static void RefreshIfActive(MonoBehaviour effect)
        {
            if (IsPreviewActive && effect != null && effect.GetComponentInParent<ThrottleVFXManager>(true) == _activePreviewManager)
                UpdatePreview();
        }

        internal static void SetPreviewEngineMode(int engineMode)
        {
            _previewEngineMode = Mathf.Max(0, engineMode);
            if (IsPreviewActive)
                UpdatePreview();
        }

        public static void ActivateAndRefresh(ThrottleVFXManager manager)
        {
            if (manager == null || EditorUtility.IsPersistent(manager) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (_activePreviewManager != manager)
            {
                StopPreview();
                bool modeExists = false;
                int firstMode = 0;
                if (manager.FXModeActionEvents is { Length: > 0 } modes)
                {
                    firstMode = modes[0]?.EngineModeIndex ?? 0;
                    foreach (ThrottleVFXManager.FXModeActionEvent mode in modes)
                    {
                        modeExists |= mode != null && mode.EngineModeIndex == _previewEngineMode;
                    }
                }
                if (!modeExists)
                    _previewEngineMode = firstMode;
            }

            if (!IsPreviewActive)
            {
                PersistentProfileManager.EnsureSettingsForEditor();
                _previousSettings = PersistentProfileManager.Settings;
                _previousQuality = _previousSettings.PlumeQuality;
                _previousVolumetricsEnabled = _previousSettings.VolumetricPlumesEnabled;
                _activePreviewManager = manager;
                EditorApplication.update += OnEditorPreviewUpdate;
                if (_neutralView)
                    ApplyNeutralView();
            }

            UpdatePreview();
        }

        private static void UpdatePreview()
        {
            if (_activePreviewManager == null || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopPreview();
                return;
            }

            // Use the production quality paths during authoring, restoring the raw settings when the session ends.
            List<SerializedObject> before = CapturePreviewProperties(_activePreviewManager);
            try
            {
                UpdatePreviewEffects();
            }
            finally
            {
                foreach (SerializedObject snapshot in before)
                {
                    Object target = snapshot.targetObject;
                    if (target == null)
                    {
                        snapshot.Dispose();
                        continue;
                    }

                    PreviewChanges.TryGetValue(target, out var saved);
                    var paths = saved.Paths != null ? new HashSet<string>(saved.Paths) : new HashSet<string>();
                    // Ending preview hides effects even when their visibility did not change during refresh.
                    if (target is Renderer or Light)
                        paths.Add("m_Enabled");
                    if (target is ProFlare)
                        paths.Add(nameof(ProFlare.DisabledPlayMode));
                    using var current = new SerializedObject(target);
                    using SerializedProperty property = snapshot.GetIterator();
                    bool enterChildren = true;
                    while (property.Next(enterChildren))
                    {
                        enterChildren = property.propertyType == SerializedPropertyType.Generic;
                        if (enterChildren)
                            continue;

                        using SerializedProperty after = current.FindProperty(property.propertyPath);
                        if (after != null && !SerializedProperty.DataEquals(property, after))
                            paths.Add(property.propertyPath);
                    }

                    // Keep the original output values across refreshes. Authored inputs are never restored.
                    if (saved.Snapshot != null)
                    {
                        foreach (string path in saved.Paths)
                        {
                            using SerializedProperty original = saved.Snapshot.FindProperty(path);
                            using SerializedProperty destination = snapshot.FindProperty(path);
                            if (original != null && destination != null)
                                snapshot.CopyFromSerializedProperty(original);
                        }
                        saved.Snapshot.Dispose();
                    }
                    PreviewChanges[target] = (snapshot, paths);
                }
            }
        }

        private static void UpdatePreviewEffects()
        {
            PersistentProfileManager.PlumeQuality = _previewQuality;
            ThrottleVFXManager manager = _activePreviewManager;
            foreach (IEngineFXData effect in GetEffects(manager))
            {
                if (effect is MonoBehaviour behaviour)
                    EnsureEngineFxComponentInitialized(behaviour, effect);
                if (effect is ThrottleBlendshapeData mesh)
                    mesh.RefreshParameters();
                effect.ToggleVisibility(false);
            }

            manager.AtmoTransVFXHandler ??= new AtmosphereTransitionVFXHandling();
            manager.AtmoTransVFXHandler.UseMaterialPropertyBlocks = true;
            manager.AtmoTransVFXHandler.ClearAtmosphericVFXPropertyBlocks();
            manager.AtmoTransVFXHandler.CollectAtmosphericVFX(manager.gameObject, true);
            manager.AtmoTransVFXHandler.VesselPressureNormalized = _previewAtmosphere;
            manager.AtmoTransVFXHandler.SetAtmosphericVFXAlpha();
            StopPreviewParticles(manager);
            PreviewParticles.Clear();
            _lastParticleUpdateTime = EditorApplication.timeSinceStartup;

            foreach (IEngineFXData effect in GetEffects(manager, true))
            {
                if (effect is VolumetricPlumeSource || (!_previewParticles && effect is ThrottleParticleSystemData))
                    continue;

                effect.ToggleVisibility(ShouldSelectedPreviewEffectsBeVisible());
                float emission = effect is ThrottleBlendshapeData { UseEngineThrottle: true }
                    ? ThrottleVFXManager.GetPlumeEmission(_previewThrottle)
                    : _previewThrottle;
                effect.TriggerUpdateVisuals?.Invoke(emission, _previewAtmosphere, 0f, Vector3.zero);
                if (effect is ThrottleParticleSystemData particles && particles.VFX != null &&
                    !PreviewParticles.Contains(particles.VFX))
                    PreviewParticles.Add(particles.VFX);
            }

            // Simulating a parent also advances its children. Keep each hierarchy only once.
            for (int i = PreviewParticles.Count - 1; i >= 0; i--)
            {
                Transform parent = PreviewParticles[i].transform.parent;
                while (parent != null)
                {
                    if (parent.TryGetComponent(out ParticleSystem ancestor) && PreviewParticles.Contains(ancestor))
                    {
                        PreviewParticles.RemoveAt(i);
                        break;
                    }
                    parent = parent.parent;
                }
            }

            // Sources need not appear in the legacy event table. Their operating mode profiles still follow it.
            foreach (VolumetricPlumeSource source in manager.GetComponentsInChildren<VolumetricPlumeSource>(true))
            {
                EnsureEngineFxComponentInitialized(source, source);
                bool runningEvent = _previewModeEvent is ThrottleVFXManager.FXmodeEvent.FXModeRunning or
                    ThrottleVFXManager.FXmodeEvent.FXModeIncreasing or ThrottleVFXManager.FXmodeEvent.FXModeDecreasing;
                source.UpdatePreview(_previewEngineMode, _previewThrottle, _previewAtmosphere,
                    _previewQuality > 0 && runningEvent && ShouldSelectedPreviewEffectsBeVisible(), _comparePreset);
            }
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }

        internal static void StopPreview()
        {
            EditorApplication.delayCall -= ResumeAfterSave;
            _resumeAfterSave = null;
            ThrottleVFXManager manager = _activePreviewManager;
            _activePreviewManager = null;
            EditorApplication.update -= OnEditorPreviewUpdate;
            PreviewParticles.Clear();

            if (manager != null)
            {
                foreach (VolumetricPlumeSource source in manager.GetComponentsInChildren<VolumetricPlumeSource>(true))
                {
                    source.ToggleVisibility(false);
                }
                foreach (IEngineFXData effect in GetEffects(manager))
                {
                    effect.ToggleVisibility(false);
                }
                if (manager.AtmoTransVFXHandler != null)
                {
                    manager.AtmoTransVFXHandler.ClearAtmosphericVFXPropertyBlocks();
                    manager.AtmoTransVFXHandler.UseMaterialPropertyBlocks = false;
                    manager.AtmoTransVFXHandler.vfxInAtmosphereList.Clear();
                    manager.AtmoTransVFXHandler.vfxOutOfAtmosphereList.Clear();
                }
                StopPreviewParticles(manager);
            }

            foreach (var entry in PreviewChanges)
            {
                using SerializedObject snapshot = entry.Value.Snapshot;
                if (entry.Key == null)
                    continue;
                using var settings = new SerializedObject(entry.Key);
                foreach (string path in entry.Value.Paths)
                {
                    using SerializedProperty destination = settings.FindProperty(path);
                    using SerializedProperty original = snapshot.FindProperty(path);
                    if (destination != null && original != null)
                        settings.CopyFromSerializedProperty(original);
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            PreviewChanges.Clear();
            foreach (var entry in PropertyBlocks)
            {
                if (entry.Key != null)
                    entry.Key.SetPropertyBlock(entry.Value);
            }
            PropertyBlocks.Clear();
            RestoreSceneViews();
            if (_previousSettings != null && ReferenceEquals(_previousSettings, PersistentProfileManager.Settings))
            {
                _previousSettings.PlumeQuality = _previousQuality;
                _previousSettings.VolumetricPlumesEnabled = _previousVolumetricsEnabled;
            }
            _previousSettings = null;
            SceneView.RepaintAll();
        }

        // Save after restoring temporary output, then resume without changing the authored data.
        internal static void SuspendForSave()
        {
            if (!IsPreviewActive)
                return;
            ThrottleVFXManager manager = _activePreviewManager;
            StopPreview();
            _resumeAfterSave = manager;
            EditorApplication.delayCall += ResumeAfterSave;
        }

        private static void ResumeAfterSave()
        {
            ThrottleVFXManager manager = _resumeAfterSave;
            _resumeAfterSave = null;
            ActivateAndRefresh(manager);
        }

        private static List<SerializedObject> CapturePreviewProperties(ThrottleVFXManager manager)
        {
            var snapshots = new List<SerializedObject>();
            foreach (Component component in manager.GetComponentsInChildren<Component>(true))
            {
                if (component is not (Renderer or Light or ParticleSystem or IEngineFXData or ProFlare or Transform))
                    continue;
                if (component is Transform && component.GetComponent<ThrottleParticleSystemData>() == null)
                    continue;
                if (component is Renderer renderer && !PropertyBlocks.ContainsKey(renderer))
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    PropertyBlocks.Add(renderer, block);
                }
                snapshots.Add(new SerializedObject(component));
            }
            return snapshots;
        }

        private static void ApplyNeutralView()
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;
            SceneViews.TryAdd(view, (view.sceneLighting, view.sceneViewState.showSkybox, view.sceneViewState.showFog, view.showGrid));
            view.sceneLighting = false;
            view.sceneViewState.showSkybox = false;
            view.sceneViewState.showFog = false;
            view.showGrid = false;
            view.Repaint();
        }

        private static void RestoreSceneViews()
        {
            foreach (var entry in SceneViews)
            {
                if (entry.Key == null)
                    continue;
                entry.Key.sceneLighting = entry.Value.Lighting;
                entry.Key.sceneViewState.showSkybox = entry.Value.Skybox;
                entry.Key.sceneViewState.showFog = entry.Value.Fog;
                entry.Key.showGrid = entry.Value.Grid;
                entry.Key.Repaint();
            }
            SceneViews.Clear();
        }

        private static void FramePlume(ThrottleVFXManager manager, int? direction)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (manager == null || view == null)
                return;
            var bounds = new Bounds(manager.transform.position, Vector3.one * 0.1f);
            Vector3 axis = -manager.transform.up;
            foreach (Renderer renderer in manager.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.TryGetComponent(out IEngineFXData _) ||
                    (_previewQuality == 0 && renderer.enabled && !renderer.forceRenderingOff))
                    bounds.Encapsulate(renderer.bounds);
            }
            foreach (VolumetricPlumeSource source in manager.GetComponentsInChildren<VolumetricPlumeSource>(true))
            {
                if (source.Nozzles.Count > 0 && source.Nozzles[0] != null)
                    axis = source.Nozzles[0].forward;
                if (VolumetricPlumeRenderer.TryGetPreviewBounds(source, out Bounds plumeBounds))
                    bounds.Encapsulate(plumeBounds);
            }
            view.Frame(bounds, true);
            if (direction.HasValue)
            {
                Vector3 radial = Vector3.Cross(axis, manager.transform.right).normalized;
                if (radial.sqrMagnitude < 0.5f)
                    radial = Vector3.Cross(axis, manager.transform.forward).normalized;
                Vector3 forward = direction.Value switch { 1 => axis, 2 => -axis, _ => radial };
                Vector3 up = direction.Value == 0 ? -axis : radial;
                Vector3 right = Vector3.Cross(up, forward).normalized;
                float ExtentAlong(Vector3 vector) => Vector3.Dot(bounds.extents,
                    new Vector3(Mathf.Abs(vector.x), Mathf.Abs(vector.y), Mathf.Abs(vector.z)));
                float size = Mathf.Max(ExtentAlong(up), ExtentAlong(right) / view.camera.aspect) * 1.15f;
                if (!view.orthographic)
                {
                    float halfFov = view.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                    size = (ExtentAlong(forward) + size / Mathf.Tan(halfFov)) * Mathf.Sin(halfFov);
                }
                view.LookAt(bounds.center, Quaternion.LookRotation(forward, up), Mathf.Max(0.1f, size), false, true);
            }
            if (_neutralView && IsPreviewActive)
                ApplyNeutralView();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                StopPreview();
        }

        private static void OnEditorPreviewUpdate()
        {
            if (!IsPreviewActive || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                StopPreview();
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            if (_previewParticles)
            {
                double now = EditorApplication.timeSinceStartup;
                float deltaTime = Mathf.Clamp((float)(now - _lastParticleUpdateTime), 0f, 0.1f);
                _lastParticleUpdateTime = now;
                foreach (ParticleSystem particles in PreviewParticles)
                {
                    if (particles != null && ShouldSelectedPreviewEffectsBeVisible())
                        particles.Simulate(deltaTime, true, false, false);
                }
            }
            SceneView.RepaintAll();
        }

        private static IEnumerable<IEngineFXData> GetEffects(ThrottleVFXManager manager, bool selectedOnly = false)
        {
            if (manager.FXModeActionEvents == null)
                yield break;

            var seen = new HashSet<IEngineFXData>();
            foreach (ThrottleVFXManager.FXModeActionEvent mode in manager.FXModeActionEvents)
            {
                if (mode?.ActionEvents == null || (selectedOnly && mode.EngineModeIndex != _previewEngineMode))
                    continue;
                foreach (ThrottleVFXManager.FXActionEvent action in mode.ActionEvents)
                {
                    if (action?.EngineEffects == null || (selectedOnly && action.ModeEvent != _previewModeEvent))
                        continue;
                    foreach (ThrottleVFXManager.EngineEffect effect in action.EngineEffects)
                    {
                        if (effect?.EffectReference == null)
                            continue;

                        // References can be replaced or deleted while the event table is being authored.
                        IEngineFXData component = effect.EffectReference.GetComponent<IEngineFXData>();
                        effect.EngineFXComponent = component;
                        if (component != null && seen.Add(component))
                            yield return component;
                    }
                }
            }
        }

        private static bool ShouldSelectedPreviewEffectsBeVisible() =>
            _previewThrottle > 0.001f || _previewModeEvent is not (
                ThrottleVFXManager.FXmodeEvent.FXModeRunning or ThrottleVFXManager.FXmodeEvent.FXModeIncreasing or
                ThrottleVFXManager.FXmodeEvent.FXModeDecreasing);

        private static void EnsureEngineFxComponentInitialized(MonoBehaviour behaviour, IEngineFXData engineFxData)
        {
            if (engineFxData is ThrottleParticleSystemData particles && particles.VFX == null)
                behaviour.GetType().GetMethod("Awake", INSTANCE | BindingFlags.Public)?.Invoke(behaviour, null);

            if (engineFxData.TriggerUpdateVisuals != null)
                return;

            behaviour.GetType().GetMethod("Awake", INSTANCE | BindingFlags.Public)?.Invoke(behaviour, null);
            behaviour.GetType().GetMethod("OnEnable", INSTANCE | BindingFlags.Public)?.Invoke(behaviour, null);
        }

        private static void StopPreviewParticles(ThrottleVFXManager manager)
        {
            foreach (ParticleSystem particleSystem in manager.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
