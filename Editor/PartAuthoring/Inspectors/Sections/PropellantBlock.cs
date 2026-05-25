using System;
using KSP.Sim.ResourceSystem;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Builds the shared author-facing layout for any <c>PropellantDefinition</c> field.
    /// Mixture (ResourceName autocomplete), multiplier, ignore-thrust-curve, and an ingredient
    /// overrides table with autocomplete + units + flow-mode per row.
    /// </summary>
    /// <remarks>
    /// Lifted from <c>EngineDataEditor</c> so RCS and any other future consumer of
    /// <c>PropellantDefinition</c> pick up the same surface via the
    /// <see cref="FieldRendererRegistry" /> dispatch.
    /// </remarks>
    internal static class PropellantBlock
    {
        /// <summary>
        /// Builds the propellant block for the given SerializedProperty.
        /// </summary>
        /// <param name="propellantProp">The <c>PropellantDefinition</c> SerializedProperty.</param>
        /// <param name="title">Title shown above the block. Pass null or empty for "Propellant".</param>
        /// <returns>The built block element.</returns>
        public static VisualElement Build(SerializedProperty propellantProp, string title = null)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");

            var header = new Label(string.IsNullOrEmpty(title) ? "Propellant" : title);
            header.AddToClassList("data-editor-subsection-header");
            outer.Add(header);

            var mixtureProp = propellantProp.FindPropertyRelative("mixtureName");
            if (mixtureProp != null)
            {
                outer.Add(new ResourceNameField(mixtureProp, "Mixture"));
            }

            var multiplierProp = propellantProp.FindPropertyRelative("mixtureMultiplier");
            if (multiplierProp != null)
            {
                var pf = new PropertyField(multiplierProp, "Multiplier");
                pf.AddToClassList("unity-base-field__aligned");
                outer.Add(pf);
            }

            var ignoreProp = propellantProp.FindPropertyRelative("ignoreForThrustCurve");
            if (ignoreProp != null)
            {
                var pf = new PropertyField(ignoreProp, "Ignore For Thrust Curve");
                pf.AddToClassList("unity-base-field__aligned");
                outer.Add(pf);
            }

            var overridesProp = propellantProp.FindPropertyRelative("ingredientOverrides");
            if (overridesProp != null)
            {
                outer.Add(BuildIngredientOverridesTable(overridesProp));
            }

            return outer;
        }

        private static VisualElement BuildIngredientOverridesTable(SerializedProperty arrayProp)
        {
            return InlineListBlock.Build(
                arrayProp,
                titleFormat: "Ingredient Overrides ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none - recipe defaults apply)",
                rowBuilder: BuildIngredientOverrideRow);
        }

        private static VisualElement BuildIngredientOverrideRow(SerializedProperty entry, int index, Action onDelete)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            var nameProp = entry.FindPropertyRelative("name");
            VisualElement nameField;
            if (nameProp != null)
            {
                nameField = new ResourceNameField(nameProp, string.Empty);
                nameField.AddToClassList("data-editor-inline-row__grow");
            }
            else
            {
                nameField = new Label("(missing name)");
            }
            row.Add(nameField);

            var unitsProp = entry.FindPropertyRelative("unitsPerRecipeUnit");
            var unitsField = new DoubleField { value = unitsProp?.doubleValue ?? 0.0, isDelayed = true };
            unitsField.AddToClassList("data-editor-inline-row__cell-units");
            if (unitsProp != null)
            {
                unitsField.RegisterValueChangedCallback(evt =>
                {
                    unitsProp.serializedObject.Update();
                    unitsProp.doubleValue = evt.newValue;
                    unitsProp.serializedObject.ApplyModifiedProperties();
                });
            }
            row.Add(unitsField);

            var flowProp = entry.FindPropertyRelative("flowMode");
            if (flowProp != null)
            {
                var flowField = new EnumField((ResourceFlowMode)flowProp.enumValueIndex);
                flowField.AddToClassList("data-editor-inline-row__cell-flow");
                flowField.RegisterValueChangedCallback(evt =>
                {
                    flowProp.serializedObject.Update();
                    flowProp.enumValueIndex = (int)(ResourceFlowMode)evt.newValue;
                    flowProp.serializedObject.ApplyModifiedProperties();
                });
                row.Add(flowField);
            }

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }
    }
}
