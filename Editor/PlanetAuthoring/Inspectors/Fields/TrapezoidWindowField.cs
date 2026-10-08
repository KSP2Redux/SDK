using System.Linq;
using Ksp2UnityTools.Editor;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Inline preview field for a trapezoidal-window Vector4 stored as <c>(center, upRange, downRange, fadeOut)</c>.
    /// </summary>
    /// <remarks>
    /// Renders like Unity's <c>CurveField</c>. One widget covers both height windows (X axis in meters,
    /// range <c>[0, planetMaxAltitude]</c>) and slope windows (X axis in degrees, range <c>[0, 90]</c>).
    /// The widget is decoupled from any specific source: callers wire it via the binder helpers and
    /// listen for value changes via the standard <c>RegisterValueChangedCallback</c> path.
    /// </remarks>
    public class TrapezoidWindowField : BaseField<Vector4>
    {
        /// <summary>
        /// X-axis interpretation for a trapezoid window field.
        /// </summary>
        public enum AxisMode
        {
            /// <summary>X axis is altitude in meters, ranging from zero to the planet maximum.</summary>
            Height,
            /// <summary>X axis is slope in degrees, fixed at <c>[0, 90]</c>.</summary>
            Slope,
        }

        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/TrapezoidWindowField.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";
        private const float ThumbnailLineWidth = 1.5f;
        private const float XMin = 0f;

        private readonly AxisMode _axisMode;
        private readonly float _xMaxOverride;

        private readonly VisualElement _previewButton;
        private readonly VisualElement _thumbnail;
        private readonly Label _summaryLabel;

        private float _xAxisMax;

        /// <summary>
        /// Creates a trapezoid-window inline preview field.
        /// </summary>
        /// <param name="label">Inspector label shown to the left of the preview.</param>
        /// <param name="tooltip">Tooltip shown on hover (routed onto the label by BaseField).</param>
        /// <param name="axisMode">Whether the X axis represents height or slope.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound for height mode. Pass <c>0</c> to use the default.</param>
        public TrapezoidWindowField(
            string label,
            string tooltip,
            AxisMode axisMode,
            float xMaxOverride = 0f
        ) : base(label, null)
        {
            _axisMode = axisMode;
            _xMaxOverride = xMaxOverride;
            this.tooltip = tooltip;
            AddToClassList("unity-base-field__aligned");

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            // BaseField(label, null) still adds an empty .unity-base-field__input sibling that competes
            // with the cloned UXML's __input for row space. Remove it so the cloned wrapper owns the input slot.
            this.Children()
                .FirstOrDefault(c => c.ClassListContains("unity-base-field__input") && c.childCount == 0)
                ?.RemoveFromHierarchy();

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                styleSheets.Add(styles);

            _previewButton = this.Q<VisualElement>("preview-button");
            _thumbnail = this.Q<VisualElement>("thumbnail");
            _summaryLabel = this.Q<Label>("summary");

            if (_thumbnail != null)
                _thumbnail.generateVisualContent += DrawThumbnail;

            if (_previewButton != null)
                _previewButton.RegisterCallback<PointerDownEvent>(OnPreviewClicked);

            RegisterCallback<AttachToPanelEvent>(_ => PlanetPreviewState.ActiveChanged += OnPreviewStateChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => PlanetPreviewState.ActiveChanged -= OnPreviewStateChanged);

            UpdateDisplay();
        }

        /// <inheritdoc />
        public override void SetValueWithoutNotify(Vector4 newValue)
        {
            base.SetValueWithoutNotify(newValue);
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            _xAxisMax = TrapezoidWindowGeometry.ComputeAxisMax(_axisMode, value, _xMaxOverride);
            if (_summaryLabel != null)
            {
                var unit = _axisMode == AxisMode.Slope ? "°" : "m";
                // Match the popup field labels: Start..End is the plateau, In/Out are the quadratic ramp widths.
                var (start, end, fadeIn, fadeOut) =
                    TrapezoidWindowGeometry.ClampToDomain(_axisMode, value.x, value.x + value.y, value.z, value.w);
                _summaryLabel.text =
                    $"{start:0.#}..{end:0.#}{unit}   in {fadeIn:0.#}   out {fadeOut:0.#}";
            }
            _thumbnail?.MarkDirtyRepaint();
        }

        private void OnPreviewStateChanged()
        {
            _thumbnail?.MarkDirtyRepaint();
        }

        private void OnPreviewClicked(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            var anchor = _previewButton.worldBound;
            TrapezoidWindowPopup.Show(anchor, value, _axisMode, _xMaxOverride, newValue => this.value = newValue);
            evt.StopPropagation();
        }

        private void DrawThumbnail(MeshGenerationContext mgc)
        {
            var rect = _thumbnail.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            var painter = mgc.painter2D;
            GraphWidgetCommon.DrawBackground(painter, rect);
            TrapezoidWindowGeometry.DrawTrapezoid(painter, value, XMin, _xAxisMax, rect, ThumbnailLineWidth);

            if (_axisMode != AxisMode.Slope)
            {
                var state = PlanetPreviewState.Active;
                if (state != null && state.HasTerrainSample)
                    GraphWidgetCommon.DrawReferenceLineAt(painter, state.TerrainElevationAtCamera, XMin, _xAxisMax, rect);
            }
        }
    }
}
