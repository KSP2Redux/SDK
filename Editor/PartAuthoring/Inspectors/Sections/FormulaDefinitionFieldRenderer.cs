using System.Reflection;
using KSP.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Direct-kind renderer for single-record <see cref="ResourceConverterFormulaDefinition" />
    /// fields (Mine today, any future single-recipe module). Iterates the record's fields and
    /// hand-dispatches each through <see cref="ReflectionModuleEditor.BuildFieldRowForCustomEditor" />
    /// so nested resource lists pick up the canonical PartModuleResourceTable instead of Unity's
    /// default array drawer.
    /// </summary>
    [FieldRenderer(typeof(ResourceConverterFormulaDefinition))]
    internal sealed class FormulaDefinitionFieldRenderer : IFieldRenderer
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty prop, string title)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");

            var header = new Label(string.IsNullOrEmpty(title) ? "Formula" : title);
            header.AddToClassList("data-editor-subsection-header");
            outer.Add(header);

            foreach (var field in typeof(ResourceConverterFormulaDefinition).GetFields(FIELD_FLAGS))
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
