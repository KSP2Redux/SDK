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
