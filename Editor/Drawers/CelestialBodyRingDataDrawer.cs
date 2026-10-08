using KSP.Sim.Definitions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Drawers
{
    /// <summary>
    /// Property drawer for <see cref="CelestialBodyRingData" /> that renders the inner and outer radii inline as a single
    /// row, with the density curve below.
    /// </summary>
    [CustomPropertyDrawer(typeof(CelestialBodyRingData))]
    public class CelestialBodyRingDataDrawer : PropertyDrawer
    {
        /// <inheritdoc />
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            var radii = new VisualElement();
            radii.style.flexDirection = FlexDirection.Row;
            root.Add(radii);

            radii.Add(MakeRadiusField("Inner Radius (m)", property.FindPropertyRelative("innerRadius")));
            radii.Add(MakeRadiusField("Outer Radius (m)", property.FindPropertyRelative("outerRadius")));

            var density = new PropertyField(property.FindPropertyRelative("density"), "Density Curve");
            root.Add(density);

            return root;
        }

        private static DoubleField MakeRadiusField(string label, SerializedProperty prop)
        {
            var field = new DoubleField(label);
            field.BindProperty(prop);
            field.style.flexGrow = 1;
            field.style.flexBasis = 0;
            field.style.minWidth = 0;
            field.style.marginLeft = 2;
            return field;
        }
    }
}
