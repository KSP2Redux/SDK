using System;
using System.Collections.Generic;
using System.Reflection;
using Ksp2UnityTools.Editor.Reflection;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors
{
    /// <summary>
    /// Discovers and creates custom <see cref="IDataEditor" /> implementations registered via
    /// <see cref="DataEditorAttribute" />.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ReduxTypeCache" /> for assembly-spanning type discovery rather than
    /// Unity's <c>UnityEditor.TypeCache</c>, since the SDK runs in ThunderKit-imported contexts
    /// where the latter is unreliable.
    /// </remarks>
    public static class DataEditorRegistry
    {
        private static Dictionary<Type, Type> _editorTypeByDataType;

        /// <summary>
        /// Attempts to create a fresh editor instance for the given Data_* type.
        /// </summary>
        /// <param name="dataType">The Data_* type to look up.</param>
        /// <param name="editor">The instantiated editor, if registered.</param>
        /// <returns>True if an editor exists; false otherwise.</returns>
        public static bool TryCreate(Type dataType, out IDataEditor editor)
        {
            editor = null;
            if (dataType == null)
            {
                return false;
            }
            EnsureBuilt();
            if (!_editorTypeByDataType.TryGetValue(dataType, out var editorType))
            {
                return false;
            }
            try
            {
                editor = Activator.CreateInstance(editorType) as IDataEditor;
                return editor != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DataEditorRegistry] Failed to construct {editorType}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Drops the cached lookup. Next call rebuilds.
        /// </summary>
        public static void Invalidate()
        {
            _editorTypeByDataType = null;
        }

        private static void EnsureBuilt()
        {
            if (_editorTypeByDataType != null)
            {
                return;
            }
            _editorTypeByDataType = new Dictionary<Type, Type>();
            foreach (var editorType in ReduxTypeCache.GetTypesWithAttribute<DataEditorAttribute>())
            {
                if (!typeof(IDataEditor).IsAssignableFrom(editorType))
                {
                    Debug.LogWarning(
                        $"[DataEditorRegistry] {editorType.FullName} carries [DataEditor] but does not implement IDataEditor; ignored.");
                    continue;
                }
                var attr = editorType.GetCustomAttribute<DataEditorAttribute>();
                if (attr?.DataType == null)
                {
                    continue;
                }
                if (_editorTypeByDataType.TryGetValue(attr.DataType, out var existing))
                {
                    Debug.LogWarning(
                        $"[DataEditorRegistry] Duplicate editor for {attr.DataType.Name}: {existing.FullName} vs {editorType.FullName}. Keeping the first.");
                    continue;
                }
                _editorTypeByDataType[attr.DataType] = editorType;
            }
        }
    }
}
