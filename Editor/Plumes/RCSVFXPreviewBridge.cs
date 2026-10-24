using System.Collections.Generic;
using KSP.Modules;
using KSP.VFX;
using UnityEditor;
using UnityEngine;

namespace KSP.Editor
{
    /// <summary>
    /// Owns the nozzle effects and cleanup for an RCS inspector preview.
    /// </summary>
    internal class RCSVFXPreviewBridge
    {
        private readonly List<MonoBehaviour> _effects = new();

        /// <summary>
        /// Gets whether the preview has nozzle effects and can run outside Play mode.
        /// </summary>
        internal bool CanPreview => !EditorApplication.isPlayingOrWillChangePlaymode && _effects.Count > 0;

        /// <summary>
        /// Gets whether all nozzle effects are being previewed.
        /// </summary>
        internal bool IsPreviewEnabled { get; private set; }

        /// <summary>
        /// Collects and initializes nozzle effects, leaving them hidden.
        /// </summary>
        /// <param name="manager">The root RCS manager being inspected.</param>
        internal void Initialize(RCSVFXManager manager)
        {
            Release();
            if (EditorApplication.isPlayingOrWillChangePlaymode || manager == null)
                return;

            var module = manager.GetComponent<Module_RCS>();
            if (module == null || module.ThrusterTransforms == null)
                return;

            foreach (Transform nozzle in module.ThrusterTransforms)
            {
                if (nozzle == null)
                    continue;

                foreach (MonoBehaviour behaviour in nozzle.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is not IEngineFXData effect || _effects.Contains(behaviour))
                        continue;

                    EngineVFXPreviewUtility.EnsureEngineFxComponentInitialized(behaviour, effect);
                    _effects.Add(behaviour);
                }
            }

            SetPreview(false);
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        /// <summary>
        /// Shows all nozzle effects at full thrust or hides and clears them.
        /// </summary>
        /// <param name="enabled">Whether the effects should be visible.</param>
        internal void SetPreview(bool enabled)
        {
            if (EditorApplication.isPlaying)
                return;

            IsPreviewEnabled = enabled && CanPreview;
            foreach (MonoBehaviour behaviour in _effects)
            {
                if (behaviour == null)
                    continue;

                var effect = (IEngineFXData)behaviour;
                if (IsPreviewEnabled)
                {
                    effect.TriggerUpdateVisuals?.Invoke(1f, 1f, 0f, Vector3.zero);
                }

                effect.ToggleVisibility(IsPreviewEnabled, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SceneView.RepaintAll();
        }

        /// <summary>
        /// Hides preview effects and removes the editor lifecycle callback.
        /// </summary>
        internal void Release()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            SetPreview(false);
            IsPreviewEnabled = false;
            _effects.Clear();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                SetPreview(false);
        }
    }
}
