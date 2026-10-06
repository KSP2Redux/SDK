using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Widgets
{
    /// <summary>
    /// Hides the inspectors of components that an authoring inspector already presents, and restores them afterwards.
    /// </summary>
    /// <remarks>
    /// An authoring inspector that presents several components as one calls <see cref="Hide" /> from its editor's
    /// <c>OnEnable</c> and <see cref="Restore" /> from <c>OnDisable</c>. HideFlags are serialized, so a restore that
    /// lands while Unity cannot safely write them, during a play mode change or a compile, is queued until it can.
    /// </remarks>
    public class InspectorComponentHider
    {
        private static readonly List<Dictionary<Component, HideFlags>> PendingRestores = new();
        private static bool _flushRegistered;

        private Dictionary<Component, HideFlags> _originalFlags;

        /// <summary>
        /// Hides the inspector of every component on <paramref name="gameObject" /> whose type is in
        /// <paramref name="types" />.
        /// </summary>
        /// <remarks>
        /// Does nothing while Unity cannot safely change HideFlags, so the components stay visible rather than risk a
        /// hide that is never undone.
        /// </remarks>
        /// <param name="gameObject">The GameObject whose components to hide.</param>
        /// <param name="types">The component types to hide, matched with <see cref="Type.IsInstanceOfType" />.</param>
        public void Hide(GameObject gameObject, IReadOnlyList<Type> types)
        {
            Restore();
            if (gameObject == null || !CanMutate())
                return;

            _originalFlags = new Dictionary<Component, HideFlags>();
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                if (component == null || !Matches(component, types))
                    continue;

                _originalFlags[component] = component.hideFlags;
                component.hideFlags |= HideFlags.HideInInspector;
            }
        }

        /// <summary>
        /// Restores the components hidden by the last <see cref="Hide" />.
        /// </summary>
        public void Restore()
        {
            if (_originalFlags == null)
                return;

            Dictionary<Component, HideFlags> originalFlags = _originalFlags;
            _originalFlags = null;
            if (CanMutate())
            {
                RestoreNow(originalFlags);
                return;
            }

            PendingRestores.Add(originalFlags);
            RegisterFlush();
        }

        private static bool Matches(Component component, IReadOnlyList<Type> types)
        {
            foreach (Type type in types)
            {
                if (type.IsInstanceOfType(component))
                    return true;
            }

            return false;
        }

        private static void RestoreNow(Dictionary<Component, HideFlags> originalFlags)
        {
            foreach (KeyValuePair<Component, HideFlags> pair in originalFlags)
            {
                if (pair.Key != null)
                {
                    pair.Key.hideFlags = pair.Value;
                }
            }
        }

        private static void RegisterFlush()
        {
            if (_flushRegistered)
                return;

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += FlushPending;
            _flushRegistered = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += FlushPending;
            }
        }

        private static void FlushPending()
        {
            if (!CanMutate())
            {
                EditorApplication.delayCall += FlushPending;
                return;
            }

            foreach (Dictionary<Component, HideFlags> originalFlags in PendingRestores)
            {
                RestoreNow(originalFlags);
            }

            PendingRestores.Clear();
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            _flushRegistered = false;
        }

        // A play mode transition is when isPlayingOrWillChangePlaymode and isPlaying disagree. Writing HideFlags then
        // races scene serialization.
        private static bool CanMutate() =>
            EditorApplication.isPlayingOrWillChangePlaymode == EditorApplication.isPlaying
            && !EditorApplication.isCompiling
            && !EditorApplication.isUpdating;
    }
}
