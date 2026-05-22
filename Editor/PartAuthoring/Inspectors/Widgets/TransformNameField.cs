using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty that holds the bare name of a single
    /// Transform somewhere inside the part hierarchy. Renders as a <c>Transform</c>-typed
    /// <see cref="ObjectField" />; the author drags a Transform from the prefab and the leaf
    /// <c>gameObject.name</c> is stored.
    /// </summary>
    /// <remarks>
    /// Resolution mirrors the runtime: a recursive name walk via
    /// <see cref="TransformExtension.FindChildren" /> with the first match selected.
    /// The string SerializedProperty stays the source of truth on disk.
    ///
    /// A HelpBox appears beneath the field if the dropped Transform isn't a descendant of the
    /// part root.
    /// </remarks>
    public sealed class TransformNameField : VisualElement
    {
        private const string NAME_SUFFIX = " Name";

        private readonly SerializedProperty _prop;
        private readonly Transform _partRoot;
        private readonly ObjectField _objectField;
        private readonly HelpBox _warning;

        public TransformNameField(SerializedProperty prop, string label, Transform partRoot)
        {
            _prop = prop;
            _partRoot = partRoot;

            AddToClassList("transform-name-field");

            var cleanLabel = label != null && label.EndsWith(NAME_SUFFIX)
                ? label.Substring(0, label.Length - NAME_SUFFIX.Length)
                : label;

            _objectField = new ObjectField(cleanLabel) { objectType = typeof(Transform), allowSceneObjects = true };
            _objectField.AddToClassList("unity-base-field__aligned");
            _objectField.SetValueWithoutNotify(ResolveName(prop.stringValue));
            _objectField.RegisterValueChangedCallback(OnObjectChanged);
            _objectField.TrackPropertyValue(prop, OnPropertyChanged);
            Add(_objectField);

            _warning = new HelpBox("Transform is not under the part root.", HelpBoxMessageType.Warning);
            _warning.style.display = DisplayStyle.None;
            Add(_warning);
        }

        private Transform ResolveName(string transformName)
        {
            if (string.IsNullOrEmpty(transformName) || _partRoot == null)
            {
                return null;
            }
            var matches = _partRoot.FindChildren(transformName);
            return matches.Count > 0 ? matches[0] : null;
        }

        private void OnObjectChanged(ChangeEvent<Object> evt)
        {
            var t = evt.newValue as Transform;
            string newName;
            var unresolvable = false;

            if (t == null)
            {
                newName = string.Empty;
            }
            else if (_partRoot == null || !t.IsChildOf(_partRoot))
            {
                newName = string.Empty;
                unresolvable = true;
            }
            else
            {
                newName = t.gameObject.name;
            }

            _warning.style.display = unresolvable ? DisplayStyle.Flex : DisplayStyle.None;

            _prop.serializedObject.Update();
            _prop.stringValue = newName;
            _prop.serializedObject.ApplyModifiedProperties();
        }

        private void OnPropertyChanged(SerializedProperty p)
        {
            var resolved = ResolveName(p.stringValue);
            if (_objectField.value != resolved)
            {
                _objectField.SetValueWithoutNotify(resolved);
            }
        }
    }
}
