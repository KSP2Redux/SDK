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
    /// Mutations are surgical and state-preserving:
    /// - Add appends a single row at the end of the array.
    /// - Remove deletes the array element at the row's CURRENT visual position
    ///   (<see cref="VisualElement.IndexOf" />), then rebuilds every subsequent sibling row
    ///   at its new index. Row content is re-created (lose transient cursor focus) but the
    ///   data bindings are correct after the shift.
    /// </remarks>
    public static class InlineListBlock
    {
        /// <summary>
        /// Builds an inline list block for the given array SerializedProperty.
        /// </summary>
        /// <param name="arrayProp">The array SerializedProperty to render.</param>
        /// <param name="titleFormat">Format string for the header. The current element count is substituted at <c>{0}</c>.</param>
        /// <param name="addButtonText">Label for the trailing add button.</param>
        /// <param name="emptyHint">Hint label shown in place of rows when the array is empty.</param>
        /// <param name="rowBuilder">Row builder invoked once per element with the element's SerializedProperty, its index, and a delete callback.</param>
        /// <param name="onAdd">Optional seed callback invoked on the new entry after the add button extends the array.</param>
        /// <returns>The built block element.</returns>
        public static VisualElement Build(
            SerializedProperty arrayProp,
            string titleFormat,
            string addButtonText,
            string emptyHint,
            Func<SerializedProperty, int, Action, VisualElement> rowBuilder,
            Action<SerializedProperty> onAdd = null)
        {
            var outer = new VisualElement();
            outer.AddToClassList("data-editor-inline-list");

            var headerRow = new VisualElement();
            headerRow.AddToClassList("data-editor-inline-list__header");

            var countLabel = new Label();
            countLabel.AddToClassList("data-editor-subsection-header");
            countLabel.AddToClassList("data-editor-inline-list__count");
            headerRow.Add(countLabel);

            var addBtn = new Button { text = addButtonText };
            addBtn.AddToClassList("data-editor-inline-list__add-btn");
            headerRow.Add(addBtn);
            outer.Add(headerRow);

            var rows = new VisualElement();
            outer.Add(rows);

            var emptyLabel = new Label(emptyHint);
            emptyLabel.AddToClassList("data-editor-empty-hint");

            void UpdateCount()
            {
                countLabel.text = string.Format(titleFormat, arrayProp.arraySize);
                if (arrayProp.arraySize == 0)
                {
                    if (emptyLabel.parent == null)
                    {
                        rows.Add(emptyLabel);
                    }
                }
                else if (emptyLabel.parent != null)
                {
                    emptyLabel.RemoveFromHierarchy();
                }
            }

            VisualElement CreateRow(int index)
            {
                if (index < 0 || index >= arrayProp.arraySize)
                {
                    return null;
                }
                var entry = arrayProp.GetArrayElementAtIndex(index);
                VisualElement row = null;
                Action onDelete = () =>
                {
                    int currentIndex = rows.IndexOf(row);
                    if (currentIndex < 0)
                    {
                        return;
                    }
                    arrayProp.serializedObject.Update();
                    if (currentIndex >= arrayProp.arraySize)
                    {
                        return;
                    }
                    arrayProp.DeleteArrayElementAtIndex(currentIndex);
                    arrayProp.serializedObject.ApplyModifiedProperties();
                    rows.Remove(row);
                    arrayProp.serializedObject.Update();
                    // Rebuild subsequent rows with their new indexes so bindings stay correct.
                    for (int i = currentIndex; i < rows.childCount; i++)
                    {
                        VisualElement old = rows.ElementAt(i);
                        if (old == emptyLabel)
                        {
                            continue;
                        }
                        VisualElement replacement = CreateRow(i);
                        if (replacement != null)
                        {
                            rows.Insert(i, replacement);
                            rows.Remove(old);
                        }
                    }
                    UpdateCount();
                };
                row = rowBuilder(entry, index, onDelete);
                return row;
            }

            // Initial population.
            for (var i = 0; i < arrayProp.arraySize; i++)
            {
                VisualElement row = CreateRow(i);
                if (row != null)
                {
                    rows.Add(row);
                }
            }
            UpdateCount();

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
                arrayProp.serializedObject.Update();
                VisualElement newRow = CreateRow(newIndex);
                if (newRow != null)
                {
                    rows.Add(newRow);
                }
                UpdateCount();
            };

            return outer;
        }
    }
}
