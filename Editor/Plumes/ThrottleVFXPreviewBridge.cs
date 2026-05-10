using System.Collections.Generic;
using System.Reflection;
using KSP.Sim.impl;
using KSP.VFX;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    internal static class ThrottleVFXPreviewBridge
    {
        private static bool _previewActive;
        private static bool _previewParticles;
        private static float _previewThrottle = 1f;
        private static float _previewAtmosphere = 1f;
        private static int _previewEngineMode;
        private static ThrottleVFXManager.FXmodeEvent _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeRunning;
        private static ThrottleVFXManager _activePreviewManager;
        private static double _particlePreviewStartTime;
        private static bool _editorUpdateRegistered;
        private static int _particlePreviewSignature;
        public static bool IsPreviewActive => _previewActive;

        public static void DrawPreviewControls(ThrottleVFXManager manager, bool showHeader = true)
        {
            if (showHeader)
            {
                EditorGUILayout.LabelField("Editor Preview", EditorStyles.boldLabel);
            }

            if (manager == null)
            {
                EditorGUILayout.HelpBox("No parent ThrottleVFXManager found.", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _previewActive = EditorGUILayout.Toggle("Enable Preview", _previewActive);
            if (EditorGUI.EndChangeCheck())
            {
                if (_previewActive)
                {
                    UpdatePreview(manager);
                    UpdateEditorPreviewRegistration(manager);
                }
                else
                {
                    StopAllVfx(manager);
                }
            }

            if (_previewActive)
            {
                EditorGUI.BeginChangeCheck();
                bool wasPreviewingParticles = _previewParticles;
                int maxEngineModeIndex = manager.FXModeActionEvents != null
                    ? Mathf.Max(0, manager.FXModeActionEvents.Length - 1)
                    : 0;
                _previewEngineMode = EditorGUILayout.IntSlider(
                    "Engine Mode Index",
                    _previewEngineMode,
                    0,
                    maxEngineModeIndex
                );

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Preview Event");
                if (EditorGUILayout.DropdownButton(new GUIContent(_previewModeEvent.ToString()), FocusType.Passive))
                {
                    var menu = new GenericMenu();
                    foreach (ThrottleVFXManager.FXmodeEvent modeEvent in System.Enum.GetValues(typeof(ThrottleVFXManager.FXmodeEvent)))
                    {
                        menu.AddItem(new GUIContent(modeEvent.ToString()), _previewModeEvent == modeEvent, () =>
                        {
                            _previewModeEvent = modeEvent;
                            ResetParticlePreviewPlayback(manager);
                            UpdatePreview(manager);
                        });
                    }

                    menu.ShowAsContext();
                }

                EditorGUILayout.EndHorizontal();
                _previewThrottle = EditorGUILayout.Slider("Throttle", _previewThrottle, 0f, 1f);
                _previewParticles = EditorGUILayout.Toggle("Preview Particles", _previewParticles);
                float logValue = Mathf.Log10(Mathf.Max(0.0001f, _previewAtmosphere));
                float newLogValue = EditorGUILayout.Slider("Atmosphere (log)", logValue, -4f, 0f);
                if (!Mathf.Approximately(newLogValue, logValue))
                {
                    _previewAtmosphere = Mathf.Pow(10f, newLogValue);
                    if (_previewAtmosphere < 0.00011f)
                    {
                        _previewAtmosphere = 0f;
                    }
                }

                EditorGUILayout.LabelField("Actual Pressure", _previewAtmosphere.ToString("F4") + " atm");
                if (EditorGUI.EndChangeCheck() || GUILayout.Button("Update Visuals"))
                {
                    if (wasPreviewingParticles && !_previewParticles)
                    {
                        StopPreviewParticles(manager);
                    }
                    else if (_previewParticles)
                    {
                        ResetParticlePreviewPlayback(manager);
                    }

                    UpdatePreview(manager);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Quick Events", EditorStyles.miniBoldLabel);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Start/Ignition"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeStart;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Stop"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeStop;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Flameout"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeFlameout;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Restart"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeRestart;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Running"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeRunning;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Increasing"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeIncreasing;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Decreasing"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeDecreasing;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Enter"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeEnter;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Exit"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeExit;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Enter Running"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeEnterRunning;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                if (GUILayout.Button("Exit Running"))
                {
                    _previewModeEvent = ThrottleVFXManager.FXmodeEvent.FXModeExitRunning;
                    ResetParticlePreviewPlayback(manager);
                    UpdatePreview(manager);
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                if (GUILayout.Button("Stop All Preview VFX"))
                {
                    StopAllVfx(manager);
                }
            }
            else if (GUI.changed)
            {
                StopAllVfx(manager);
            }
        }

        public static void ActivateAndRefresh(ThrottleVFXManager manager)
        {
            if (manager == null)
            {
                return;
            }

            _previewActive = true;
            UpdatePreview(manager);
            UpdateEditorPreviewRegistration(manager);
        }

        private static void UpdatePreview(ThrottleVFXManager manager, bool repaint = true)
        {
            if (manager.FXModeActionEvents == null)
            {
                return;
            }

            MethodInfo initFXDataMethod = typeof(ThrottleVFXManager).GetMethod(
                "InitFXData",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            initFXDataMethod?.Invoke(manager, null);

            foreach (ThrottleVFXManager.FXModeActionEvent modeEvent in manager.FXModeActionEvents)
            {
                if (modeEvent?.ActionEvents == null)
                {
                    continue;
                }

                foreach (ThrottleVFXManager.FXActionEvent actionEvent in modeEvent.ActionEvents)
                {
                    if (actionEvent?.EngineEffects == null)
                    {
                        continue;
                    }

                    foreach (ThrottleVFXManager.EngineEffect effect in actionEvent.EngineEffects)
                    {
                        if (effect == null)
                        {
                            continue;
                        }

                        if (effect.EffectReference != null && effect.EngineFXComponent == null)
                        {
                            effect.EngineFXComponent = effect.EffectReference.GetComponent<IEngineFXData>();
                        }

                        if (effect.EngineFXComponent is MonoBehaviour mb)
                        {
                            EnsureEngineFxComponentInitialized(mb, effect.EngineFXComponent);
                        }
                    }
                }
            }

            manager.AtmoTransVFXHandler ??= new AtmosphereTransitionVFXHandling();
            manager.AtmoTransVFXHandler.UseMaterialPropertyBlocks = true;
            manager.AtmoTransVFXHandler.ClearAtmosphericVFXPropertyBlocks();
            manager.AtmoTransVFXHandler.CollectAtmosphericVFX(manager.gameObject, true);

            manager.AtmoTransVFXHandler.VesselPressureNormalized = _previewAtmosphere;
            manager.AtmoTransVFXHandler.SetAtmosphericVFXAlpha();
            EnsureParticlePreviewSignature(manager);
            StopUnselectedPreviewParticles(manager);

            foreach (ThrottleVFXManager.FXModeActionEvent modeEvent in manager.FXModeActionEvents)
            {
                if (modeEvent == null || modeEvent.EngineModeIndex != _previewEngineMode || modeEvent.ActionEvents == null)
                {
                    continue;
                }

                foreach (ThrottleVFXManager.FXActionEvent actionEvent in modeEvent.ActionEvents)
                {
                    if (actionEvent == null || actionEvent.EngineEffects == null || actionEvent.ModeEvent != _previewModeEvent)
                    {
                        continue;
                    }

                    foreach (ThrottleVFXManager.EngineEffect effect in actionEvent.EngineEffects)
                    {
                        if (effect?.EngineFXComponent == null)
                        {
                            continue;
                        }

                        if (!_previewParticles && effect.EngineFXComponent is ThrottleParticleSystemData)
                        {
                            effect.EngineFXComponent.ToggleVisibility(
                                false,
                                ParticleSystemStopBehavior.StopEmittingAndClear
                            );
                            continue;
                        }

                        bool shouldBeVisible = ShouldSelectedPreviewEffectsBeVisible();
                        effect.EngineFXComponent.ToggleVisibility(shouldBeVisible);
                        effect.EngineFXComponent.TriggerUpdateVisuals?.Invoke(
                            _previewThrottle,
                            _previewAtmosphere,
                            0f,
                            Vector3.zero
                        );
                    }
                }
            }

            _activePreviewManager = manager;
            UpdateEditorPreviewRegistration(manager);
            if (repaint)
            {
                SceneView.RepaintAll();
            }
        }

        private static void StopAllVfx(ThrottleVFXManager manager)
        {
            if (manager == null || manager.FXModeActionEvents == null)
            {
                UnregisterEditorPreviewUpdate();
                return;
            }

            foreach (ThrottleVFXManager.FXModeActionEvent modeEvent in manager.FXModeActionEvents)
            {
                if (modeEvent?.ActionEvents == null)
                {
                    continue;
                }

                foreach (ThrottleVFXManager.FXActionEvent actionEvent in modeEvent.ActionEvents)
                {
                    if (actionEvent?.EngineEffects == null)
                    {
                        continue;
                    }

                    foreach (ThrottleVFXManager.EngineEffect effect in actionEvent.EngineEffects)
                    {
                        effect?.EngineFXComponent?.ToggleVisibility(false);
                    }
                }
            }

            if (manager.AtmoTransVFXHandler != null)
            {
                manager.AtmoTransVFXHandler.ClearAtmosphericVFXPropertyBlocks();
                manager.AtmoTransVFXHandler.UseMaterialPropertyBlocks = false;
                manager.AtmoTransVFXHandler.vfxInAtmosphereList.Clear();
                manager.AtmoTransVFXHandler.vfxOutOfAtmosphereList.Clear();
            }

            StopPreviewParticles(manager);
            UnregisterEditorPreviewUpdate();
            SceneView.RepaintAll();
        }

        private static void EnsureEngineFxComponentInitialized(MonoBehaviour behaviour, IEngineFXData engineFxData)
        {
            MethodInfo awake = behaviour.GetType()
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            MethodInfo onEnable = behaviour.GetType()
                .GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (engineFxData is ThrottleParticleSystemData particleData && particleData.VFX == null)
            {
                awake?.Invoke(behaviour, null);
            }

            if (engineFxData.TriggerUpdateVisuals != null)
            {
                return;
            }

            awake?.Invoke(behaviour, null);
            onEnable?.Invoke(behaviour, null);
        }

        private static void UpdateEditorPreviewRegistration(ThrottleVFXManager manager)
        {
            _activePreviewManager = manager;
            if (_previewActive && manager != null)
            {
                if (!_editorUpdateRegistered)
                {
                    EditorApplication.update += OnEditorPreviewUpdate;
                    _editorUpdateRegistered = true;
                    _particlePreviewStartTime = EditorApplication.timeSinceStartup;
                }

                return;
            }

            UnregisterEditorPreviewUpdate();
        }

        private static void UnregisterEditorPreviewUpdate()
        {
            if (!_editorUpdateRegistered)
            {
                return;
            }

            EditorApplication.update -= OnEditorPreviewUpdate;
            _editorUpdateRegistered = false;
            _particlePreviewStartTime = 0d;
            _particlePreviewSignature = 0;
        }

        private static void OnEditorPreviewUpdate()
        {
            if (!_previewActive || _activePreviewManager == null ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnregisterEditorPreviewUpdate();
                return;
            }

            EditorApplication.QueuePlayerLoopUpdate();
            if (_previewParticles)
            {
                float previewTime = Mathf.Max(0f, (float)(EditorApplication.timeSinceStartup - _particlePreviewStartTime));
                SimulatePreviewParticles(_activePreviewManager, previewTime);
            }

            SceneView.RepaintAll();
        }

        private static void SimulatePreviewParticles(ThrottleVFXManager manager, float previewTime)
        {
            bool shouldBeVisible = ShouldSelectedPreviewEffectsBeVisible();
            HashSet<ParticleSystem> selectedParticleSystems = new(GetSelectedPreviewParticleSystems(manager));
            foreach (ParticleSystem particleSystem in selectedParticleSystems)
            {
                if (particleSystem == null || !particleSystem.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!shouldBeVisible)
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    continue;
                }

                if (!particleSystem.isPlaying)
                {
                    particleSystem.Play(true);
                }

                particleSystem.Simulate(previewTime, false, true, false);
            }
        }

        private static bool ShouldSelectedPreviewEffectsBeVisible()
        {
            return _previewThrottle > 0.001f ||
                _previewModeEvent != ThrottleVFXManager.FXmodeEvent.FXModeRunning &&
                _previewModeEvent != ThrottleVFXManager.FXmodeEvent.FXModeIncreasing &&
                _previewModeEvent != ThrottleVFXManager.FXmodeEvent.FXModeDecreasing;
        }

        private static void EnsureParticlePreviewSignature(ThrottleVFXManager manager)
        {
            if (!_previewParticles || manager == null)
            {
                return;
            }

            int signature = GetParticlePreviewSignature(manager);
            if (signature == _particlePreviewSignature)
            {
                return;
            }

            ResetParticlePreviewPlayback(manager);
            _particlePreviewSignature = signature;
        }

        private static int GetParticlePreviewSignature(ThrottleVFXManager manager)
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + manager.GetInstanceID();
                hash = hash * 31 + _previewEngineMode;
                hash = hash * 31 + (int)_previewModeEvent;
                hash = hash * 31 + Mathf.RoundToInt(_previewThrottle * 1000f);
                hash = hash * 31 + Mathf.RoundToInt(_previewAtmosphere * 100000f);
                return hash;
            }
        }

        private static void ResetParticlePreviewPlayback(ThrottleVFXManager manager)
        {
            StopPreviewParticles(manager);
            _particlePreviewStartTime = EditorApplication.timeSinceStartup;
            _particlePreviewSignature = GetParticlePreviewSignature(manager);
        }

        private static void StopUnselectedPreviewParticles(ThrottleVFXManager manager)
        {
            if (!_previewParticles || manager == null)
            {
                return;
            }

            HashSet<ParticleSystem> selectedParticleSystems = new(GetSelectedPreviewParticleSystems(manager));
            foreach (ParticleSystem particleSystem in manager.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (!selectedParticleSystems.Contains(particleSystem))
                {
                    particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private static void StopPreviewParticles(ThrottleVFXManager manager)
        {
            if (manager == null)
            {
                return;
            }

            foreach (ParticleSystem particleSystem in manager.GetComponentsInChildren<ParticleSystem>(true))
            {
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static IEnumerable<ParticleSystem> GetSelectedPreviewParticleSystems(ThrottleVFXManager manager)
        {
            if (manager == null || manager.FXModeActionEvents == null)
            {
                yield break;
            }

            foreach (ThrottleVFXManager.FXModeActionEvent modeEvent in manager.FXModeActionEvents)
            {
                if (modeEvent == null || modeEvent.EngineModeIndex != _previewEngineMode || modeEvent.ActionEvents == null)
                {
                    continue;
                }

                foreach (ThrottleVFXManager.FXActionEvent actionEvent in modeEvent.ActionEvents)
                {
                    if (actionEvent == null || actionEvent.ModeEvent != _previewModeEvent ||
                        actionEvent.EngineEffects == null)
                    {
                        continue;
                    }

                    foreach (ThrottleVFXManager.EngineEffect effect in actionEvent.EngineEffects)
                    {
                        if (effect?.EffectReference == null)
                        {
                            continue;
                        }

                        foreach (ParticleSystem particleSystem in effect.EffectReference.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            yield return particleSystem;
                        }
                    }
                }
            }
        }

    }
}
