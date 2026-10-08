using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.SceneTools
{
    /// <summary>
    /// Inspector field for a Vector3 or Vector3d that pairs a PropertyField with a SceneView handle
    /// picker button.
    /// </summary>
    /// <remarks>
    /// The PropertyField uses Unity's stock drawer for the value type (three FloatField inputs for Vector3, three DoubleField inputs for Vector3d). The button toggles a <see cref="SceneHandlePicker" /> session on the field. While engaged, a Unity Handle is drawn in the SceneView and dragging updates the field's value.
    /// </remarks>
    public sealed class VectorHandleField : VisualElement
    {
        /// <summary>
        /// Creates a handle-enabled vector field.
        /// </summary>
        /// <param name="primary">The Vector3 or Vector3d property being edited.</param>
        /// <param name="target">The Component the property lives on. Provides the SerializedObject and Undo target.</param>
        /// <param name="mode">Position or Orientation handle kind.</param>
        /// <param name="anchor">Anchor position for Orientation handles. Ignored for Position handles.</param>
        /// <param name="label">Overrides the PropertyField label. Pass an empty string to hide it (useful in table cells where the column header names the field). Pass null to keep the property's default display name.</param>
        public VectorHandleField(SerializedProperty primary, Component target, SceneHandlePicker.HandleMode mode, SerializedProperty anchor = null, string label = null)
        {
            AddToClassList("vector3d-handle-field");

            var field = label == null ? new PropertyField(primary) : new PropertyField(primary, label);
            field.AddToClassList("vector3d-handle-field__field");
            field.AddToClassList("unity-base-field__aligned");
            Add(field);

            var button = new Button(() => SceneHandlePicker.Engage(target, primary, mode, anchor))
            {
                text = mode == SceneHandlePicker.HandleMode.Position ? "Scene" : "Rotate",
                tooltip = mode == SceneHandlePicker.HandleMode.Position
                    ? "Drag this position with a SceneView handle. Click again to disengage."
                    : "Drag this direction with a SceneView rotation handle. Click again to disengage.",
            };
            button.AddToClassList("vector3d-handle-field__button");
            Add(button);

            string activePath = primary.propertyPath;
            void Refresh()
            {
                button.EnableInClassList("is-active", SceneHandlePicker.ActivePath == activePath);
            }
            Refresh();
            SceneHandlePicker.OnActiveChanged += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => SceneHandlePicker.OnActiveChanged -= Refresh);
        }
    }
}
