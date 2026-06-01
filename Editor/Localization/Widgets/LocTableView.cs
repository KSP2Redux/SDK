using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.Localization.Widgets
{
    /// <summary>
    /// Resizable, hideable spreadsheet-style table for displaying and editing localization rows.
    /// </summary>
    /// <remarks>
    /// Renders a header row plus a virtualized body backed by a <see cref="ListView" />, with a sticky-left
    /// frozen column and a horizontal scroller that keeps header and body in sync. Columns can be
    /// drag-resized at their right edge, hidden through the Columns dropdown, renamed inline (non-protected
    /// columns only), and inserted or deleted through the header context menu. Rows can be inserted or
    /// deleted through the row context menu. Cell edits mutate the row model in place and raise
    /// <see cref="RowEdited" />. Column widths and hidden state persist per-file via EditorPrefs.
    /// </remarks>
    public sealed class LocTableView : VisualElement
    {
        /// <summary>
        /// Raised after a cell edit has been committed to the row model.
        /// </summary>
        public event Action<LocRow> RowEdited;

        private const float ResizeHandleWidth = 4f;
        private const float FrozenWidthSafetyMargin = 200f;
        private const long PersistDebounceMs = 250L;
        private const string PrefsKeyPrefix = "Redux.Localization.TableLayout.";
        private const int ProtectedColumnCount = 3;
        private const string StyleSheetPath = "/Assets/Windows/Localization/Widgets/LocTableView.uss";
        private static StyleSheet _cachedStyleSheet;

        private readonly List<LocColumnSpec> _columns;
        private readonly List<LocRow> _rows;
        private readonly string _prefsKey;

        private VisualElement _headerRow;
        private VisualElement _frozenHeaderContainer;
        private VisualElement _bodyHeaderViewport;
        private VisualElement _bodyHeaderContent;
        private ListView _bodyListView;
        private Scroller _horizontalScroller;

        private float _scrollX;
        private float _bodyContentWidth;
        private float _bodyViewportWidth;
        private bool _persistScheduled;

        // Active resize-drag state. Pointer capture target is the dragged handle element.
        private LocColumnSpec _resizingColumn;
        private float _resizeStartPointerX;
        private float _resizeStartWidth;
        private VisualElement _resizeHandleCapture;

        /// <summary>
        /// Constructs a LocTableView bound to the given columns and rows.
        /// </summary>
        /// <param name="columns">Ordered column specs. Mutated in place by resize and hide operations.</param>
        /// <param name="rows">Rows backing the table body. Cell edits mutate this list in place.</param>
        /// <param name="filePathForPersistence">Path scoping the per-file EditorPrefs key. Use "__sandbox__" for dev sandboxes.</param>
        public LocTableView(IList<LocColumnSpec> columns, IList<LocRow> rows, string filePathForPersistence)
        {
            _columns = new List<LocColumnSpec>(columns ?? Array.Empty<LocColumnSpec>());
            _rows = new List<LocRow>(rows ?? Array.Empty<LocRow>());
            _prefsKey = ComposePrefsKey(filePathForPersistence);

            AddToClassList("loctable");
            LoadStyleSheet();
            InitializeColumnWidths();
            if (_rows.Count == 0)
            {
                _rows.Add(new LocRow());
            }
            BuildLayout();
            LoadPrefs();
            ApplyAllColumnVisibility();
            ApplyAllColumnWidths();

            RegisterCallback<GeometryChangedEvent>(_ => OnGeometryChanged());
        }

        /// <summary>
        /// Rebuilds the visible rows from the backing list.
        /// </summary>
        /// <remarks>
        /// Call after row data changes outside the table.
        /// </remarks>
        public void Refresh()
        {
            _bodyListView?.Rebuild();
            UpdateScrollRange();
        }

        #region Layout construction

        private void LoadStyleSheet()
        {
            if (_cachedStyleSheet == null)
            {
                _cachedStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + StyleSheetPath);
            }
            if (_cachedStyleSheet != null)
            {
                styleSheets.Add(_cachedStyleSheet);
            }
        }

        private void InitializeColumnWidths()
        {
            foreach (var col in _columns)
            {
                if (col.CurrentWidth <= 0f)
                {
                    col.CurrentWidth = col.DefaultWidth;
                }
            }
        }

        private void BuildLayout()
        {
            _headerRow = new VisualElement();
            _headerRow.AddToClassList("loctable__header");

            _frozenHeaderContainer = new VisualElement();
            _frozenHeaderContainer.AddToClassList("loctable__frozen-header");
            _headerRow.Add(_frozenHeaderContainer);

            _bodyHeaderViewport = new VisualElement();
            _bodyHeaderViewport.AddToClassList("loctable__body-header-viewport");
            _headerRow.Add(_bodyHeaderViewport);

            _bodyHeaderContent = new VisualElement();
            _bodyHeaderContent.AddToClassList("loctable__body-header-content");
            _bodyHeaderViewport.Add(_bodyHeaderContent);

            _bodyHeaderViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateScrollRange());

            Add(_headerRow);

            _bodyListView = new ListView
            {
                fixedItemHeight = 22f,
                itemsSource = _rows,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.None,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
            };
            _bodyListView.AddToClassList("loctable__body");
            _bodyListView.style.flexGrow = 1f;
            Add(_bodyListView);

            _horizontalScroller = new Scroller(0f, 0f, OnHorizontalScroll, SliderDirection.Horizontal)
            {
                value = 0f,
            };
            _horizontalScroller.AddToClassList("loctable__horizontal-scroller");
            Add(_horizontalScroller);

            RebuildHeader();
        }

        private void RebuildHeader()
        {
            _frozenHeaderContainer.Clear();
            _bodyHeaderContent.Clear();
            for (int i = 0; i < _columns.Count; i++)
            {
                var col = _columns[i];
                var cell = BuildHeaderCell(col, i);
                if (col.Frozen)
                {
                    _frozenHeaderContainer.Add(cell);
                }
                else
                {
                    _bodyHeaderContent.Add(cell);
                }
            }
        }

        private VisualElement BuildHeaderCell(LocColumnSpec col, int columnIndex)
        {
            var cell = new VisualElement();
            cell.AddToClassList("loctable__header-cell");
            cell.userData = col;
            cell.AddManipulator(new ContextualMenuManipulator(PopulateColumnContextMenu));

            if (columnIndex >= ProtectedColumnCount)
            {
                var field = new TextField { value = col.HeaderLabel ?? col.Id, isDelayed = true };
                field.AddToClassList("loctable__header-cell-label");
                field.AddToClassList("loctable__header-cell-label--editable");
                field.userData = col;
                field.RegisterValueChangedCallback(OnHeaderRenamed);
                cell.Add(field);
            }
            else
            {
                var label = new Label(col.HeaderLabel ?? col.Id);
                label.AddToClassList("loctable__header-cell-label");
                cell.Add(label);
            }

            var handle = new VisualElement();
            handle.AddToClassList("loctable__resize-handle");
            handle.style.width = ResizeHandleWidth;
            handle.userData = col;
            handle.RegisterCallback<PointerDownEvent>(OnResizeHandlePointerDown);
            handle.RegisterCallback<PointerMoveEvent>(OnResizeHandlePointerMove);
            handle.RegisterCallback<PointerUpEvent>(OnResizeHandlePointerUp);
            handle.RegisterCallback<PointerCaptureOutEvent>(OnResizeHandleCaptureLost);
            cell.Add(handle);

            return cell;
        }

        private void OnHeaderRenamed(ChangeEvent<string> evt)
        {
            if (evt.target is not TextField tf || tf.userData is not LocColumnSpec col) return;
            var newName = evt.newValue?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(newName) || col.Id == newName)
            {
                tf.SetValueWithoutNotify(col.HeaderLabel ?? col.Id);
                return;
            }
            foreach (var other in _columns)
            {
                if (other != col && other.Id == newName)
                {
                    tf.SetValueWithoutNotify(col.HeaderLabel ?? col.Id);
                    return;
                }
            }
            var oldId = col.Id;
            col.Id = newName;
            col.HeaderLabel = newName;
            foreach (var row in _rows)
            {
                if (row.Cells.TryGetValue(oldId, out var v))
                {
                    row.Cells.Remove(oldId);
                    row.Cells[newName] = v;
                }
            }
            _bodyListView?.Rebuild();
            ApplyAllColumnVisibility();
            ApplyAllColumnWidths();
            SchedulePersist();
        }

        private void PopulateColumnContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.currentTarget is not VisualElement cellElement) return;
            if (cellElement.userData is not LocColumnSpec col) return;
            int columnIndex = _columns.IndexOf(col);
            if (columnIndex < 0) return;

            var beforeStatus = columnIndex < ProtectedColumnCount
                ? DropdownMenuAction.Status.Disabled
                : DropdownMenuAction.Status.Normal;
            var afterStatus = columnIndex < ProtectedColumnCount - 1
                ? DropdownMenuAction.Status.Disabled
                : DropdownMenuAction.Status.Normal;
            var deleteStatus = columnIndex < ProtectedColumnCount
                ? DropdownMenuAction.Status.Disabled
                : DropdownMenuAction.Status.Normal;

            evt.menu.AppendAction("Insert column before", _ => InsertColumnAt(columnIndex), beforeStatus);
            evt.menu.AppendAction("Insert column after", _ => InsertColumnAt(columnIndex + 1), afterStatus);
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete column", _ => DeleteColumn(col), deleteStatus);
        }

        private void InsertColumnAt(int index)
        {
            var name = GenerateUniqueColumnName();
            var col = new LocColumnSpec
            {
                Id = name,
                HeaderLabel = name,
                DefaultWidth = LocColumnSpecDefaults.WidthFor(name),
                MinWidth = LocColumnSpecDefaults.MinWidthFor(name),
                CurrentWidth = LocColumnSpecDefaults.WidthFor(name),
            };
            _columns.Insert(index, col);
            RebuildHeader();
            _bodyListView?.Rebuild();
            ApplyAllColumnVisibility();
            ApplyAllColumnWidths();
            SchedulePersist();

            schedule.Execute(() =>
            {
                var cell = _bodyHeaderContent.Children().FirstOrDefault(c => c.userData is LocColumnSpec s && s == col);
                cell?.Q<TextField>(className: "loctable__header-cell-label")?.Focus();
            }).StartingIn(0);
        }

        private void DeleteColumn(LocColumnSpec col)
        {
            var columnIndex = _columns.IndexOf(col);
            if (columnIndex < ProtectedColumnCount) return;
            _columns.Remove(col);
            foreach (var row in _rows)
            {
                row.Cells.Remove(col.Id);
            }
            RebuildHeader();
            _bodyListView?.Rebuild();
            ApplyAllColumnVisibility();
            ApplyAllColumnWidths();
            UpdateScrollRange();
            SchedulePersist();
        }

        private string GenerateUniqueColumnName()
        {
            var existingIds = new HashSet<string>(_columns.Count);
            foreach (var c in _columns) existingIds.Add(c.Id);
            var n = 1;
            while (existingIds.Contains("Column " + n)) n++;
            return "Column " + n;
        }

        private void InsertRowAt(int index)
        {
            _rows.Insert(index, new LocRow());
            _bodyListView?.Rebuild();
            ApplyAllColumnWidthsToBody();
            ApplyAllColumnVisibilityToBody();
            SchedulePersist();
        }

        #endregion

        #region Row build and bind

        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("loctable__row");
            row.AddManipulator(new ContextualMenuManipulator(PopulateRowContextMenu));

            var frozenContainer = new VisualElement();
            frozenContainer.AddToClassList("loctable__frozen-body");
            row.Add(frozenContainer);

            var bodyViewport = new VisualElement();
            bodyViewport.AddToClassList("loctable__body-viewport");
            row.Add(bodyViewport);

            var bodyContent = new VisualElement();
            bodyContent.AddToClassList("loctable__body-content");
            bodyViewport.Add(bodyContent);

            foreach (var col in _columns)
            {
                var cell = BuildBodyCell(col);
                if (col.Frozen)
                {
                    frozenContainer.Add(cell);
                }
                else
                {
                    bodyContent.Add(cell);
                }
            }

            // Re-apply current widths and hidden state on construction so recycled rows match.
            ApplyAllColumnWidthsToRow(row);
            ApplyAllColumnVisibilityToRow(row);
            ApplyScrollToRow(row);
            return row;
        }

        private VisualElement BuildBodyCell(LocColumnSpec col)
        {
            var field = new TextField
            {
                isDelayed = true,
            };
            field.AddToClassList("loctable__cell");
            field.userData = col;
            return field;
        }

        private void BindRow(VisualElement rowElement, int index)
        {
            if (index < 0 || index >= _rows.Count) return;
            var row = _rows[index];
            rowElement.userData = row;

            if ((index & 1) == 1)
            {
                rowElement.AddToClassList("loctable__row--alt");
            }
            else
            {
                rowElement.RemoveFromClassList("loctable__row--alt");
            }

            BindCellsInContainer(rowElement.Q<VisualElement>(className: "loctable__frozen-body"), row);
            BindCellsInContainer(rowElement.Q<VisualElement>(className: "loctable__body-content"), row);

            ApplyAllColumnWidthsToRow(rowElement);
            ApplyAllColumnVisibilityToRow(rowElement);
            ApplyScrollToRow(rowElement);
        }

        private void PopulateRowContextMenu(ContextualMenuPopulateEvent evt)
        {
            if (evt.currentTarget is not VisualElement rowElement) return;
            if (rowElement.userData is not LocRow row) return;
            int rowIndex = _rows.IndexOf(row);
            if (rowIndex < 0) return;
            evt.menu.AppendAction("Insert row before", _ => InsertRowAt(rowIndex));
            evt.menu.AppendAction("Insert row after", _ => InsertRowAt(rowIndex + 1));
            evt.menu.AppendSeparator();
            evt.menu.AppendAction("Delete row", _ => DeleteRow(row));
        }

        private void DeleteRow(LocRow row)
        {
            _rows.Remove(row);
            _bodyListView?.Rebuild();
            ApplyAllColumnWidthsToBody();
            ApplyAllColumnVisibilityToBody();
            SchedulePersist();
        }

        private void BindCellsInContainer(VisualElement container, LocRow row)
        {
            if (container == null) return;
            foreach (var child in container.Children())
            {
                LocColumnSpec col = child.userData switch
                {
                    LocColumnSpec direct => direct,
                    CellBinding existing => existing.Column,
                    _ => null,
                };
                if (col == null) continue;
                if (child is TextField tf)
                {
                    tf.SetValueWithoutNotify(row.Get(col.Id));
                    tf.UnregisterValueChangedCallback(OnCellCommitted);
                    tf.RegisterValueChangedCallback(OnCellCommitted);
                    tf.userData = new CellBinding { Column = col, Row = row, Owner = this };
                }
            }
        }

        private sealed class CellBinding
        {
            public LocColumnSpec Column;
            public LocRow Row;
            public LocTableView Owner;
        }

        private static void OnCellCommitted(ChangeEvent<string> evt)
        {
            if (evt.target is TextField tf && tf.userData is CellBinding binding)
            {
                binding.Row.Set(binding.Column.Id, evt.newValue ?? string.Empty);
                binding.Owner?.RowEdited?.Invoke(binding.Row);
            }
        }

        #endregion

        #region Width application

        private void ApplyAllColumnWidths()
        {
            foreach (var col in _columns)
            {
                ApplyColumnWidthEverywhere(col);
            }
            UpdateScrollRange();
        }

        private void ApplyColumnWidthEverywhere(LocColumnSpec col)
        {
            ApplyColumnWidthToHeader(col);
            if (_bodyListView == null) return;
            var rows = _bodyListView.Query<VisualElement>(className: "loctable__row").ToList();
            foreach (var row in rows)
            {
                ApplyColumnWidthToRow(row, col);
            }
        }

        private void ApplyAllColumnWidthsToBody()
        {
            if (_bodyListView == null) return;
            var rows = _bodyListView.Query<VisualElement>(className: "loctable__row").ToList();
            foreach (var row in rows)
            {
                foreach (var col in _columns)
                {
                    ApplyColumnWidthToRow(row, col);
                }
            }
            UpdateScrollRange();
        }

        private void ApplyColumnWidthToHeader(LocColumnSpec col)
        {
            var cells = (col.Frozen ? _frozenHeaderContainer : _bodyHeaderContent)?.Children();
            if (cells == null) return;
            foreach (var cell in cells)
            {
                if (cell.userData is LocColumnSpec c && c == col)
                {
                    cell.style.width = col.CurrentWidth;
                }
            }
        }

        private void ApplyAllColumnWidthsToRow(VisualElement rowElement)
        {
            foreach (var col in _columns)
            {
                ApplyColumnWidthToRow(rowElement, col);
            }
        }

        private static void ApplyColumnWidthToRow(VisualElement rowElement, LocColumnSpec col)
        {
            var container = rowElement.Q<VisualElement>(className: col.Frozen ? "loctable__frozen-body" : "loctable__body-content");
            if (container == null) return;
            foreach (var child in container.Children())
            {
                if (child.userData is LocColumnSpec c && c == col)
                {
                    child.style.width = col.CurrentWidth;
                }
                else if (child.userData is CellBinding b && b.Column == col)
                {
                    child.style.width = col.CurrentWidth;
                }
            }
        }

        #endregion

        #region Visibility application

        private void ApplyAllColumnVisibility()
        {
            foreach (var col in _columns)
            {
                ApplyColumnVisibilityEverywhere(col);
            }
            UpdateScrollRange();
        }

        private void ApplyColumnVisibilityEverywhere(LocColumnSpec col)
        {
            ApplyColumnVisibilityToHeader(col);
            if (_bodyListView == null) return;
            var rows = _bodyListView.Query<VisualElement>(className: "loctable__row").ToList();
            foreach (var row in rows)
            {
                ApplyColumnVisibilityToRow(row, col);
            }
        }

        private void ApplyAllColumnVisibilityToBody()
        {
            if (_bodyListView == null) return;
            var rows = _bodyListView.Query<VisualElement>(className: "loctable__row").ToList();
            foreach (var row in rows)
            {
                foreach (var col in _columns)
                {
                    ApplyColumnVisibilityToRow(row, col);
                }
            }
            UpdateScrollRange();
        }

        private void ApplyColumnVisibilityToHeader(LocColumnSpec col)
        {
            var cells = (col.Frozen ? _frozenHeaderContainer : _bodyHeaderContent)?.Children();
            if (cells == null) return;
            foreach (var cell in cells)
            {
                if (cell.userData is LocColumnSpec c && c == col)
                {
                    cell.style.display = col.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        private void ApplyAllColumnVisibilityToRow(VisualElement rowElement)
        {
            foreach (var col in _columns)
            {
                ApplyColumnVisibilityToRow(rowElement, col);
            }
        }

        private static void ApplyColumnVisibilityToRow(VisualElement rowElement, LocColumnSpec col)
        {
            var container = rowElement.Q<VisualElement>(className: col.Frozen ? "loctable__frozen-body" : "loctable__body-content");
            if (container == null) return;
            foreach (var child in container.Children())
            {
                bool match = (child.userData is LocColumnSpec c && c == col)
                    || (child.userData is CellBinding b && b.Column == col);
                if (match)
                {
                    child.style.display = col.Hidden ? DisplayStyle.None : DisplayStyle.Flex;
                }
            }
        }

        #endregion

        #region Resize drag

        private void OnResizeHandlePointerDown(PointerDownEvent evt)
        {
            if (evt.target is not VisualElement handle) return;
            if (handle.userData is not LocColumnSpec col) return;
            _resizingColumn = col;
            _resizeStartPointerX = evt.position.x;
            _resizeStartWidth = col.CurrentWidth;
            _resizeHandleCapture = handle;
            handle.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnResizeHandlePointerMove(PointerMoveEvent evt)
        {
            if (_resizingColumn == null) return;
            if (evt.target is not VisualElement handle || !handle.HasPointerCapture(evt.pointerId)) return;
            var delta = evt.position.x - _resizeStartPointerX;
            var proposed = Mathf.Max(_resizingColumn.MinWidth, _resizeStartWidth + delta);
            if (_resizingColumn.Frozen)
            {
                proposed = ClampFrozenWidth(_resizingColumn, proposed);
            }
            _resizingColumn.CurrentWidth = proposed;
            ApplyColumnWidthEverywhere(_resizingColumn);
            UpdateScrollRange();
            evt.StopPropagation();
        }

        private void OnResizeHandlePointerUp(PointerUpEvent evt)
        {
            EndResize(evt.pointerId);
        }

        private void OnResizeHandleCaptureLost(PointerCaptureOutEvent evt)
        {
            EndResize(evt.pointerId);
        }

        private void EndResize(int pointerId)
        {
            if (_resizingColumn == null) return;
            _resizingColumn = null;
            if (_resizeHandleCapture != null && _resizeHandleCapture.HasPointerCapture(pointerId))
            {
                _resizeHandleCapture.ReleasePointer(pointerId);
            }
            _resizeHandleCapture = null;
            SchedulePersist();
        }

        private float ClampFrozenWidth(LocColumnSpec col, float proposed)
        {
            float otherFrozen = 0f;
            foreach (var c in _columns)
            {
                if (c == col) continue;
                if (c.Frozen && !c.Hidden) otherFrozen += c.CurrentWidth;
            }
            var maxFrozenTotal = Mathf.Max(0f, resolvedStyle.width - FrozenWidthSafetyMargin);
            var maxForThisColumn = Mathf.Max(col.MinWidth, maxFrozenTotal - otherFrozen);
            return Mathf.Min(proposed, maxForThisColumn);
        }

        #endregion

        #region Columns dropdown

        /// <summary>
        /// Opens the Columns dropdown beneath the given anchor, listing non-frozen columns as toggleable visibility entries.
        /// </summary>
        /// <param name="anchor">The element below which the dropdown anchors.</param>
        public void OpenColumnsMenu(VisualElement anchor)
        {
            if (anchor == null) return;
            var menu = new GenericDropdownMenu();
            foreach (var col in _columns)
            {
                if (col.Frozen) continue;
                var captured = col;
                menu.AddItem(col.HeaderLabel ?? col.Id, !col.Hidden, () =>
                {
                    captured.Hidden = !captured.Hidden;
                    ApplyColumnVisibilityEverywhere(captured);
                    UpdateScrollRange();
                    SchedulePersist();
                });
            }
            menu.DropDown(anchor.worldBound, anchor, DropdownMenuSizeMode.Auto);
        }

        #endregion

        #region Scroll sync

        private void OnHorizontalScroll(float value)
        {
            _scrollX = value;
            ApplyScrollToHeader();
            ApplyScrollToAllRows();
        }

        private void ApplyScrollToHeader()
        {
            if (_bodyHeaderContent == null) return;
            _bodyHeaderContent.style.translate = new Translate(-_scrollX, 0f);
        }

        private void ApplyScrollToAllRows()
        {
            if (_bodyListView == null) return;
            var rows = _bodyListView.Query<VisualElement>(className: "loctable__row").ToList();
            foreach (var row in rows)
            {
                ApplyScrollToRow(row);
            }
        }

        private void ApplyScrollToRow(VisualElement rowElement)
        {
            var content = rowElement.Q<VisualElement>(className: "loctable__body-content");
            if (content == null) return;
            content.style.translate = new Translate(-_scrollX, 0f);
        }

        private void UpdateScrollRange()
        {
            float total = 0f;
            foreach (var col in _columns)
            {
                if (col.Frozen || col.Hidden) continue;
                total += col.CurrentWidth;
            }
            _bodyContentWidth = total;
            _bodyViewportWidth = _bodyHeaderViewport?.layout.width ?? 0f;
            var range = Mathf.Max(0f, _bodyContentWidth - _bodyViewportWidth);
            if (_horizontalScroller != null)
            {
                _horizontalScroller.highValue = range;
                var ratio = _bodyContentWidth > 0f ? Mathf.Clamp01(_bodyViewportWidth / _bodyContentWidth) : 1f;
                _horizontalScroller.Adjust(ratio);
                if (_scrollX > range)
                {
                    _scrollX = range;
                    _horizontalScroller.value = range;
                    ApplyScrollToHeader();
                    ApplyScrollToAllRows();
                }
            }
        }

        private void OnGeometryChanged()
        {
            UpdateScrollRange();
        }

        #endregion

        #region Persistence

        [Serializable]
        private sealed class LayoutPrefs
        {
            public List<string> ColumnIds = new();
            public List<float> ColumnWidths = new();
            public List<string> HiddenColumns = new();
        }

        private void LoadPrefs()
        {
            var json = EditorPrefs.GetString(_prefsKey, null);
            if (string.IsNullOrEmpty(json)) return;
            LayoutPrefs prefs;
            try
            {
                prefs = JsonUtility.FromJson<LayoutPrefs>(json);
            }
            catch
            {
                return;
            }
            if (prefs == null) return;

            for (int i = 0; i < prefs.ColumnIds.Count && i < prefs.ColumnWidths.Count; i++)
            {
                var col = FindColumn(prefs.ColumnIds[i]);
                if (col != null && prefs.ColumnWidths[i] >= col.MinWidth)
                {
                    col.CurrentWidth = prefs.ColumnWidths[i];
                }
            }
            var hidden = new HashSet<string>(prefs.HiddenColumns ?? new List<string>());
            foreach (var col in _columns)
            {
                col.Hidden = !col.Frozen && hidden.Contains(col.Id);
            }
        }

        private void SavePrefs()
        {
            var prefs = new LayoutPrefs();
            foreach (var col in _columns)
            {
                prefs.ColumnIds.Add(col.Id);
                prefs.ColumnWidths.Add(col.CurrentWidth);
                if (col.Hidden) prefs.HiddenColumns.Add(col.Id);
            }
            EditorPrefs.SetString(_prefsKey, JsonUtility.ToJson(prefs));
        }

        private void SchedulePersist()
        {
            if (_persistScheduled) return;
            _persistScheduled = true;
            schedule.Execute(() =>
            {
                _persistScheduled = false;
                SavePrefs();
            }).StartingIn(PersistDebounceMs);
        }

        private LocColumnSpec FindColumn(string id)
        {
            foreach (var col in _columns)
            {
                if (col.Id == id) return col;
            }
            return null;
        }

        private static string ComposePrefsKey(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return PrefsKeyPrefix + "__unnamed__";
            }
            var sanitized = filePath
                .Replace('/', '_')
                .Replace('\\', '_')
                .Replace(':', '_')
                .Replace(' ', '_');
            return PrefsKeyPrefix + sanitized;
        }

        #endregion
    }
}
