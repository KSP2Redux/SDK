using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Drawers
{
    /// <summary>
    /// Property drawer for <see cref="Vector3d" /> that renders X, Y, Z inline as a single row,
    /// mirroring Unity's built-in Vector3 inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(Vector3d))]
    public class Vector3dDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var field = new Vector3dField(property.displayName);
            field.AddToClassList("unity-base-field__aligned");

            field.AddAxisField("X", property.FindPropertyRelative("x"));
            field.AddAxisField("Y", property.FindPropertyRelative("y"));
            field.AddAxisField("Z", property.FindPropertyRelative("z"));

            return field;
        }

        private sealed class Vector3dField : BaseField<Vector3d>
        {
            private readonly VisualElement _inputRow;

            public Vector3dField(string label) : base(label, new VisualElement())
            {
                AddToClassList("unity-composite-field");
                _inputRow = this.Q(className: "unity-base-field__input");
                _inputRow.style.flexDirection = FlexDirection.Row;
            }

            public void AddAxisField(string axisLabel, SerializedProperty prop)
            {
                var axis = new DoubleField(axisLabel);
                axis.BindProperty(prop);
                axis.AddToClassList("unity-composite-field__field");
                axis.style.flexGrow = 1;
                axis.style.flexBasis = 0;
                axis.style.minWidth = 0;
                axis.style.marginLeft = 2;

                axis.labelElement.style.minWidth = 12;
                axis.labelElement.style.width = 12;
                axis.labelElement.style.flexShrink = 0;
                axis.labelElement.style.unityTextAlign = TextAnchor.MiddleCenter;

                _inputRow.Add(axis);
            }
        }
    }
}
