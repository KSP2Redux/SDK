using System;
using KSP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    /// <summary>
    /// Tracks the part the user is currently editing.
    /// </summary>
    /// <remarks>
    /// Resolves the editor selection (or the open prefab-stage root) to a
    /// <see cref="CorePartData" /> instance and broadcasts changes through
    /// <see cref="OnChanged" />. Two consumers benefit: windows that follow
    /// selection (Reference Parts, Validation Report) get a pre-resolved part
    /// without re-running the prefab-stage rule, and SceneView gizmos or
    /// overlays get an O(1) "am I the focused part?" check via
    /// <see cref="Current" />. Survives domain reload via
    /// <see cref="InitializeOnLoadAttribute" />.
    /// </remarks>
    [InitializeOnLoad]
    public static class ActivePartTracker
    {
        private static CorePartData _current;

        /// <summary>
        /// Fires when <see cref="Current" /> changes, including when it becomes null.
        /// </summary>
        /// <remarks>
        /// The argument is the new value of <see cref="Current" />. Null indicates no part is selected.
        /// </remarks>
        public static event Action<CorePartData> OnChanged;

        /// <summary>
        /// Gets the part the user is currently editing, or null if none is selected.
        /// </summary>
        public static CorePartData Current => _current;

        static ActivePartTracker()
        {
            Selection.selectionChanged += Refresh;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            Refresh();
        }

        /// <summary>
        /// Re-resolves <see cref="Current" /> from the editor selection and prefab stage.
        /// </summary>
        /// <remarks>
        /// Useful after asset operations that invalidate cached references such as
        /// deletes, reimports, or external file changes. Fires <see cref="OnChanged" />
        /// only when the resolved part differs from the previous value.
        /// </remarks>
        public static void Refresh()
        {
            var resolved = Resolve();
            if (resolved == _current) return;
            _current = resolved;
            OnChanged?.Invoke(_current);
        }

        private static CorePartData Resolve()
        {
            // The open prefab stage wins. Editing a part prefab is the authoring context,
            // even when the user has drilled into a child transform inside it.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null)
            {
                var rootCore = stage.prefabContentsRoot.GetComponent<CorePartData>();
                if (rootCore != null)
                {
                    return rootCore;
                }
                var childCore = stage.prefabContentsRoot.GetComponentInChildren<CorePartData>(true);
                if (childCore != null)
                {
                    return childCore;
                }
            }

            // Falls back to whatever the Project or Hierarchy selection points at.
            var selection = Selection.activeObject;
            return selection switch
            {
                CorePartData direct => direct,
                GameObject go => go.GetComponentInChildren<CorePartData>(true),
                _ => null,
            };
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            Refresh();
        }

        private static void OnPrefabStageClosing(PrefabStage stage)
        {
            Refresh();
        }
    }
}
