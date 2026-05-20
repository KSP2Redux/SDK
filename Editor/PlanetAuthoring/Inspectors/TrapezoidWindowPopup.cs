using System;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Popout editor window for a trapezoidal-window Vector4 stored as <c>(center, upRange, downRange, fadeOut)</c>.
    /// </summary>
    /// <remarks>
    /// The shader's <c>ComputeFadeTri</c> produces a quadratic fade-in across
    /// <c>[center - downRange, center]</c>, a flat plateau across <c>[center, center + upRange]</c>,
    /// and a quadratic fade-out across <c>[center + upRange, center + upRange + fadeOut]</c>.
    /// This popup exposes the four edges of that shape via artist-facing names (Fade In, Start, End, Fade Out).
    /// Each of the four corner handles is independent. The body of the plateau translates the
    /// whole window. Shift = fine drag, Ctrl = snap to 100 m or 5 deg. Decoupled from any source of
    /// truth — callers pass in the initial value and a per-edit callback that receives the new value.
    /// </remarks>
    public class TrapezoidWindowPopup : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/TrapezoidWindowPopup.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        private const float XMin = 0f;
        private const float GraphLineWidth = 2f;

        private static readonly Vector2 WindowSize = new(380f, 360f);

        private TrapezoidWindowField.AxisMode _axisMode;
        private float _xMaxOverride;
        private Action<Vector4> _onValueChanged;

        private VisualElement _graph;
        private FloatField _startField;
        private FloatField _endField;
        private FloatField _fadeInField;
        private FloatField _fadeOutField;

        private Vector4 _value;
        private float _xAxisMax = GraphWidgetCommon.MinAxisMax;
        private Handle _activeHandle = Handle.None;
        private Vector2 _dragStartLocal;
        private Vector4 _dragStartValue;
        private float _newWindowAnchorX;

        private enum Handle { None, LeftFoot, LeftShoulder, RightShoulder, RightFoot, Body, NewWindow }

        /// <summary>
        /// Opens the trapezoid-window popup anchored below the supplied trigger rect.
        /// </summary>
        /// <param name="anchorWorldRect">World-space rect of the trigger element. The popup opens just below its bottom edge.</param>
        /// <param name="initialValue">Starting value displayed by the popup.</param>
        /// <param name="axisMode">Whether the X axis represents height or slope.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound for height mode. Pass <c>0</c> to use the default.</param>
        /// <param name="onValueChanged">Callback invoked with the new value after each edit. Material or PQSData writes happen outside the popup.</param>
        public static void Show(
            Rect anchorWorldRect,
            Vector4 initialValue,
            TrapezoidWindowField.AxisMode axisMode,
            float xMaxOverride,
            Action<Vector4> onValueChanged
        )
        {
            var window = CreateInstance<TrapezoidWindowPopup>();
            var unitLabel = axisMode == TrapezoidWindowField.AxisMode.Slope ? "Slope" : "Height";
            window.titleContent = new GUIContent($"{unitLabel} Window");
            window.minSize = WindowSize;
            window._value = initialValue;
            window._axisMode = axisMode;
            window._xMaxOverride = xMaxOverride;
            window._onValueChanged = onValueChanged;

            var anchorScreen = GUIUtility.GUIToScreenRect(anchorWorldRect);
            window.position = new Rect(anchorScreen.x, anchorScreen.yMax + 2, WindowSize.x, WindowSize.y);
            window.ShowAuxWindow();
        }

        private void CreateGUI()
        {
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

            _fadeInField = WireField("fadeIn", FadeIn, v =>
            {
                SetEdges(Start, End, Mathf.Max(0f, v), FadeOut);
                ApplyEdit();
            });
            _startField = WireField("start", Start, v =>
            {
                SetEdges(v, Mathf.Max(v, End), FadeIn, FadeOut);
                ApplyEdit();
            });
            _endField = WireField("end", End, v =>
            {
                SetEdges(Mathf.Min(Start, v), v, FadeIn, FadeOut);
                ApplyEdit();
            });
            _fadeOutField = WireField("fadeOut", FadeOut, v =>
            {
                SetEdges(Start, End, FadeIn, Mathf.Max(0f, v));
                ApplyEdit();
            });
        }

        private void AddModifierHint()
        {
            var unit = _axisMode == TrapezoidWindowField.AxisMode.Slope ? "5°" : "100 m";
            rootVisualElement.Add(GraphWidgetCommon.BuildModifierHint(unit));
        }

        // The four edge accessors map (center, upRange, downRange, fadeOut) onto an artist-
        // friendly (Fade In, Start, End, Fade Out) model that matches the shape the shader
        // actually produces. "Start" and "End" bracket the plateau (where the layer is at
        // full strength); "Fade In" and "Fade Out" are the widths of the quadratic ramps
        // below Start and above End.
        private float Start   => _value.x;
        private float End     => _value.x + _value.y;
        private float FadeIn  => _value.z;
        private float FadeOut => _value.w;

        private bool IsDegenerate() =>
            Mathf.Approximately(_value.y, 0f)
            && Mathf.Approximately(_value.z, 0f)
            && Mathf.Approximately(_value.w, 0f);

        private void SetEdges(float start, float end, float fadeIn, float fadeOut)
        {
            (start, end, fadeIn, fadeOut) =
                TrapezoidWindowGeometry.ClampToDomain(_axisMode, start, end, fadeIn, fadeOut);
            _value.x = start;                                          // center
            _value.y = end - start;                                    // upRange
            _value.z = fadeIn;                                         // downRange
            _value.w = fadeOut;                                        // fadeOut
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
            UpdateAxisRange();
            SyncFieldsFromValue();
            _onValueChanged?.Invoke(_value);
            _graph?.MarkDirtyRepaint();
        }

        private void SyncFieldsFromValue()
        {
            _fadeInField?.SetValueWithoutNotify(FadeIn);
            _startField?.SetValueWithoutNotify(Start);
            _endField?.SetValueWithoutNotify(End);
            _fadeOutField?.SetValueWithoutNotify(FadeOut);
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

            GraphWidgetCommon.DrawHandle(painter, leftFoot,      _activeHandle == Handle.LeftFoot);
            GraphWidgetCommon.DrawHandle(painter, leftShoulder,  _activeHandle == Handle.LeftShoulder);
            GraphWidgetCommon.DrawHandle(painter, rightShoulder, _activeHandle == Handle.RightShoulder);
            GraphWidgetCommon.DrawHandle(painter, rightFoot,     _activeHandle == Handle.RightFoot);

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
            var pLeftFoot      = GraphWidgetCommon.WorldToPixel(Start - FadeIn, 0f, XMin, _xAxisMax, rect);
            var pLeftShoulder  = GraphWidgetCommon.WorldToPixel(Start,          1f, XMin, _xAxisMax, rect);
            var pRightShoulder = GraphWidgetCommon.WorldToPixel(End,            1f, XMin, _xAxisMax, rect);
            var pRightFoot     = GraphWidgetCommon.WorldToPixel(End + FadeOut,  0f, XMin, _xAxisMax, rect);

            var hits = new (Handle h, float d)[]
            {
                (Handle.LeftFoot,      Vector2.Distance(local, pLeftFoot)),
                (Handle.LeftShoulder,  Vector2.Distance(local, pLeftShoulder)),
                (Handle.RightShoulder, Vector2.Distance(local, pRightShoulder)),
                (Handle.RightFoot,     Vector2.Distance(local, pRightFoot)),
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
            // The body is the flat plateau between Start and End.
            var pLeftShoulder  = GraphWidgetCommon.WorldToPixel(Start, 1f, XMin, _xAxisMax, rect);
            var pRightShoulder = GraphWidgetCommon.WorldToPixel(End,   1f, XMin, _xAxisMax, rect);

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
            if (_activeHandle == Handle.None)
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
            // Edge values captured at the start of the drag.
            var origStart     = v.x;
            var origEnd       = v.x + v.y;
            var origFadeIn    = v.z;
            var origFadeOut   = v.w;
            var origLeftFoot  = origStart - origFadeIn;
            var origRightFoot = origEnd + origFadeOut;

            switch (_activeHandle)
            {
                case Handle.LeftFoot:
                {
                    var newLeftFoot = GraphWidgetCommon.SnapIfNeeded(origLeftFoot + deltaWorldX, snap, snapStep);
                    if (newLeftFoot > origStart) newLeftFoot = origStart;
                    SetEdges(origStart, origEnd, origStart - newLeftFoot, origFadeOut);
                    break;
                }
                case Handle.LeftShoulder:
                {
                    var newStart = GraphWidgetCommon.SnapIfNeeded(origStart + deltaWorldX, snap, snapStep);
                    if (newStart > origEnd) newStart = origEnd;
                    if (newStart < origLeftFoot) newStart = origLeftFoot;
                    SetEdges(newStart, origEnd, newStart - origLeftFoot, origFadeOut);
                    break;
                }
                case Handle.RightShoulder:
                {
                    var newEnd = GraphWidgetCommon.SnapIfNeeded(origEnd + deltaWorldX, snap, snapStep);
                    if (newEnd < origStart) newEnd = origStart;
                    if (newEnd > origRightFoot) newEnd = origRightFoot;
                    SetEdges(origStart, newEnd, origFadeIn, origRightFoot - newEnd);
                    break;
                }
                case Handle.RightFoot:
                {
                    var newRightFoot = GraphWidgetCommon.SnapIfNeeded(origRightFoot + deltaWorldX, snap, snapStep);
                    if (newRightFoot < origEnd) newRightFoot = origEnd;
                    SetEdges(origStart, origEnd, origFadeIn, newRightFoot - origEnd);
                    break;
                }
                case Handle.Body:
                {
                    var shift = GraphWidgetCommon.SnapIfNeeded(deltaWorldX, snap, snapStep);
                    SetEdges(origStart + shift, origEnd + shift, origFadeIn, origFadeOut);
                    break;
                }
                case Handle.NewWindow:
                {
                    var anchor = _newWindowAnchorX;
                    var newStart = GraphWidgetCommon.SnapIfNeeded(Mathf.Min(anchor, currentWorldX), snap, snapStep);
                    var newEnd   = GraphWidgetCommon.SnapIfNeeded(Mathf.Max(anchor, currentWorldX), snap, snapStep);
                    SetEdges(newStart, newEnd, 0f, 0f);
                    break;
                }
            }

            ApplyEdit();
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
