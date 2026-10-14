using System;
using System.Collections.Generic;
using System.Linq;
using Redux.TechTree;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The tech tree editor's canvas: a tree drawn where and how the R&amp;D Center draws it, which pans and zooms, and
    /// where nodes are added, moved, linked, duplicated and deleted.
    /// </summary>
    /// <remarks>
    /// Everything is laid out in content space, one unit to one unit of the game's tree, and the content element is
    /// then moved and scaled as a whole. Its local origin is the top-left of the tree's visible band, with y running
    /// down, so a node's tech tree y is flipped against <see cref="TechTreeCanvasGeometry.BAND_HEIGHT" />.
    /// </remarks>
    public class TechTreeCanvas : VisualElement
    {
        private const float MIN_ZOOM = 0.15f;

        private const float MAX_ZOOM = 2f;

        private const float ZOOM_STEP = 1.12f;

        private const float FRAME_MARGIN = 40f;

        // A press that moves less than this before release is a click, not a pan
        private const float CLICK_SLOP = 4f;

        private const float GRID_DOT_SIZE = 3f;

        // Colours read from the R&D Center's prefab and tier art
        private static readonly Color BandColor = new(0.059f, 0.063f, 0.114f);

        private static readonly Color OverflowColor = new(0.32f, 0.35f, 0.39f, 0.08f);

        private static readonly Color RuleColor = new(0.322f, 0.349f, 0.388f);

        private static readonly Color GridColor = new(0.565f, 0.592f, 0.686f, 0.22f);

        private static readonly Color LockedLineColor = new(0.329f, 0.353f, 0.4f, 0.902f);

        private static readonly Color PrerequisiteLineColor = new(0.71f, 0.745f, 0.839f);

        private static readonly Color DependentLineColor = new(0.357f, 0.373f, 0.859f);

        private readonly VisualElement _content;

        private readonly VisualElement _backdrop;

        private readonly VisualElement _lines;

        private readonly VisualElement _pageLabels;

        private readonly VisualElement _nodeLayer;

        private readonly Dictionary<string, TechTreeNodeElement> _nodeElements = new();

        private readonly Dictionary<string, Vector2> _positions = new();

        private TechTreeAsset _tree;

        private string _selectedId;

        private int _pageCount = 1;

        private float _overflow;

        private float _zoom = 0.7f;

        private Vector2 _pan;

        private bool _isPanning;

        private bool _hasPanMoved;

        private Vector2 _panStart;

        private Vector2 _panOrigin;

        private bool _hasFramed;

        private bool _showsGrid = true;

        private bool _showsPages = true;

        private bool _showsBand = true;

        // The node a left press started on, and whether that press moves it or links it to another
        private string _pressedNodeId;

        private Vector2 _pressStart;

        private bool _isLinking;

        private bool _isDragging;

        private Vector2 _dragStartPosition;

        private int _dragUndoGroup;

        // Where a link being dragged ends, in content space
        private Vector2 _linkEnd;

        /// <summary>
        /// Raised with the selected node's ID when the selection changes, or null when nothing is selected.
        /// </summary>
        public event Action<string> OnSelectionChanged;

        /// <summary>
        /// Raised with the zoom whenever it changes.
        /// </summary>
        public event Action<float> OnZoomChanged;

        /// <summary>
        /// Raised after the canvas changes the tree.
        /// </summary>
        public event Action OnTreeEdited;

        /// <summary>
        /// Raised with the reason when the canvas refuses an edit.
        /// </summary>
        public event Action<string> OnRefused;

        /// <summary>
        /// Gets or sets whether moved and added nodes snap to the stock grid.
        /// </summary>
        public bool SnapsToGrid { get; set; } = true;

        /// <summary>
        /// Gets the selected node's ID, or null when nothing is selected.
        /// </summary>
        public string SelectedId => _selectedId;

        /// <summary>
        /// Gets or sets whether the stock grid's points are drawn.
        /// </summary>
        public bool ShowsGrid
        {
            get => _showsGrid;
            set
            {
                _showsGrid = value;
                _backdrop.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Gets or sets whether the edges and names of the tier pages are drawn.
        /// </summary>
        public bool ShowsPages
        {
            get => _showsPages;
            set
            {
                _showsPages = value;
                _pageLabels.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
                _backdrop.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Gets or sets whether the outline of the band the game shows without scrolling is drawn.
        /// </summary>
        public bool ShowsBand
        {
            get => _showsBand;
            set
            {
                _showsBand = value;
                _backdrop.MarkDirtyRepaint();
            }
        }

        /// <summary>
        /// Creates an empty canvas.
        /// </summary>
        public TechTreeCanvas()
        {
            AddToClassList("tech-tree-canvas");
            focusable = true;

            _content = new VisualElement { name = "tech-tree-content" };
            _content.AddToClassList("tech-tree-canvas__content");
            _content.style.transformOrigin = new TransformOrigin(0f, 0f);
            Add(_content);

            _backdrop = MakeLayer("tech-tree-backdrop");
            _backdrop.generateVisualContent += DrawBackdrop;
            _lines = MakeLayer("tech-tree-lines");
            _lines.generateVisualContent += DrawLines;
            _pageLabels = MakeLayer("tech-tree-page-labels");
            _nodeLayer = MakeLayer("tech-tree-nodes");

            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<GeometryChangedEvent>(_ => FrameOnce());
        }

        /// <summary>
        /// Shows a tree, or clears the canvas.
        /// </summary>
        /// <param name="tree">The tree to show, or null for none.</param>
        public void SetTree(TechTreeAsset tree)
        {
            _tree = tree;
            _hasFramed = false;
            Rebuild();
            FrameOnce();
        }

        /// <summary>
        /// Redraws the tree from its asset, keeping the view and the selection where the node still exists.
        /// </summary>
        public void Rebuild()
        {
            // A drag moves its node's element itself and rebuilds once it is dropped
            if (_isDragging)
                return;

            _nodeLayer.Clear();
            _pageLabels.Clear();
            _nodeElements.Clear();
            _positions.Clear();
            if (_tree == null)
            {
                _backdrop.MarkDirtyRepaint();
                _lines.MarkDirtyRepaint();
                return;
            }

            List<TechTreeNode> nodes = _tree.Nodes.Where(node => !string.IsNullOrEmpty(node.Data?.ID)).ToList();
            _pageCount = TechTreeLayout.GetTierCount(
                nodes.Select(node => node.Data.TechTreePosition.x),
                TechTreeCanvasGeometry.PLACEMENT_OFFSET,
                TechTreeCanvasGeometry.PAGE_WIDTH,
                Mathf.Max(1, _tree.Tiers.Count)
            );
            _overflow = TechTreeLayout.GetVerticalOverflow(
                nodes.Select(node => node.Data.TechTreePosition.y),
                TechTreeCanvasGeometry.PLACEMENT_OFFSET,
                TechTreeCanvasGeometry.NODE_HEIGHT / 2f
            );
            _content.style.width = _pageCount * TechTreeCanvasGeometry.PAGE_WIDTH;
            _content.style.height = TechTreeCanvasGeometry.BAND_HEIGHT + _overflow;

            foreach (TechTreeNode node in nodes)
            {
                Vector2 position = node.Data.TechTreePosition;

                // Of two nodes sharing an ID, lines and selection follow the first, as the game keeps the first
                if (!_positions.TryAdd(node.Data.ID, position))
                    continue;

                var element = new TechTreeNodeElement(node, ToLocal(position) - FrameHalfSize);
                _nodeLayer.Add(element);
                _nodeElements[node.Data.ID] = element;
            }

            AddPageLabels();
            if (_selectedId != null && !_nodeElements.ContainsKey(_selectedId))
            {
                Select(null);
            }

            RefreshSelection();
            _backdrop.MarkDirtyRepaint();
            _lines.MarkDirtyRepaint();
        }

        /// <summary>
        /// Selects a node, or clears the selection.
        /// </summary>
        /// <param name="nodeId">The node's ID, or null to select nothing.</param>
        public void Select(string nodeId)
        {
            if (nodeId == _selectedId)
                return;

            _selectedId = nodeId;
            RefreshSelection();
            _lines.MarkDirtyRepaint();
            OnSelectionChanged?.Invoke(nodeId);
        }

        /// <summary>
        /// Zooms in or out about the middle of the canvas.
        /// </summary>
        /// <param name="factor">How much to multiply the zoom by.</param>
        public void ZoomBy(float factor) => ZoomAt(layout.size / 2f, factor);

        /// <summary>
        /// Fits every node into view, or shows the start of the band when the tree is empty.
        /// </summary>
        public void FrameAll()
        {
            Vector2 viewSize = layout.size;
            if (float.IsNaN(viewSize.x) || viewSize.x <= 0f || viewSize.y <= 0f)
                return;

            if (_positions.Count == 0)
            {
                _zoom = 0.7f;
                _pan = new Vector2(16f, (viewSize.y - TechTreeCanvasGeometry.BAND_HEIGHT * _zoom) / 2f);
                ApplyTransform();
                return;
            }

            Rect bounds = GetNodeBounds();
            _zoom = Mathf.Clamp(
                Mathf.Min((viewSize.x - 2f * FRAME_MARGIN) / bounds.width, (viewSize.y - 2f * FRAME_MARGIN) / bounds.height),
                MIN_ZOOM,
                MAX_ZOOM
            );
            _pan = viewSize / 2f - bounds.center * _zoom;
            ApplyTransform();
        }

        /// <summary>
        /// Centres the view on a node.
        /// </summary>
        /// <param name="nodeId">The node's ID.</param>
        public void CentreOn(string nodeId)
        {
            if (!_positions.TryGetValue(nodeId, out Vector2 position))
                return;

            _pan = layout.size / 2f - ToLocal(position) * _zoom;
            ApplyTransform();
        }

        private static Vector2 FrameHalfSize =>
            new(TechTreeCanvasGeometry.FRAME_WIDTH / 2f, TechTreeCanvasGeometry.FRAME_HEIGHT / 2f);

        // Content space has y up from the band's bottom, the canvas has y down from the band's top
        private static Vector2 ToLocal(Vector2 techTreePosition)
        {
            Vector2 content = TechTreeCanvasGeometry.ToContent(techTreePosition);
            return new Vector2(content.x, TechTreeCanvasGeometry.BAND_HEIGHT - content.y);
        }

        private VisualElement MakeLayer(string layerName)
        {
            var layer = new VisualElement { name = layerName, pickingMode = PickingMode.Ignore };
            layer.AddToClassList("tech-tree-canvas__layer");
            _content.Add(layer);
            return layer;
        }

        private void AddPageLabels()
        {
            for (int page = 0; page < _pageCount; page++)
            {
                string tierName = page < _tree.Tiers.Count ? _tree.Tiers[page].EnglishName : null;
                var label = new Label(string.IsNullOrEmpty(tierName) ? $"TIER {page + 1}" : tierName.ToUpperInvariant())
                {
                    pickingMode = PickingMode.Ignore,
                };
                label.AddToClassList("tech-tree-page-label");
                label.style.left = page * TechTreeCanvasGeometry.PAGE_WIDTH + 8f;
                label.style.top = 6f;
                _pageLabels.Add(label);
            }
        }

        private void RefreshSelection()
        {
            foreach (KeyValuePair<string, TechTreeNodeElement> pair in _nodeElements)
            {
                pair.Value.SetSelected(pair.Key == _selectedId);
            }
        }

        private Rect GetNodeBounds()
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (Vector2 position in _positions.Values)
            {
                Vector2 local = ToLocal(position);
                min = Vector2.Min(min, local - FrameHalfSize);
                max = Vector2.Max(max, local + FrameHalfSize);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void FrameOnce()
        {
            if (_hasFramed || float.IsNaN(layout.width) || layout.width <= 0f)
                return;

            _hasFramed = true;
            FrameAll();
        }

        private void ApplyTransform()
        {
            _content.style.translate = new Translate(_pan.x, _pan.y);
            _content.style.scale = new Scale(new Vector2(_zoom, _zoom));
            OnZoomChanged?.Invoke(_zoom);
        }

        private void ZoomAt(Vector2 point, float factor)
        {
            float zoom = Mathf.Clamp(_zoom * factor, MIN_ZOOM, MAX_ZOOM);
            Vector2 contentPoint = (point - _pan) / _zoom;
            _zoom = zoom;
            _pan = point - contentPoint * zoom;
            ApplyTransform();
        }

        private void OnWheel(WheelEvent evt)
        {
            ZoomAt(evt.localMousePosition, evt.delta.y < 0f ? ZOOM_STEP : 1f / ZOOM_STEP);
            evt.StopPropagation();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            Focus();
            var node = (evt.target as VisualElement)?.GetFirstOfType<TechTreeNodeElement>();
            if (evt.button == 1)
            {
                ShowContextMenu(node?.NodeId, evt.position);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0 && evt.button != 2)
                return;

            if (_tree != null && evt.button == 0)
            {
                if (node != null)
                {
                    PressNode(node.NodeId, evt);
                    return;
                }

                if (evt.clickCount == 2)
                {
                    AddNodeAt(evt.position);
                    evt.StopPropagation();
                    return;
                }
            }

            _isPanning = true;
            _hasPanMoved = false;
            _panStart = evt.position;
            _panOrigin = _pan;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        // Shift starts a link from the node, anything else a move once the pointer leaves the click slop
        private void PressNode(string nodeId, PointerDownEvent evt)
        {
            Select(nodeId);
            _pressedNodeId = nodeId;
            _pressStart = evt.position;
            _isLinking = evt.shiftKey;
            _linkEnd = ToContentLocal(evt.position);
            _dragStartPosition = TechTreeEditing.Find(_tree, nodeId).Data.TechTreePosition;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!this.HasPointerCapture(evt.pointerId))
                return;

            Vector2 delta = (Vector2)evt.position - (_pressedNodeId != null ? _pressStart : _panStart);
            if (_pressedNodeId != null)
            {
                DragNode(evt.position, delta);
                return;
            }

            if (!_isPanning)
                return;

            _hasPanMoved |= delta.magnitude > CLICK_SLOP;
            _pan = _panOrigin + delta;
            ApplyTransform();
        }

        private void DragNode(Vector2 pointer, Vector2 delta)
        {
            if (_isLinking)
            {
                _linkEnd = ToContentLocal(pointer);
                _lines.MarkDirtyRepaint();
                return;
            }

            if (!_isDragging)
            {
                if (delta.magnitude <= CLICK_SLOP)
                    return;

                // The whole drag is one undo step, so one Ctrl+Z puts a node dragged by accident back
                Undo.IncrementCurrentGroup();
                _dragUndoGroup = Undo.GetCurrentGroup();
                Undo.RecordObject(_tree, "Move Tech Node");
                _isDragging = true;
            }

            // Content y runs down, tech tree y up
            Vector2 position = _dragStartPosition + new Vector2(delta.x, -delta.y) / _zoom;
            if (SnapsToGrid)
            {
                position = TechTreeCanvasGeometry.Snap(position);
            }

            TechTreeEditing.Find(_tree, _pressedNodeId).Data.TechTreePosition = position;
            _positions[_pressedNodeId] = position;
            if (_nodeElements.TryGetValue(_pressedNodeId, out TechTreeNodeElement element))
            {
                element.MoveTo(ToLocal(position) - FrameHalfSize);
            }

            _lines.MarkDirtyRepaint();
        }

        // A click on empty space, rather than a drag, clears the selection
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_pressedNodeId != null)
            {
                this.ReleasePointer(evt.pointerId);
                DropNode(evt.position);
                return;
            }

            if (!_isPanning)
                return;

            _isPanning = false;
            this.ReleasePointer(evt.pointerId);
            if (!_hasPanMoved && evt.button == 0)
            {
                Select(null);
            }
        }

        private void DropNode(Vector2 pointer)
        {
            string nodeId = _pressedNodeId;
            _pressedNodeId = null;
            if (_isLinking)
            {
                _isLinking = false;
                _lines.MarkDirtyRepaint();
                string targetId = PickNode(pointer)?.NodeId;
                if (targetId != null && targetId != nodeId)
                {
                    Link(targetId, nodeId);
                }

                return;
            }

            if (!_isDragging)
                return;

            _isDragging = false;
            Undo.CollapseUndoOperations(_dragUndoGroup);
            EditorUtility.SetDirty(_tree);
            Rebuild();
            OnTreeEdited?.Invoke();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_tree == null || _selectedId == null)
                return;

            if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace)
                return;

            DeleteNode(_selectedId);
            evt.StopPropagation();
        }

        private void ShowContextMenu(string nodeId, Vector2 pointer)
        {
            if (_tree == null)
                return;

            var menu = new GenericDropdownMenu();
            if (nodeId == null)
            {
                menu.AddItem("Add Node Here", false, () => AddNodeAt(pointer));
            }
            else
            {
                if (_selectedId != null && _selectedId != nodeId && _positions.ContainsKey(_selectedId))
                {
                    AddLinkItems(menu, _selectedId, nodeId);
                    menu.AddSeparator(string.Empty);
                }

                menu.AddItem("Duplicate", false, () => DuplicateNode(nodeId));
                menu.AddItem("Delete", false, () => DeleteNode(nodeId));
            }

            menu.DropDown(new Rect(pointer, Vector2.zero), this, DropdownMenuSizeMode.Auto);
        }

        // Links the selected node and the clicked one either way, or removes a link between them
        private void AddLinkItems(GenericDropdownMenu menu, string selectedId, string clickedId)
        {
            AddLinkItem(menu, clickedId, selectedId);
            AddLinkItem(menu, selectedId, clickedId);
            if (IsLinked(selectedId, clickedId) || IsLinked(clickedId, selectedId))
            {
                menu.AddItem("Remove Link", false, () => Edit("Unlink Tech Nodes", () =>
                    TechTreeEditing.RemovePrerequisite(_tree, selectedId, clickedId)
                    | TechTreeEditing.RemovePrerequisite(_tree, clickedId, selectedId)));
            }
        }

        private void AddLinkItem(GenericDropdownMenu menu, string nodeId, string prerequisiteId)
        {
            string label = $"{DisplayName(nodeId)} requires {DisplayName(prerequisiteId)}";
            if (IsLinked(nodeId, prerequisiteId))
            {
                menu.AddDisabledItem(label, true);
            }
            else if (TechTreeEditing.WouldCreateLoop(_tree, nodeId, prerequisiteId))
            {
                menu.AddDisabledItem(label, false);
            }
            else
            {
                menu.AddItem(label, false, () => Link(nodeId, prerequisiteId));
            }
        }

        private void Link(string nodeId, string prerequisiteId)
        {
            if (TechTreeEditing.WouldCreateLoop(_tree, nodeId, prerequisiteId))
            {
                OnRefused?.Invoke($"{DisplayName(prerequisiteId)} already requires {DisplayName(nodeId)}, so the link would make a loop.");
                return;
            }

            Edit("Link Tech Nodes", () => TechTreeEditing.TryAddPrerequisite(_tree, nodeId, prerequisiteId));
        }

        private void AddNodeAt(Vector2 pointer)
        {
            Vector2 position = ToTechTree(ToContentLocal(pointer));
            if (SnapsToGrid)
            {
                position = TechTreeCanvasGeometry.Snap(position);
            }

            string addedId = null;
            Edit("Add Tech Node", () =>
            {
                addedId = TechTreeEditing.AddNode(_tree, position).Data.ID;
                return true;
            });
            Select(addedId);
        }

        // The copy goes one stock row below the original
        private void DuplicateNode(string nodeId)
        {
            string copyId = null;
            Edit("Duplicate Tech Node", () =>
            {
                copyId = TechTreeEditing.DuplicateNode(_tree, nodeId, new Vector2(0f, -TechTreeCanvasGeometry.GRID_Y))?.Data.ID;
                return copyId != null;
            });
            if (copyId != null)
            {
                Select(copyId);
            }
        }

        private void DeleteNode(string nodeId)
        {
            if (nodeId == _selectedId)
            {
                Select(null);
            }

            Edit("Delete Tech Node", () => TechTreeEditing.DeleteNode(_tree, nodeId));
        }

        private void Edit(string undoName, Func<bool> change)
        {
            Undo.RecordObject(_tree, undoName);
            if (!change())
                return;

            EditorUtility.SetDirty(_tree);
            Rebuild();
            OnTreeEdited?.Invoke();
        }

        private bool IsLinked(string nodeId, string prerequisiteId) =>
            TechTreeEditing.Find(_tree, nodeId)?.Data.RequiredTechNodeIDs?.Contains(prerequisiteId) == true;

        private string DisplayName(string nodeId)
        {
            TechTreeNode node = TechTreeEditing.Find(_tree, nodeId);
            return node == null || string.IsNullOrEmpty(node.EnglishName) ? nodeId : node.EnglishName;
        }

        private TechTreeNodeElement PickNode(Vector2 pointer) =>
            (panel?.Pick(pointer))?.GetFirstOfType<TechTreeNodeElement>();

        private Vector2 ToContentLocal(Vector2 pointer) => (this.WorldToLocal(pointer) - _pan) / _zoom;

        // The inverse of ToLocal
        private static Vector2 ToTechTree(Vector2 contentLocal) =>
            new(
                contentLocal.x - TechTreeCanvasGeometry.PLACEMENT_OFFSET,
                TechTreeCanvasGeometry.BAND_HEIGHT - contentLocal.y - TechTreeCanvasGeometry.PLACEMENT_OFFSET
            );

        private void DrawBackdrop(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            float width = _pageCount * TechTreeCanvasGeometry.PAGE_WIDTH;
            float height = TechTreeCanvasGeometry.BAND_HEIGHT + _overflow;

            FillRect(painter, new Rect(0f, 0f, width, TechTreeCanvasGeometry.BAND_HEIGHT), BandColor);
            if (_overflow > 0f)
            {
                FillRect(painter, new Rect(0f, TechTreeCanvasGeometry.BAND_HEIGHT, width, _overflow), OverflowColor);
            }

            if (_showsGrid)
            {
                DrawGrid(painter, width, height);
            }

            painter.lineWidth = 1f;
            painter.strokeColor = RuleColor;
            if (_showsPages)
            {
                for (int page = 1; page < _pageCount; page++)
                {
                    float x = page * TechTreeCanvasGeometry.PAGE_WIDTH;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(x, 0f));
                    painter.LineTo(new Vector2(x, height));
                    painter.Stroke();
                }
            }

            if (_showsBand)
            {
                painter.BeginPath();
                painter.MoveTo(Vector2.zero);
                painter.LineTo(new Vector2(width, 0f));
                painter.LineTo(new Vector2(width, TechTreeCanvasGeometry.BAND_HEIGHT));
                painter.LineTo(new Vector2(0f, TechTreeCanvasGeometry.BAND_HEIGHT));
                painter.ClosePath();
                painter.Stroke();
            }
        }

        // One dot per stock grid point inside the content, as a single fill
        private void DrawGrid(Painter2D painter, float width, float height)
        {
            float lowestY = TechTreeCanvasGeometry.BAND_HEIGHT - height - TechTreeCanvasGeometry.PLACEMENT_OFFSET;
            float highestY = TechTreeCanvasGeometry.BAND_HEIGHT - TechTreeCanvasGeometry.PLACEMENT_OFFSET;
            int firstRow = Mathf.CeilToInt((lowestY - TechTreeCanvasGeometry.GRID_Y_ORIGIN) / TechTreeCanvasGeometry.GRID_Y);
            int lastRow = Mathf.FloorToInt((highestY - TechTreeCanvasGeometry.GRID_Y_ORIGIN) / TechTreeCanvasGeometry.GRID_Y);
            float half = GRID_DOT_SIZE / 2f;

            painter.fillColor = GridColor;
            painter.BeginPath();
            for (float x = 0f; x + TechTreeCanvasGeometry.PLACEMENT_OFFSET <= width; x += TechTreeCanvasGeometry.GRID_X)
            {
                for (int row = firstRow; row <= lastRow; row++)
                {
                    Vector2 dot = ToLocal(new Vector2(x, TechTreeCanvasGeometry.GRID_Y_ORIGIN + row * TechTreeCanvasGeometry.GRID_Y));
                    painter.MoveTo(dot + new Vector2(-half, -half));
                    painter.LineTo(dot + new Vector2(half, -half));
                    painter.LineTo(dot + new Vector2(half, half));
                    painter.LineTo(dot + new Vector2(-half, half));
                    painter.ClosePath();
                }
            }

            painter.Fill();
        }

        // Every connector in the locked grey, then the selection's own on top, prerequisites and dependents apart
        private void DrawLines(MeshGenerationContext context)
        {
            if (_tree == null)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = TechTreeCanvasGeometry.CONNECTOR_WIDTH;
            painter.lineJoin = LineJoin.Miter;
            painter.lineCap = LineCap.Butt;

            var highlighted = new List<(Vector2 from, Vector2 to, Color color)>();
            foreach (TechTreeNode node in _tree.Nodes)
            {
                string id = node.Data?.ID;
                if (string.IsNullOrEmpty(id) || !_positions.TryGetValue(id, out Vector2 position))
                    continue;

                foreach (string prerequisiteId in node.Data.RequiredTechNodeIDs ?? Array.Empty<string>())
                {
                    if (!_positions.TryGetValue(prerequisiteId, out Vector2 prerequisite))
                        continue;

                    StrokeConnector(painter, prerequisite, position, LockedLineColor);
                    if (id == _selectedId)
                    {
                        highlighted.Add((prerequisite, position, PrerequisiteLineColor));
                    }
                    else if (prerequisiteId == _selectedId)
                    {
                        highlighted.Add((prerequisite, position, DependentLineColor));
                    }
                }
            }

            foreach ((Vector2 from, Vector2 to, Color color) in highlighted)
            {
                StrokeConnector(painter, from, to, color);
            }

            // A link being dragged runs straight from its node to the pointer
            if (_isLinking && _pressedNodeId != null && _positions.TryGetValue(_pressedNodeId, out Vector2 source))
            {
                painter.strokeColor = DependentLineColor;
                painter.BeginPath();
                painter.MoveTo(ToLocal(source));
                painter.LineTo(_linkEnd);
                painter.Stroke();
            }
        }

        private static void StrokeConnector(Painter2D painter, Vector2 prerequisite, Vector2 node, Color color)
        {
            List<Vector2> points = TechTreeCanvasGeometry.GetConnectorPoints(prerequisite, node);
            painter.strokeColor = color;
            painter.BeginPath();
            painter.MoveTo(ToLocal(points[0]));
            for (int i = 1; i < points.Count; i++)
            {
                painter.LineTo(ToLocal(points[i]));
            }

            painter.Stroke();
        }

        private static void FillRect(Painter2D painter, Rect rect, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(rect.min);
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(rect.max);
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
