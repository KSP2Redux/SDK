using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Renders a shader controller's property bag as the controls it describes.
    /// </summary>
    /// <remarks>
    /// <see cref="SerializedControllerProperty" /> is self describing: it carries a type, a display
    /// name, a description and a value slot per type, with a range for the numeric ones. Each
    /// descriptor is read and turned into the one control it asks for.
    ///
    /// Which properties exist is decided by the shader controller, which probes the material and
    /// declares only what the shader has, so the list is fixed in structure and carries no add or
    /// remove buttons. The values in it are authored and persist.
    ///
    /// Properties named in <see cref="Suppressed" /> are dropped. They are declared unconditionally by
    /// the controller and consumed by nothing.
    /// </remarks>
    internal static class ControllerPropertyList
    {
        /// <summary>
        /// Property names to hide because nothing reads them.
        /// </summary>
        /// <remarks>
        /// <c>KSPScatterController</c> adds both as sliders and writes them to the material without
        /// checking, while guarding <c>_Metallic</c> and <c>_Specular</c> with <c>HasProperty</c>.
        /// Neither scatter shader in this project declares either one, so the writes are dropped and
        /// reads come back zero.
        /// </remarks>
        private static readonly HashSet<string> Suppressed = new() { "BlendDistance", "BlendStrength" };

        /// <summary>
        /// Builds the property controls into <paramref name="host" />.
        /// </summary>
        /// <param name="host">Element to populate. Emptied first.</param>
        /// <param name="propertyList">The <c>ControlerPropertyList</c> array property.</param>
        public static void Build(VisualElement host, SerializedProperty propertyList)
        {
            if (host == null)
                return;

            host.Clear();
            if (propertyList == null || !propertyList.isArray)
                return;

            for (var i = 0; i < propertyList.arraySize; i++)
            {
                VisualElement row = BuildRow(propertyList.GetArrayElementAtIndex(i));
                if (row != null)
                {
                    host.Add(row);
                }
            }
        }

        private static VisualElement BuildRow(SerializedProperty entry)
        {
            SerializedProperty typeProperty = entry.FindPropertyRelative("SerializedControlerPropertyType");
            SerializedProperty name = entry.FindPropertyRelative("PropertyName");
            SerializedProperty description = entry.FindPropertyRelative("PropertyDescription");
            if (typeProperty == null || name == null)
                return null;

            if (Suppressed.Contains(name.stringValue))
                return null;

            var type = (SerializedControlerPropertyType)typeProperty.enumValueIndex;
            string label = string.IsNullOrWhiteSpace(description?.stringValue)
                ? name.stringValue
                : description.stringValue;

            // The description doubles as the label, so the tooltip carries the underlying name. That
            // is the string an author needs when cross referencing a shader property or a log line.
            string tooltip = $"Shader controller property '{name.stringValue}'.";

            switch (type)
            {
                case SerializedControlerPropertyType.Label:
                    var heading = new Label(label);
                    heading.AddToClassList("sdk-hint");
                    return heading;

                case SerializedControlerPropertyType.Float:
                    return AsAlignedField(
                        BuildFloat(entry, label),
                        tooltip);

                case SerializedControlerPropertyType.Integer:
                    return AsAlignedField(
                        BuildInteger(entry, label),
                        tooltip);

                case SerializedControlerPropertyType.ColorSelector:
                    return AsAlignedField(Field<ColorField>(entry, "ColorValue", label), tooltip);

                case SerializedControlerPropertyType.Boolean:
                    return AsAlignedField(Field<Toggle>(entry, "BoolValue", label), tooltip);

                case SerializedControlerPropertyType.Texture2D:
                    return AsAlignedField(Field<ObjectField>(entry, "TextureValue", label), tooltip);

                default:
                    // RgbaSelector and DropDownStringList are declared by controllers this project
                    // does not use. Left as a plain integer.
                    return AsAlignedField(Field<IntegerField>(entry, "IntValue", label), tooltip);
            }
        }

        private static VisualElement BuildFloat(SerializedProperty entry, string label)
        {
            SerializedProperty value = entry.FindPropertyRelative("FloatValue");
            float minimum = entry.FindPropertyRelative("FloatMinValue")?.floatValue ?? 0f;
            float maximum = entry.FindPropertyRelative("FloatMaxValue")?.floatValue ?? 0f;

            // A degenerate range means the controller declared no bounds, so a slider would pin the
            // value to one number. Fall back to a plain field.
            if (maximum <= minimum)
                return Field<FloatField>(entry, "FloatValue", label);

            var slider = new Slider(label, minimum, maximum) { showInputField = true };
            slider.BindProperty(value);
            return slider;
        }

        private static VisualElement BuildInteger(SerializedProperty entry, string label)
        {
            SerializedProperty value = entry.FindPropertyRelative("IntValue");
            int minimum = entry.FindPropertyRelative("IntMinValue")?.intValue ?? 0;
            int maximum = entry.FindPropertyRelative("IntMaxValue")?.intValue ?? 0;

            if (maximum <= minimum)
                return Field<IntegerField>(entry, "IntValue", label);

            var slider = new SliderInt(label, minimum, maximum) { showInputField = true };
            slider.BindProperty(value);
            return slider;
        }

        private static VisualElement Field<T>(SerializedProperty entry, string relative, string label)
            where T : VisualElement, IBindable, new()
        {
            var field = new T();
            if (field is ObjectField objectField)
            {
                objectField.objectType = typeof(Texture2D);
            }

            SetLabel(field, label);
            SerializedProperty value = entry.FindPropertyRelative(relative);
            if (value != null)
            {
                field.BindProperty(value);
            }

            return field;
        }

        private static void SetLabel(VisualElement field, string label)
        {
            switch (field)
            {
                case FloatField floatField: floatField.label = label; break;
                case IntegerField integerField: integerField.label = label; break;
                case Toggle toggle: toggle.label = label; break;
                case ColorField colorField: colorField.label = label; break;
                case ObjectField objectField: objectField.label = label; break;
            }
        }

        private static VisualElement AsAlignedField(VisualElement field, string tooltip)
        {
            field.tooltip = tooltip;
            field.AddToClassList("unity-base-field__aligned");
            return field;
        }
    }
}
