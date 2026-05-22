using System.Reflection;
using KSP.Modules;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Array-element renderer for <see cref="Data_Command.ControlPoint" /> lists. Replaces
    /// Unity's default foldout-of-elements with the shared <see cref="CardListSection" /> card
    /// pattern: each control point shows its Id in the header, with Position and Orientation as
    /// body rows that pick up SceneView handles via the standard <c>[SceneViewHandle]</c>
    /// dispatch.
    /// </summary>
    [FieldRenderer(typeof(Data_Command.ControlPoint), FieldRendererKind.ArrayElement)]
    internal sealed class ControlPointArrayRenderer : IFieldRenderer
    {
        private const BindingFlags FIELD_FLAGS =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <inheritdoc />
        public VisualElement Build(SerializedProperty arrayProp, string title)
        {
            return CardListSection.Build(arrayProp, new CardListSection.Config
            {
                Title = string.IsNullOrEmpty(title) ? "Control Points" : title,
                AddButtonText = "+ Add Control Point",
                IdentityFieldName = nameof(Data_Command.ControlPoint.Id),
                BuildBody = BuildControlPointBody,
            });
        }

        private static void BuildControlPointBody(SerializedProperty entry, VisualElement body)
        {
            foreach (var field in typeof(Data_Command.ControlPoint).GetFields(FIELD_FLAGS))
            {
                if (field.Name == nameof(Data_Command.ControlPoint.Id))
                {
                    continue;
                }
                if (field.IsDefined(typeof(HideInInspector), inherit: true))
                {
                    continue;
                }
                var prop = entry.FindPropertyRelative(field.Name);
                if (prop == null)
                {
                    continue;
                }
                var row = ReflectionModuleEditor.BuildFieldRowForCustomEditor(prop, field, partRoot: null);
                if (row != null)
                {
                    body.Add(row);
                }
            }
        }
    }
}
