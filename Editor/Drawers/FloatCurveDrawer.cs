using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Drawers
{
    /// <summary>
    /// Property drawer for <see cref="FloatCurve" /> that renders the underlying AnimationCurve inline.
    /// The cached _minTime / _maxTime fields are runtime-derived and hidden from the inspector.
    /// </summary>
    [CustomPropertyDrawer(typeof(FloatCurve))]
    public class FloatCurveDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty curveProp = property.FindPropertyRelative("fCurve");
            if (curveProp == null)
            {
                return new Label(property.displayName + " (FloatCurve has no fCurve field)");
            }

            var field = new CurveField(property.displayName);
            field.AddToClassList("unity-base-field__aligned");
            field.BindProperty(curveProp);
            return field;
        }
    }
}
