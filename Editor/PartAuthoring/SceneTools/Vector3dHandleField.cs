using KSP;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.SceneTools
{
    /// <summary>
    /// Inspector field for a Vector3d that pairs a PropertyField with a SceneView handle picker button.
    /// </summary>
    /// <remarks>
    /// The PropertyField uses the registered Vector3d drawer (three DoubleField inputs). The
    /// button toggles a <see cref="SceneHandlePicker" /> session on the field; while engaged,
    /// a Unity Handle is drawn in the SceneView and dragged updates the field's value.
    /// </remarks>
    public sealed class Vector3dHandleField : VisualElement
    {
        /// <summary>
        /// Creates a handle-enabled Vector3d field.
        /// </summary>
        /// <param name="primary">The Vector3d property being edited.</param>
        /// <param name="target">The owning part. Provides the transform and Undo target.</param>
        /// <param name="mode">Position or Orientation handle kind.</param>
        /// <param name="anchor">Anchor position for Orientation handles. Ignored for Position handles.</param>
        public Vector3dHandleField(SerializedProperty primary, CorePartData target, SceneHandlePicker.HandleMode mode, SerializedProperty anchor = null)
        {
            AddToClassList("vector3d-handle-field");

            var field = new PropertyField(primary);
            field.AddToClassList("vector3d-handle-field__field");
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
