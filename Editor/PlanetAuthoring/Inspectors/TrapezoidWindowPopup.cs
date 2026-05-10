using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Popout editor window for a shader trapezoidal-window vector property stored as <c>(center, upRange, downRange, fadeOut)</c> on the material.
    /// </summary>
    /// <remarks>
    /// Exposes the window through a simpler <c>(start, end, fade)</c> model in both numeric fields and drag handles, where
    /// <c>start = center - downRange</c>, <c>end = center + upRange</c>, and <c>fade = fadeOut</c>. Edits keep the
    /// underlying trapezoid symmetric so <c>upRange == downRange</c>. Asymmetric authoring is not supported through this widget.
    /// Drag interactions are split by handle. Shoulders adjust start (left) or end (right). Feet adjust fade symmetrically
    /// because the shader has a single shared <c>fadeOut</c>. The body of the trapezoid (the flat top) translates the whole
    /// window. When the window is fully collapsed (<c>start == end == 0</c> and <c>fade == 0</c>), a click-and-drag anywhere
    /// on the graph creates a new window between the click anchor and the current pointer position. Hold Shift for
    /// fine-grained drag. Hold Ctrl to snap to 100 m or 5 deg depending on axis mode.
    /// </remarks>
    public class TrapezoidWindowPopup : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PropertyFields/TrapezoidWindowPopup.uxml";
        private const string UssPath = "/Assets/Windows/PropertyFields/PropertyFields.uss";

        private const float XMin = 0f;
        private const float GraphLineWidth = 2f;

        private static readonly Vector2 WindowSize = new(380f, 320f);

        private Material _material;
        private string _propertyName;
        private TrapezoidWindowField.AxisMode _axisMode;
        private float _xMaxOverride;
        private Action _onChanged;

        private VisualElement _graph;
        private FloatField _startField;
        private FloatField _endField;
        private FloatField _fadeField;

        private Vector4 _value;
        private float _xAxisMax = GraphWidgetCommon.MinAxisMax;
        private Handle _activeHandle = Handle.None;
        private Vector2 _dragStartLocal;
        private Vector4 _dragStartValue;
        private float _newWindowAnchorX;

        private enum Handle { None, LeftShoulder, RightShoulder, LeftFoot, RightFoot, Body, NewWindow }

        /// <summary>
        /// Opens the trapezoid-window popup anchored below the supplied trigger rect.
        /// </summary>
        /// <param name="anchorWorldRect">World-space rect of the inline preview that triggered the popup. The window opens flush against its bottom edge.</param>
        /// <param name="material">Material whose vector property is being edited.</param>
        /// <param name="propertyName">Shader property name on <paramref name="material" />.</param>
        /// <param name="axisMode">Whether the X axis represents height or slope.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound for height mode. Pass <c>0</c> to use the default.</param>
        /// <param name="onChanged">Callback invoked after each edit writes a new value back to <paramref name="material" />.</param>
        public static void Show(
            Rect anchorWorldRect,
            Material material,
            string propertyName,
            TrapezoidWindowField.AxisMode axisMode,
            float xMaxOverride,
            Action onChanged
        )
        {
            var window = CreateInstance<TrapezoidWindowPopup>();
            var unitLabel = axisMode == TrapezoidWindowField.AxisMode.Slope ? "slope" : "height";
            window.titleContent = new GUIContent($"Trapezoid {unitLabel} window - {GraphWidgetCommon.PrettifyPropertyName(propertyName)}");
            window.minSize = WindowSize;
            window._material = material;
            window._propertyName = propertyName;
            window._axisMode = axisMode;
            window._xMaxOverride = xMaxOverride;
            window._onChanged = onChanged;

            var anchorScreen = GUIUtility.GUIToScreenRect(anchorWorldRect);
            window.position = new Rect(anchorScreen.x, anchorScreen.yMax + 2, WindowSize.x, WindowSize.y);
            window.ShowAuxWindow();
        }

        private void CreateGUI()
        {
            _value = _material != null ? _material.GetVector(_propertyName) : default;
            UpdateAxisRange();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                rootVisualElement.Add(new Label("Failed to load TrapezoidWindowPopup.uxml"));
                return;
            }
            tree.CloneTree(rootVisualElement);

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                rootVisualElement.styleSheets.Add(styles);

            _graph = rootVisualElement.Q<VisualElement>("graph");
            if (_graph != null)
            {
                _graph.generateVisualContent += DrawGraph;
                _graph.RegisterCallback<PointerDownEvent>(OnPointerDown);
                _graph.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                _graph.RegisterCallback<PointerUpEvent>(OnPointerUp);
            }

            AddModifierHint();

            PlanetPreviewState.ActiveChanged += OnPreviewStateChanged;

            _startField = WireField("start", GetStart(), v =>
            {
                SetStartEnd(v, GetEnd());
                ApplyEdit();
            });
            _endField = WireField("end", GetEnd(), v =>
            {
                SetStartEnd(GetStart(), v);
                ApplyEdit();
            });
            _fadeField = WireField("fadeOut", _value.w, v =>
            {
                _value.w = Mathf.Max(0f, v);
                ApplyEdit();
            });
        }

        private void AddModifierHint()
        {
            var unit = _axisMode == TrapezoidWindowField.AxisMode.Slope ? "5°" : "100 m";
            rootVisualElement.Add(GraphWidgetCommon.BuildModifierHint(unit));
        }

        private float GetStart() => _value.x - _value.z;
        private float GetEnd() => _value.x + _value.y;
        private bool IsDegenerate() => Mathf.Approximately(_value.y, 0f) && Mathf.Approximately(_value.z, 0f) && Mathf.Approximately(_value.w, 0f);

        private void SetStartEnd(float start, float end)
        {
            if (end < start) end = start;
            var center = (start + end) * 0.5f;
            var half = (end - start) * 0.5f;
            _value.x = center;
            _value.y = half;
            _value.z = half;
        }

        private FloatField WireField(string name, float initial, Action<float> onChange)
        {
            var field = rootVisualElement.Q<FloatField>(name);
            if (field == null)
                return null;
            field.SetValueWithoutNotify(initial);
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return field;
        }

        private void ApplyEdit()
        {
            if (_material == null)
                return;
            Undo.RecordObject(_material, "Edit trapezoid window");
            _material.SetVector(_propertyName, _value);
            EditorUtility.SetDirty(_material);
            UpdateAxisRange();
            SyncFieldsFromValue();
            _onChanged?.Invoke();
            SceneView.RepaintAll();
            _graph?.MarkDirtyRepaint();
        }

        private void SyncFieldsFromValue()
        {
            _startField?.SetValueWithoutNotify(GetStart());
            _endField?.SetValueWithoutNotify(GetEnd());
            _fadeField?.SetValueWithoutNotify(_value.w);
        }

        private void UpdateAxisRange()
        {
            _xAxisMax = TrapezoidWindowGeometry.ComputeAxisMax(_axisMode, _value, _xMaxOverride);
        }

        private void DrawGraph(MeshGenerationContext mgc)
        {
            var rect = _graph.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            var painter = mgc.painter2D;
            GraphWidgetCommon.DrawBackground(painter, rect);
            GraphWidgetCommon.DrawGrid(painter, rect);

            var (leftFoot, leftShoulder, rightShoulder, rightFoot) =
                TrapezoidWindowGeometry.DrawTrapezoid(painter, _value, XMin, _xAxisMax, rect, GraphLineWidth);

            GraphWidgetCommon.DrawHandle(painter, leftShoulder, _activeHandle == Handle.LeftShoulder);
            GraphWidgetCommon.DrawHandle(painter, rightShoulder, _activeHandle == Handle.RightShoulder);
            GraphWidgetCommon.DrawHandle(painter, leftFoot, _activeHandle == Handle.LeftFoot || _activeHandle == Handle.RightFoot);
            GraphWidgetCommon.DrawHandle(painter, rightFoot, _activeHandle == Handle.LeftFoot || _activeHandle == Handle.RightFoot);

            if (_activeHandle == Handle.LeftFoot || _activeHandle == Handle.RightFoot)
            {
                painter.strokeColor = TrapezoidWindowGeometry.FootIndicatorColor;
                painter.lineWidth = 1f;
                painter.BeginPath();
                painter.MoveTo(leftFoot);
                painter.LineTo(rightFoot);
                painter.Stroke();
            }

            if (_axisMode != TrapezoidWindowField.AxisMode.Slope)
            {
                var state = PlanetPreviewState.Active;
                if (state != null && state.HasTerrainSample)
                    GraphWidgetCommon.DrawReferenceLineAt(painter, state.TerrainElevationAtCamera, XMin, _xAxisMax, rect);
            }
        }

        private void OnPreviewStateChanged()
        {
            _graph?.MarkDirtyRepaint();
        }

        private void OnDisable()
        {
            PlanetPreviewState.ActiveChanged -= OnPreviewStateChanged;
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            var rect = _graph.contentRect;
            var local = (Vector2)evt.localPosition;

            if (!TryHitDegenerate(local, rect)
                && !TryHitHandles(local, rect)
                && !TryHitBody(local, rect))
                return;

            _dragStartLocal = local;
            _dragStartValue = _value;
            _graph.CapturePointer(evt.pointerId);
            _graph.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private bool TryHitDegenerate(Vector2 local, Rect rect)
        {
            if (!IsDegenerate())
                return false;
            _activeHandle = Handle.NewWindow;
            _newWindowAnchorX = GraphWidgetCommon.PixelToWorldX(local.x, XMin, _xAxisMax, rect);
            return true;
        }

        private bool TryHitHandles(Vector2 local, Rect rect)
        {
            var center = _value.x;
            var up = Mathf.Max(0f, _value.y);
            var down = Mathf.Max(0f, _value.z);
            var fade = Mathf.Max(0f, _value.w);

            var pLeftShoulder = GraphWidgetCommon.WorldToPixel(center - down, 1f, XMin, _xAxisMax, rect);
            var pRightShoulder = GraphWidgetCommon.WorldToPixel(center + up, 1f, XMin, _xAxisMax, rect);
            var pLeftFoot = GraphWidgetCommon.WorldToPixel(center - down - fade, 0f, XMin, _xAxisMax, rect);
            var pRightFoot = GraphWidgetCommon.WorldToPixel(center + up + fade, 0f, XMin, _xAxisMax, rect);

            var hits = new (Handle h, float d)[]
            {
                (Handle.LeftShoulder, Vector2.Distance(local, pLeftShoulder)),
                (Handle.RightShoulder, Vector2.Distance(local, pRightShoulder)),
                (Handle.LeftFoot, Vector2.Distance(local, pLeftFoot)),
                (Handle.RightFoot, Vector2.Distance(local, pRightFoot)),
            };
            var nearest = Handle.None;
            var nearestDist = float.MaxValue;
            foreach (var (h, d) in hits)
            {
                if (d < nearestDist)
                {
                    nearest = h;
                    nearestDist = d;
                }
            }

            if (nearestDist > GraphWidgetCommon.HandleHitRadius)
                return false;

            _activeHandle = nearest;
            return true;
        }

        private bool TryHitBody(Vector2 local, Rect rect)
        {
            var center = _value.x;
            var up = Mathf.Max(0f, _value.y);
            var down = Mathf.Max(0f, _value.z);

            var pLeftShoulder = GraphWidgetCommon.WorldToPixel(center - down, 1f, XMin, _xAxisMax, rect);
            var pRightShoulder = GraphWidgetCommon.WorldToPixel(center + up, 1f, XMin, _xAxisMax, rect);

            var topY = Mathf.Min(pLeftShoulder.y, pRightShoulder.y) - 4f;
            var bodyY = pLeftShoulder.y;
            if (local.x < pLeftShoulder.x || local.x > pRightShoulder.x)
                return false;
            if (local.y < topY || local.y > bodyY + 6f)
                return false;

            _activeHandle = Handle.Body;
            return true;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_activeHandle == Handle.None || _material == null)
                return;

            var rect = _graph.contentRect;
            var fine = evt.shiftKey;
            var snap = evt.ctrlKey || evt.commandKey;
            var snapStep = _axisMode == TrapezoidWindowField.AxisMode.Slope
                ? GraphWidgetCommon.SnapStepSlope
                : GraphWidgetCommon.SnapStepHeight;
            var fineScale = fine ? GraphWidgetCommon.FineDragScale : 1f;

            var local = (Vector2)evt.localPosition;
            var startWorldX = GraphWidgetCommon.PixelToWorldX(_dragStartLocal.x, XMin, _xAxisMax, rect);
            var currentWorldX = GraphWidgetCommon.PixelToWorldX(local.x, XMin, _xAxisMax, rect);
            var deltaWorldX = (currentWorldX - startWorldX) * fineScale;

            var v = _dragStartValue;

            switch (_activeHandle)
            {
                case Handle.LeftShoulder:
                {
                    var oldStart = v.x - v.z;
                    var oldEnd = v.x + v.y;
                    var newStart = GraphWidgetCommon.SnapIfNeeded(oldStart + deltaWorldX, snap, snapStep);
                    if (newStart > oldEnd) newStart = oldEnd;
                    SetValueFromStartEnd(ref v, newStart, oldEnd);
                    break;
                }
                case Handle.RightShoulder:
                {
                    var oldStart = v.x - v.z;
                    var oldEnd = v.x + v.y;
                    var newEnd = GraphWidgetCommon.SnapIfNeeded(oldEnd + deltaWorldX, snap, snapStep);
                    if (newEnd < oldStart) newEnd = oldStart;
                    SetValueFromStartEnd(ref v, oldStart, newEnd);
                    break;
                }
                case Handle.LeftFoot:
                {
                    var newFade = Mathf.Max(0f, v.w - deltaWorldX);
                    v.w = GraphWidgetCommon.SnapIfNeeded(newFade, snap, snapStep);
                    break;
                }
                case Handle.RightFoot:
                {
                    var newFade = Mathf.Max(0f, v.w + deltaWorldX);
                    v.w = GraphWidgetCommon.SnapIfNeeded(newFade, snap, snapStep);
                    break;
                }
                case Handle.Body:
                {
                    v.x += deltaWorldX;
                    v.x = GraphWidgetCommon.SnapIfNeeded(v.x, snap, snapStep);
                    break;
                }
                case Handle.NewWindow:
                {
                    var anchor = _newWindowAnchorX;
                    var newStart = Mathf.Min(anchor, currentWorldX);
                    var newEnd = Mathf.Max(anchor, currentWorldX);
                    newStart = GraphWidgetCommon.SnapIfNeeded(newStart, snap, snapStep);
                    newEnd = GraphWidgetCommon.SnapIfNeeded(newEnd, snap, snapStep);
                    SetValueFromStartEnd(ref v, newStart, newEnd);
                    break;
                }
            }

            _value = v;
            ApplyEdit();
        }

        private static void SetValueFromStartEnd(ref Vector4 v, float start, float end)
        {
            if (end < start) end = start;
            var center = (start + end) * 0.5f;
            var half = (end - start) * 0.5f;
            v.x = center;
            v.y = half;
            v.z = half;
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (_activeHandle == Handle.None)
                return;

            _graph.ReleasePointer(evt.pointerId);
            _activeHandle = Handle.None;
            _graph.MarkDirtyRepaint();
        }
    }
}
