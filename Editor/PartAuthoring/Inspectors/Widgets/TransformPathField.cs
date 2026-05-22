using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Editor field for a string SerializedProperty that holds a slash-separated path to a
    /// Transform within the part hierarchy. Renders as a <c>Transform</c>-typed
    /// <see cref="ObjectField" />; the author drags a Transform from the prefab and the
    /// relative path string is written automatically.
    /// </summary>
    /// <remarks>
    /// The string SerializedProperty stays the source of truth on disk. Resolution uses
    /// <c>Transform.Find(path)</c> which matches the runtime path-aware lookup
    /// (<c>root.Find(path)</c>).
    ///
    /// A HelpBox appears beneath the field if the dropped Transform isn't a descendant of the
    /// part root, since the resulting path would be unresolvable at runtime.
    /// </remarks>
    public sealed class TransformPathField : VisualElement
    {
        private const string PATH_SUFFIX = " Path";

        private readonly SerializedProperty _prop;
        private readonly Transform _partRoot;
        private readonly ObjectField _objectField;
        private readonly HelpBox _warning;

        public TransformPathField(SerializedProperty prop, string label, Transform partRoot)
        {
            _prop = prop;
            _partRoot = partRoot;

            AddToClassList("transform-path-field");

            var cleanLabel = label != null && label.EndsWith(PATH_SUFFIX)
                ? label.Substring(0, label.Length - PATH_SUFFIX.Length)
                : label;

            _objectField = new ObjectField(cleanLabel) { objectType = typeof(Transform), allowSceneObjects = true };
            _objectField.AddToClassList("unity-base-field__aligned");
            _objectField.SetValueWithoutNotify(ResolvePath(prop.stringValue));
            _objectField.RegisterValueChangedCallback(OnObjectChanged);
            _objectField.TrackPropertyValue(prop, OnPropertyChanged);
            Add(_objectField);

            _warning = new HelpBox("Transform is not under the part root.", HelpBoxMessageType.Warning);
            _warning.style.display = DisplayStyle.None;
            Add(_warning);
        }

        private Transform ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || _partRoot == null)
            {
                return null;
            }
            return _partRoot.Find(path);
        }

        private void OnObjectChanged(ChangeEvent<Object> evt)
        {
            var t = evt.newValue as Transform;
            string newPath;
            var unresolvable = false;

            if (t == null)
            {
                newPath = string.Empty;
            }
            else if (_partRoot == null || !t.IsChildOf(_partRoot))
            {
                newPath = string.Empty;
                unresolvable = true;
            }
            else
            {
                newPath = ComputeRelativePath(_partRoot, t);
            }

            _warning.style.display = unresolvable ? DisplayStyle.Flex : DisplayStyle.None;

            _prop.serializedObject.Update();
            _prop.stringValue = newPath;
            _prop.serializedObject.ApplyModifiedProperties();
        }

        private void OnPropertyChanged(SerializedProperty p)
        {
            var resolved = ResolvePath(p.stringValue);
            if (_objectField.value != resolved)
            {
                _objectField.SetValueWithoutNotify(resolved);
            }
        }

        private static string ComputeRelativePath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }
            var segments = new List<string>();
            var current = target;
            while (current != null && current != root)
            {
                segments.Add(current.name);
                current = current.parent;
            }
            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
