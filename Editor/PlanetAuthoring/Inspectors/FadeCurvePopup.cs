using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Popout editor window for a Vector4 stored as <c>(start, range, nearOpacity, farOpacity)</c>.
    /// </summary>
    /// <remarks>
    /// Hosts the full graph with drag handles plus aligned numeric fields for direct keyed input. The window is anchored
    /// to its trigger element via <see cref="EditorWindow.ShowAuxWindow" /> so it opens as a draggable utility window
    /// that closes when focus leaves the editor. Drag interactions use Shift for fine drag (0.2x) and Ctrl for snap to
    /// <see cref="GraphWidgetCommon.SnapStepHeight" />, mirroring the trapezoid window popup. The popup is decoupled
    /// from any source of truth: callers pass in the initial value and a per-edit callback that receives the new value.
    /// </remarks>
    public class FadeCurvePopup : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/FadeCurvePopup.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        private const float XMin = 0f;
        private const float GraphLineWidth = 2f;

        private static readonly Vector2 WindowSize = new(360f, 280f);

        private Action<Vector4> _onValueChanged;

        private VisualElement _graph;
        private FloatField _startField;
        private FloatField _rangeField;
        private FloatField _nearField;
        private FloatField _farField;

        private Vector4 _value;
        private float _xAxisMax = GraphWidgetCommon.MinAxisMax;
        private Handle _activeHandle = Handle.None;
        private Vector2 _dragStartLocal;
        private Vector4 _dragStartValue;

        private enum Handle { None, Left, Right }

        /// <summary>
        /// Opens the fade-curve popup anchored below the supplied trigger rect.
        /// </summary>
        /// <param name="anchorWorldRect">World-space rect of the inline preview that triggered the popup. The window opens flush against its bottom edge.</param>
        /// <param name="initialValue">Starting value displayed by the popup.</param>
        /// <param name="onValueChanged">Callback invoked with the new value after each edit. Material or PQSData writes happen outside the popup.</param>
        public static void Show(Rect anchorWorldRect, Vector4 initialValue, Action<Vector4> onValueChanged)
        {
            var window = CreateInstance<FadeCurvePopup>();
            window.titleContent = new GUIContent("Fade Curve");
            window.minSize = WindowSize;
            window._value = initialValue;
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
                rootVisualElement.Add(new Label("Failed to load FadeCurvePopup.uxml"));
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

            rootVisualElement.Add(GraphWidgetCommon.BuildModifierHint("100 m"));

            PlanetPreviewState.ActiveChanged += OnPreviewStateChanged;

            _startField = WireField("start", _value.x, v => { _value.x = Mathf.Max(0f, v); ApplyEdit(); });
            _rangeField = WireField("range", _value.y, v => { _value.y = Mathf.Max(0f, v); ApplyEdit(); });
            _nearField = WireField("near", _value.z, v => { _value.z = Mathf.Clamp01(v); ApplyEdit(); });
            _farField = WireField("far", _value.w, v => { _value.w = Mathf.Clamp01(v); ApplyEdit(); });
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
            _startField?.SetValueWithoutNotify(_value.x);
            _rangeField?.SetValueWithoutNotify(_value.y);
            _nearField?.SetValueWithoutNotify(_value.z);
            _farField?.SetValueWithoutNotify(_value.w);
        }

        private void UpdateAxisRange()
        {
            _xAxisMax = FadeCurveGeometry.ComputeAxisMax(_value);
        }

        private void DrawGraph(MeshGenerationContext mgc)
        {
            var rect = _graph.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            var painter = mgc.painter2D;
            GraphWidgetCommon.DrawBackground(painter, rect);
            GraphWidgetCommon.DrawGrid(painter, rect);

            var (_, pKneeLeft, pKneeRight, _) =
                FadeCurveGeometry.DrawCurve(painter, _value, XMin, _xAxisMax, rect, GraphLineWidth);

            GraphWidgetCommon.DrawHandle(painter, pKneeLeft, _activeHandle == Handle.Left);
            GraphWidgetCommon.DrawHandle(painter, pKneeRight, _activeHandle == Handle.Right);

            var state = PlanetPreviewState.Active;
            if (state != null && state.HasTerrainSample)
                GraphWidgetCommon.DrawReferenceLineAt(painter, state.CameraDistanceFromSurface, XMin, _xAxisMax, rect);
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
            var pLeft = GraphWidgetCommon.WorldToPixel(_value.x, _value.z, XMin, _xAxisMax, rect);
            var pRight = GraphWidgetCommon.WorldToPixel(_value.x + _value.y, _value.w, XMin, _xAxisMax, rect);
            var dLeft = Vector2.Distance(local, pLeft);
            var dRight = Vector2.Distance(local, pRight);
            var nearest = Mathf.Min(dLeft, dRight);

            if (nearest > GraphWidgetCommon.HandleHitRadius)
                return;

            _activeHandle = dLeft <= dRight ? Handle.Left : Handle.Right;
            _dragStartLocal = local;
            _dragStartValue = _value;
            _graph.CapturePointer(evt.pointerId);
            _graph.MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_activeHandle == Handle.None)
                return;

            var rect = _graph.contentRect;
            var fine = evt.shiftKey;
            var snap = evt.ctrlKey || evt.commandKey;
            var fineScale = fine ? GraphWidgetCommon.FineDragScale : 1f;
            var snapStep = GraphWidgetCommon.SnapStepHeight;

            var local = (Vector2)evt.localPosition;
            var startWorldX = GraphWidgetCommon.PixelToWorldX(_dragStartLocal.x, XMin, _xAxisMax, rect);
            var currentWorldX = GraphWidgetCommon.PixelToWorldX(local.x, XMin, _xAxisMax, rect);
            var deltaX = (currentWorldX - startWorldX) * fineScale;

            var startNormY = Mathf.Clamp01(Mathf.InverseLerp(rect.yMax, rect.yMin, _dragStartLocal.y));
            var currentNormY = Mathf.Clamp01(Mathf.InverseLerp(rect.yMax, rect.yMin, local.y));
            var deltaY = (currentNormY - startNormY) * fineScale;

            var v = _dragStartValue;

            if (_activeHandle == Handle.Left)
            {
                var oldStart = v.x;
                var oldEnd = v.x + v.y;
                var newStart = GraphWidgetCommon.SnapIfNeeded(oldStart + deltaX, snap, snapStep);
                newStart = Mathf.Clamp(newStart, 0f, oldEnd);
                v.x = newStart;
                v.y = oldEnd - newStart;
                v.z = Mathf.Clamp01(v.z + deltaY);
            }
            else
            {
                var oldEnd = v.x + v.y;
                var newEnd = GraphWidgetCommon.SnapIfNeeded(oldEnd + deltaX, snap, snapStep);
                newEnd = Mathf.Max(newEnd, v.x);
                v.y = newEnd - v.x;
                v.w = Mathf.Clamp01(v.w + deltaY);
            }

            _value = v;
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
