using System;
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.Gizmos
{
    /// <summary>
    /// Registry of per-field gizmo-visibility toggles. Lets a field with a SceneView handle
    /// expose an inline "show gizmo" toggle next to its handle button, so the visibility control
    /// for a module-specific gizmo lives with the field rather than on the global part inspector
    /// gizmo bar.
    /// </summary>
    /// <remarks>
    /// Entries are keyed by the field's declaring type and field name. The editor-side
    /// dispatcher (<see cref="ReflectionModuleEditor.BuildFieldRow" />) looks up the registry
    /// when constructing a <see cref="VectorHandleField" /> and forwards the entry, if any.
    /// Registrations are typically declared from <c>[InitializeOnLoadMethod]</c> hooks on the
    /// editor-only gizmo files that actually draw the gizmo.
    /// </remarks>
    public static class GizmoToggleRegistry
    {
        /// <summary>
        /// One entry in the registry: a getter/setter pair backing a per-field gizmo toggle, with
        /// an author-facing label.
        /// </summary>
        public sealed class Entry
        {
            public string Label { get; }
            public Func<bool> Getter { get; }
            public Action<bool> Setter { get; }

            public Entry(string label, Func<bool> getter, Action<bool> setter)
            {
                Label = label;
                Getter = getter;
                Setter = setter;
            }
        }

        private static readonly Dictionary<(Type, string), Entry> _entries = new();

        /// <summary>
        /// Registers a per-field gizmo toggle.
        /// </summary>
        /// <param name="declaringType">The Data_* type declaring the field.</param>
        /// <param name="fieldName">The field's source name.</param>
        /// <param name="label">Author-facing toggle label.</param>
        /// <param name="getter">Returns the current toggle value.</param>
        /// <param name="setter">Writes the toggle value.</param>
        public static void Register(Type declaringType, string fieldName, string label, Func<bool> getter, Action<bool> setter)
        {
            if (declaringType == null || string.IsNullOrEmpty(fieldName) || getter == null || setter == null)
            {
                return;
            }
            _entries[(declaringType, fieldName)] = new Entry(label, getter, setter);
        }

        /// <summary>
        /// Looks up an entry by declaring type and field name. Returns null when no entry is registered.
        /// </summary>
        public static Entry TryGet(Type declaringType, string fieldName)
        {
            if (declaringType == null || string.IsNullOrEmpty(fieldName))
            {
                return null;
            }
            return _entries.TryGetValue((declaringType, fieldName), out var entry) ? entry : null;
        }
    }
}
