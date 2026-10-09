using System;
using System.Collections.Generic;
using System.Linq;
using Redux.TechTree;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The tech tree editor's canvas: a tree drawn where and how the R&amp;D Center draws it, which pans and zooms.
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

        /// <summary>
        /// Raised with the selected node's ID when the selection changes, or null when nothing is selected.
        /// </summary>
        public event Action<string> OnSelectionChanged;

        /// <summary>
        /// Raised with the zoom whenever it changes.
        /// </summary>
        public event Action<float> OnZoomChanged;

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
            if (evt.button != 0 && evt.button != 2)
                return;

            var node = (evt.target as VisualElement)?.GetFirstOfType<TechTreeNodeElement>();
            if (node != null && evt.button == 0)
            {
                Select(node.NodeId);
                evt.StopPropagation();
                return;
            }

            _isPanning = true;
            _hasPanMoved = false;
            _panStart = evt.position;
            _panOrigin = _pan;
            this.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_isPanning || !this.HasPointerCapture(evt.pointerId))
                return;

            Vector2 delta = (Vector2)evt.position - _panStart;
            _hasPanMoved |= delta.magnitude > CLICK_SLOP;
            _pan = _panOrigin + delta;
            ApplyTransform();
        }

        // A click on empty space, rather than a drag, clears the selection
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isPanning)
                return;

            _isPanning = false;
            this.ReleasePointer(evt.pointerId);
            if (!_hasPanMoved && evt.button == 0)
            {
                Select(null);
            }
        }

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
