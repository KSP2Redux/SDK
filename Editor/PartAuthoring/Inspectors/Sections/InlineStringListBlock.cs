using System;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors;
using UnityEditor;
using UnityEngine.UIElements;
using Ksp2UnityTools.Editor.Widgets;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Builds an inline list of plain string rows for any <c>string[]</c> or <c>List&lt;string&gt;</c>
    /// SerializedProperty. Replaces Unity's default foldout-per-element drawer with a TextField
    /// plus delete-button per entry.
    /// </summary>
    internal static class InlineStringListBlock
    {
        /// <summary>
        /// Builds the inline string list block for the given array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">The string-array SerializedProperty to render.</param>
        /// <param name="title">Header title. The element count is appended in parentheses.</param>
        /// <param name="buildField">Builds the field for one entry, or null for a plain text field.</param>
        /// <returns>The built block element.</returns>
        public static VisualElement Build(
            SerializedProperty arrayProp,
            string title,
            Func<SerializedProperty, VisualElement> buildField = null)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-block");
            outer.Add(InlineListBlock.Build(
                arrayProp,
                titleFormat: title + " ({0})",
                addButtonText: "+ Add",
                emptyHint: "(none)",
                rowBuilder: (entry, index, onDelete) => BuildStringRow(entry, onDelete, buildField)));
            return outer;
        }

        private static VisualElement BuildStringRow(
            SerializedProperty entry,
            Action onDelete,
            Func<SerializedProperty, VisualElement> buildField)
        {
            var row = new VisualElement();
            row.AddToClassList("data-editor-inline-row");

            VisualElement field = buildField?.Invoke(entry) ?? BuildTextField(entry);
            field.AddToClassList("data-editor-inline-row__grow");
            row.Add(field);

            var removeBtn = new Button(onDelete) { text = "X" };
            removeBtn.AddToClassList("data-editor-card-remove-btn");
            row.Add(removeBtn);

            return row;
        }

        private static TextField BuildTextField(SerializedProperty entry)
        {
            var textField = new TextField { value = entry.stringValue, isDelayed = true };
            textField.RegisterValueChangedCallback(evt =>
            {
                entry.serializedObject.Update();
                entry.stringValue = evt.newValue ?? string.Empty;
                entry.serializedObject.ApplyModifiedProperties();
            });
            return textField;
        }
    }
}
