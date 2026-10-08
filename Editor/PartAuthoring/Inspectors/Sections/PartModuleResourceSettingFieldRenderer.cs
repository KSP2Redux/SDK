using System.Reflection;
using KSP.Sim.Definitions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Direct-kind renderer for single-record <see cref="PartModuleResourceSetting" /> fields
    /// (Data_ModuleGenerator.ResourceSetting, Data_WheelMotor.requiredResource,
    /// Data_Light.requiredResource, Data_SolarPanel.ResourceSettings, and any future single
    /// embedded resource setting). Iterates the record's fields and hand-dispatches each
    /// through <see cref="ReflectionModuleEditor.BuildFieldRowForCustomEditor" /> so the
    /// <c>[ResourceName]</c> autocomplete and <c>[Unit("u/s")]</c> suffix fire automatically.
    /// </summary>
    /// <remarks>
    /// The list-element variant lives at <see cref="PartModuleResourceListRenderer" /> and uses
    /// the column-table layout. This Direct variant uses the same vertical inline-block layout
    /// as <see cref="FormulaDefinitionFieldRenderer" /> so single records read consistently with
    /// other embedded structs in the inspector.
    /// </remarks>
    [FieldRenderer(typeof(PartModuleResourceSetting))]
    internal sealed class PartModuleResourceSettingFieldRenderer : IFieldRenderer
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty prop, string title)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");

            var header = new Label(string.IsNullOrEmpty(title) ? "Resource" : title);
            header.AddToClassList("data-editor-subsection-header");
            outer.Add(header);

            foreach (var field in typeof(PartModuleResourceSetting).GetFields(FIELD_FLAGS))
            {
                if (field.IsDefined(typeof(HideInInspector), inherit: true))
                {
                    continue;
                }
                var childProp = prop.FindPropertyRelative(field.Name);
                if (childProp == null)
                {
                    continue;
                }
                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(childProp, field, partRoot: null);
                if (row != null)
                {
                    outer.Add(row);
                }
            }

            return outer;
        }
    }
}
