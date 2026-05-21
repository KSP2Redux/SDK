using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Sections
{
    /// <summary>
    /// Cell widget kinds the table knows how to render.
    /// </summary>
    internal enum SerializedTableColumnKind
    {
        /// <summary>Single-line string cell backed by a <see cref="TextField" />.</summary>
        Text,
        /// <summary>Double-precision numeric cell backed by a <see cref="DoubleField" />.</summary>
        Double,
        /// <summary>Single-precision numeric cell backed by a <see cref="FloatField" />.</summary>
        Float,
        /// <summary>Integer numeric cell backed by an <see cref="IntegerField" />.</summary>
        Int,
        /// <summary>Boolean cell backed by a <see cref="Toggle" />.</summary>
        Toggle,
    }

    /// <summary>
    /// Describes one column in a <see cref="SerializedArrayTable" />.
    /// </summary>
    /// <remarks>
    /// A column is sized either by <see cref="Flex" /> (when > 0, the column grows to fill remaining width)
    /// or by <see cref="FixedWidth" /> (in pixels, when <see cref="Flex" /> is 0). The header label and the
    /// body cell are sized together so they stay column-aligned.
    /// </remarks>
    internal sealed class SerializedTableColumn
    {
        /// <summary>Header label shown above the column.</summary>
        public string HeaderLabel;
        /// <summary>Property name on the array element, passed to <see cref="SerializedProperty.FindPropertyRelative" />.</summary>
        public string PropertyName;
        /// <summary>Cell widget kind.</summary>
        public SerializedTableColumnKind Kind;
        /// <summary>Flex-grow weight. 0 means use <see cref="FixedWidth" />.</summary>
        public float Flex;
        /// <summary>Fixed cell width in pixels. Applies when <see cref="Flex" /> is 0.</summary>
        public float FixedWidth;
        /// <summary>Optional tooltip for both the header cell and the row cell.</summary>
        public string Tooltip;
    }

    /// <summary>
    /// Renders a <see cref="SerializedProperty" /> array as a table with a chrome header row,
    /// one data row per element, a per-row delete button, and a trailing add button.
    /// </summary>
    /// <remarks>
    /// Designed for arrays of small serializable types whose fields fit naturally as columns
    /// (resource containers, resource costs, etc.). Cells use bare bound fields rather than
    /// <see cref="PropertyField" /> to keep the row dense and avoid the aligned-label gutter.
    /// </remarks>
    internal sealed class SerializedArrayTable
    {
        private readonly SerializedObject _so;
        private readonly SerializedProperty _arrayProp;
        private readonly string _title;
        private readonly string _addButtonText;
        private readonly SerializedTableColumn[] _columns;
        private VisualElement _body;

        /// <summary>
        /// Creates a table bound to the array at <paramref name="arrayPath" />.
        /// </summary>
        /// <param name="so">The owning SerializedObject.</param>
        /// <param name="arrayPath">Property path to the array on <paramref name="so" />.</param>
        /// <param name="title">Optional bold title rendered above the header row. Pass null or empty to omit.</param>
        /// <param name="addButtonText">Label for the trailing add button.</param>
        /// <param name="columns">Column specs in left-to-right order.</param>
        public SerializedArrayTable(
            SerializedObject so,
            string arrayPath,
            string title,
            string addButtonText,
            SerializedTableColumn[] columns)
        {
            _so = so;
            _arrayProp = so.FindProperty(arrayPath);
            _title = title;
            _addButtonText = addButtonText;
            _columns = columns;
        }

        /// <summary>
        /// Builds the table's VisualElement tree.
        /// </summary>
        public VisualElement Build()
        {
            var outer = new VisualElement();
            outer.AddToClassList("serialized-array-table");

            if (!string.IsNullOrEmpty(_title))
            {
                var titleLabel = new Label(_title);
                titleLabel.AddToClassList("serialized-array-table__title");
                outer.Add(titleLabel);
            }

            outer.Add(BuildHeaderRow());

            _body = new VisualElement();
            _body.AddToClassList("serialized-array-table__body");
            outer.Add(_body);

            Refresh();
            return outer;
        }

        /// <summary>
        /// Rebuilds the data rows from the current array state. Call after add/remove.
        /// </summary>
        public void Refresh()
        {
            if (_body == null || _arrayProp == null)
            {
                return;
            }
            _body.Clear();
            for (int i = 0; i < _arrayProp.arraySize; i++)
            {
                _body.Add(BuildDataRow(i));
            }
            _body.Add(BuildAddButton());
        }

        private VisualElement BuildHeaderRow()
        {
            var header = new VisualElement();
            header.AddToClassList("serialized-array-table__header");

            foreach (var col in _columns)
            {
                var cell = new Label(col.HeaderLabel);
                cell.AddToClassList("serialized-array-table__header-cell");
                if (!string.IsNullOrEmpty(col.Tooltip))
                {
                    cell.tooltip = col.Tooltip;
                }
                ApplyCellSize(cell, col);
                header.Add(cell);
            }

            var deleteHeader = new VisualElement();
            deleteHeader.AddToClassList("serialized-array-table__delete-col");
            header.Add(deleteHeader);

            return header;
        }

        private VisualElement BuildDataRow(int index)
        {
            var row = new VisualElement();
            row.AddToClassList("serialized-array-table__row");

            var element = _arrayProp.GetArrayElementAtIndex(index);

            foreach (var col in _columns)
            {
                var cell = BuildCell(element, col);
                cell.AddToClassList("serialized-array-table__cell");
                if (!string.IsNullOrEmpty(col.Tooltip))
                {
                    cell.tooltip = col.Tooltip;
                }
                ApplyCellSize(cell, col);
                row.Add(cell);
            }

            int captured = index;
            var removeBtn = new Button(() => RemoveAt(captured))
            {
                text = "X",
                tooltip = "Remove this row.",
            };
            removeBtn.AddToClassList("serialized-array-table__remove-btn");
            removeBtn.AddToClassList("serialized-array-table__delete-col");
            row.Add(removeBtn);

            return row;
        }

        private static VisualElement BuildCell(SerializedProperty element, SerializedTableColumn col)
        {
            var prop = element.FindPropertyRelative(col.PropertyName);
            if (prop == null)
            {
                return new Label("(missing)");
            }
            switch (col.Kind)
            {
                case SerializedTableColumnKind.Text:
                {
                    var field = new TextField { isDelayed = true };
                    field.BindProperty(prop);
                    return field;
                }
                case SerializedTableColumnKind.Double:
                {
                    var field = new DoubleField { isDelayed = true };
                    field.BindProperty(prop);
                    return field;
                }
                case SerializedTableColumnKind.Float:
                {
                    var field = new FloatField { isDelayed = true };
                    field.BindProperty(prop);
                    return field;
                }
                case SerializedTableColumnKind.Int:
                {
                    var field = new IntegerField { isDelayed = true };
                    field.BindProperty(prop);
                    return field;
                }
                case SerializedTableColumnKind.Toggle:
                {
                    var field = new Toggle();
                    field.BindProperty(prop);
                    return field;
                }
                default:
                {
                    return new Label("?");
                }
            }
        }

        private Button BuildAddButton()
        {
            var btn = new Button(AddOne) { text = _addButtonText };
            btn.AddToClassList("serialized-array-table__add-btn");
            return btn;
        }

        private void AddOne()
        {
            if (_arrayProp == null)
            {
                return;
            }
            _arrayProp.arraySize++;
            _so.ApplyModifiedProperties();
            Refresh();
        }

        private void RemoveAt(int index)
        {
            if (_arrayProp == null || index < 0 || index >= _arrayProp.arraySize)
            {
                return;
            }
            _arrayProp.DeleteArrayElementAtIndex(index);
            _so.ApplyModifiedProperties();
            Refresh();
        }

        private static void ApplyCellSize(VisualElement el, SerializedTableColumn col)
        {
            if (col.Flex > 0f)
            {
                el.style.flexGrow = col.Flex;
                el.style.flexShrink = 1f;
                el.style.flexBasis = 0f;
            }
            else
            {
                el.style.width = col.FixedWidth;
                el.style.flexShrink = 0f;
                el.style.flexGrow = 0f;
            }
        }
    }
}
