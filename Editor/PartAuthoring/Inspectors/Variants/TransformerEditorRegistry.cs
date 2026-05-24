using System;
using System.Collections.Generic;
using System.Reflection;
using Ksp2UnityTools.Editor.Reflection;
using UnityEngine;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Variants
{
    /// <summary>
    /// Discovers and creates custom <see cref="ITransformerEditor" /> implementations registered via <see cref="TransformerEditorAttribute" />.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ReduxTypeCache" /> for assembly-spanning type discovery rather than Unity's <c>UnityEditor.TypeCache</c>, since the SDK runs in ThunderKit-imported contexts where the latter is unreliable.
    /// </remarks>
    public static class TransformerEditorRegistry
    {
        private static Dictionary<Type, Type> _editorTypeByTransformerType;

        /// <summary>
        /// Attempts to create a fresh editor instance for the given transformer type.
        /// </summary>
        /// <param name="transformerType">The transformer type to look up.</param>
        /// <param name="editor">The instantiated editor, if registered.</param>
        /// <returns>True if an editor exists; false otherwise.</returns>
        public static bool TryCreate(Type transformerType, out ITransformerEditor editor)
        {
            editor = null;
            if (transformerType == null)
            {
                return false;
            }
            EnsureBuilt();
            if (!_editorTypeByTransformerType.TryGetValue(transformerType, out var editorType))
            {
                return false;
            }
            try
            {
                editor = Activator.CreateInstance(editorType) as ITransformerEditor;
                return editor != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TransformerEditorRegistry] Failed to construct {editorType}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Drops the cached lookup. Next call rebuilds.
        /// </summary>
        public static void Invalidate()
        {
            _editorTypeByTransformerType = null;
        }

        private static void EnsureBuilt()
        {
            if (_editorTypeByTransformerType != null)
            {
                return;
            }
            _editorTypeByTransformerType = new Dictionary<Type, Type>();
            foreach (var editorType in ReduxTypeCache.GetTypesWithAttribute<TransformerEditorAttribute>())
            {
                if (!typeof(ITransformerEditor).IsAssignableFrom(editorType))
                {
                    Debug.LogWarning(
                        $"[TransformerEditorRegistry] {editorType.FullName} carries [TransformerEditor] but does not implement ITransformerEditor; ignored.");
                    continue;
                }
                var attr = editorType.GetCustomAttribute<TransformerEditorAttribute>();
                if (attr?.TransformerType == null)
                {
                    continue;
                }
                if (_editorTypeByTransformerType.TryGetValue(attr.TransformerType, out var existing))
                {
                    Debug.LogWarning(
                        $"[TransformerEditorRegistry] Duplicate editor for {attr.TransformerType.Name}: {existing.FullName} vs {editorType.FullName}. Keeping the first.");
                    continue;
                }
                _editorTypeByTransformerType[attr.TransformerType] = editorType;
            }
        }
    }
}
