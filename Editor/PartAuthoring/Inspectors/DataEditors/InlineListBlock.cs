using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.DataEditors
{
    /// <summary>
    /// Reusable scaffold for the "header row + add button + rows container + empty hint" pattern
    /// that custom <see cref="IDataEditor" /> implementations use to render array-typed fields
    /// as inline card-style lists, replacing Unity's default ReorderableList chrome.
    /// </summary>
    /// <remarks>
    /// Only the per-row layout varies between call sites; the surrounding scaffold is identical.
    /// Pass a row builder delegate that constructs one row given the element property, index,
    /// and an <c>onChanged</c> callback to invoke after a delete or external mutation.
    /// </remarks>
    public static class InlineListBlock
    {
        /// <summary>
        /// Builds an inline list block bound to an array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">The array SerializedProperty to render.</param>
        /// <param name="titleFormat">Title format string taking the element count as <c>{0}</c>
        /// (e.g. <c>"Engine Modes ({0})"</c>).</param>
        /// <param name="addButtonText">Text on the add button (e.g. <c>"+ Add"</c>).</param>
        /// <param name="emptyHint">Italic-gray hint shown when the array is empty.</param>
        /// <param name="rowBuilder">Builds one row for an array element. Receives the element
        /// SerializedProperty, the index, and an <c>onDelete</c> callback that removes the
        /// element from the array and refreshes the list. Wire the remove button to this
        /// callback.</param>
        /// <param name="onAdd">Optional post-add hook to seed a new element with defaults
        /// (e.g. give it a unique name). Called with the new element's SerializedProperty inside
        /// an Update/ApplyModifiedProperties pair.</param>
        public static VisualElement Build(
            SerializedProperty arrayProp,
            string titleFormat,
            string addButtonText,
            string emptyHint,
            Func<SerializedProperty, int, Action, VisualElement> rowBuilder,
            Action<SerializedProperty> onAdd = null)
        {
            var outer = new VisualElement();
            outer.style.marginTop = 4f;
            outer.style.marginBottom = 4f;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 2f;

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-subsection-header");
            countLabel.style.flexGrow = 1f;
            countLabel.style.marginBottom = 0;
            headerRow.Add(countLabel);

            var addBtn = new Button { text = addButtonText };
            addBtn.style.flexShrink = 0;
            headerRow.Add(addBtn);
            outer.Add(headerRow);

            var rows = new VisualElement();
            outer.Add(rows);

            void Refresh()
            {
                countLabel.text = string.Format(titleFormat, arrayProp.arraySize);
                rows.Clear();
                if (arrayProp.arraySize == 0)
                {
                    var empty = new Label(emptyHint);
                    empty.AddToClassList("data-editor-empty-hint");
                    rows.Add(empty);
                    return;
                }
                for (var i = 0; i < arrayProp.arraySize; i++)
                {
                    var entry = arrayProp.GetArrayElementAtIndex(i);
                    var capturedIndex = i;
                    Action onDelete = () =>
                    {
                        arrayProp.serializedObject.Update();
                        arrayProp.DeleteArrayElementAtIndex(capturedIndex);
                        arrayProp.serializedObject.ApplyModifiedProperties();
                        Refresh();
                    };
                    var row = rowBuilder(entry, i, onDelete);
                    if (row != null)
                    {
                        rows.Add(row);
                    }
                }
            }

            addBtn.clicked += () =>
            {
                arrayProp.serializedObject.Update();
                var newIndex = arrayProp.arraySize;
                arrayProp.arraySize++;
                arrayProp.serializedObject.ApplyModifiedProperties();
                if (onAdd != null)
                {
                    arrayProp.serializedObject.Update();
                    onAdd(arrayProp.GetArrayElementAtIndex(newIndex));
                    arrayProp.serializedObject.ApplyModifiedProperties();
                }
                Refresh();
            };

            Refresh();
            return outer;
        }
    }
}
