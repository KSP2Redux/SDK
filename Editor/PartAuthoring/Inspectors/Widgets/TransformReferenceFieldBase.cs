using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Shared chrome for editor fields that store a string locator for a <see cref="Transform" /> somewhere inside the part hierarchy.
    /// </summary>
    /// <remarks>
    /// Renders as a <c>Transform</c>-typed <see cref="ObjectField" /> with a warning HelpBox when the dropped Transform is outside the part root. Subclasses define how the stored string maps to and from the Transform. The string SerializedProperty stays the source of truth on disk. The ObjectField is a thin adapter that resolves the stored locator to a live Transform reference for drag-and-drop ergonomics. A drop from outside the part root clears the stored value and surfaces the warning so an unresolvable cross-prefab reference never silently makes it to disk.
    /// </remarks>
    public abstract class TransformReferenceFieldBase : VisualElement
    {
        private readonly SerializedProperty _prop;
        private readonly ObjectField _objectField;
        private readonly HelpBox _warning;

        /// <summary>
        /// Resolves the stored locator to a live Transform, or null when nothing matches.
        /// </summary>
        /// <param name="stored">The stored locator string.</param>
        /// <returns>The resolved Transform, or null when nothing matches.</returns>
        protected abstract Transform Resolve(string stored);

        /// <summary>
        /// Computes the storage form for the given Transform (already verified to live under <see cref="PartRoot" />).
        /// </summary>
        /// <param name="target">The Transform to encode as a locator string.</param>
        /// <returns>The locator string for <paramref name="target" />.</returns>
        protected abstract string Compute(Transform target);

        /// <summary>
        /// Gets the part root Transform, available to subclasses for resolving and computing.
        /// </summary>
        protected Transform PartRoot { get; }

        /// <summary>
        /// Initialises the shared ObjectField, warning HelpBox, and SerializedProperty wiring.
        /// </summary>
        /// <param name="prop">The string SerializedProperty holding the locator.</param>
        /// <param name="label">The author-facing label. A trailing <paramref name="labelSuffix" /> is stripped for display.</param>
        /// <param name="partRoot">The part root used to resolve and verify Transforms.</param>
        /// <param name="className">USS class applied to the root visual element.</param>
        /// <param name="labelSuffix">Suffix stripped from <paramref name="label" /> when present.</param>
        protected TransformReferenceFieldBase(
            SerializedProperty prop,
            string label,
            Transform partRoot,
            string className,
            string labelSuffix)
        {
            _prop = prop;
            PartRoot = partRoot;

            AddToClassList(className);

            var cleanLabel = label != null && label.EndsWith(labelSuffix)
                ? label.Substring(0, label.Length - labelSuffix.Length)
                : label;

            _objectField = new ObjectField(cleanLabel) { objectType = typeof(Transform), allowSceneObjects = true };
            _objectField.AddToClassList("unity-base-field__aligned");
            _objectField.SetValueWithoutNotify(Resolve(prop.stringValue));
            _objectField.RegisterValueChangedCallback(OnObjectChanged);
            _objectField.TrackPropertyValue(prop, OnPropertyChanged);
            Add(_objectField);

            _warning = new HelpBox("Transform is not under the part root.", HelpBoxMessageType.Warning);
            _warning.style.display = DisplayStyle.None;
            Add(_warning);
        }

        private void OnObjectChanged(ChangeEvent<Object> evt)
        {
            var t = evt.newValue as Transform;
            string newValue;
            var unresolvable = false;

            if (t == null)
            {
                newValue = string.Empty;
            }
            else if (PartRoot == null || !t.IsChildOf(PartRoot))
            {
                newValue = string.Empty;
                unresolvable = true;
            }
            else
            {
                newValue = Compute(t);
            }

            _warning.style.display = unresolvable ? DisplayStyle.Flex : DisplayStyle.None;

            _prop.serializedObject.Update();
            _prop.stringValue = newValue;
            _prop.serializedObject.ApplyModifiedProperties();
        }

        private void OnPropertyChanged(SerializedProperty p)
        {
            var resolved = Resolve(p.stringValue);
            if (_objectField.value != resolved)
            {
                _objectField.SetValueWithoutNotify(resolved);
            }
        }
    }
}
