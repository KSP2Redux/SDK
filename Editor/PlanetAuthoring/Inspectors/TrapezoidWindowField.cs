using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inline preview field for a shader trapezoidal-window vector property stored as <c>(center, upRange, downRange, fadeOut)</c>.
    /// </summary>
    /// <remarks>
    /// Renders like Unity's <c>CurveField</c> or <c>ColorField</c>. The label sits on the left and a clickable preview
    /// button on the right shows a thumbnail of the trapezoid plus the current value summary. Clicking the preview opens
    /// <see cref="TrapezoidWindowPopup" /> for full editing. One widget covers both height windows (X axis in meters,
    /// range <c>[0, planetMaxAltitude]</c>) and slope windows (X axis in degrees, range <c>[0, 90]</c>). Layout lives in
    /// <c>Assets/Windows/PlanetAuthoring/PropertyFields/TrapezoidWindowField.uxml</c> with styling shared in <c>PropertyFields.uss</c>.
    /// Drawing math, palette, and axis tuning live on <see cref="TrapezoidWindowGeometry" /> so they stay in sync with
    /// the popup.
    /// </remarks>
    public class TrapezoidWindowField : VisualElement
    {
        /// <summary>
        /// X-axis interpretation for a trapezoid window field.
        /// </summary>
        public enum AxisMode
        {
            /// <summary>
            /// X axis is altitude in meters, ranging from zero to the planet maximum.
            /// </summary>
            Height,
            /// <summary>
            /// X axis is slope in degrees, fixed at <c>[0, 90]</c>.
            /// </summary>
            Slope,
        }

        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/TrapezoidWindowField.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";
        private const float ThumbnailLineWidth = 1.5f;
        private const float XMin = 0f;

        private readonly Material _material;
        private readonly string _propertyName;
        private readonly AxisMode _axisMode;
        private readonly float _xMaxOverride;
        private readonly Action _onChanged;

        private readonly VisualElement _previewButton;
        private readonly VisualElement _thumbnail;
        private readonly Label _summaryLabel;

        private Vector4 _value;
        private float _xAxisMax;

        /// <summary>
        /// Creates a trapezoid-window inline preview field bound to a shader vector property on <paramref name="material" />.
        /// </summary>
        /// <param name="material">Material whose vector property is being edited.</param>
        /// <param name="propertyName">Shader property name on <paramref name="material" />.</param>
        /// <param name="label">Inspector label shown to the left of the preview.</param>
        /// <param name="tooltip">Tooltip displayed on the inspector label.</param>
        /// <param name="axisMode">Whether the X axis represents height or slope.</param>
        /// <param name="xMaxOverride">Explicit X-axis upper bound for height mode. Pass <c>0</c> to use the default.</param>
        /// <param name="onChanged">Callback invoked after the popup writes a new value back to <paramref name="material" />.</param>
        public TrapezoidWindowField(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            AxisMode axisMode,
            float xMaxOverride,
            Action onChanged
        )
        {
            _material = material;
            _propertyName = propertyName;
            _axisMode = axisMode;
            _xMaxOverride = xMaxOverride;
            _onChanged = onChanged;

            RefreshFromMaterial();

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                styleSheets.Add(styles);

            var labelEl = this.Q<Label>("field-label");
            if (labelEl != null)
            {
                labelEl.text = label;
                labelEl.tooltip = tooltip;
            }

            _previewButton = this.Q<VisualElement>("preview-button");
            _thumbnail = this.Q<VisualElement>("thumbnail");
            _summaryLabel = this.Q<Label>("summary");

            if (_thumbnail != null)
                _thumbnail.generateVisualContent += DrawThumbnail;

            if (_previewButton != null)
                _previewButton.RegisterCallback<PointerDownEvent>(OnPreviewClicked);

            UpdateSummary();

            RegisterCallback<AttachToPanelEvent>(_ => PlanetPreviewState.ActiveChanged += OnPreviewStateChanged);
            RegisterCallback<DetachFromPanelEvent>(_ => PlanetPreviewState.ActiveChanged -= OnPreviewStateChanged);
        }

        private void OnPreviewStateChanged()
        {
            _thumbnail?.MarkDirtyRepaint();
        }

        private void RefreshFromMaterial()
        {
            _value = _material != null ? _material.GetVector(_propertyName) : default;
            _xAxisMax = TrapezoidWindowGeometry.ComputeAxisMax(_axisMode, _value, _xMaxOverride);
        }

        /// <summary>
        /// Re-reads the bound material property and repaints the thumbnail and value summary.
        /// </summary>
        public void Refresh()
        {
            RefreshFromMaterial();
            UpdateSummary();
            _thumbnail?.MarkDirtyRepaint();
        }

        private void UpdateSummary()
        {
            if (_summaryLabel == null)
                return;
            var unit = _axisMode == AxisMode.Slope ? "°" : "m";
            // The shader's ComputeFadeTri produces a quadratic fade-in across [center-down, center],
            // a flat plateau across [center, center+up], and a quadratic fade-out across
            // [center+up, center+up+fadeOut]. The summary names match the popup's field labels:
            // Start..End is the plateau, In/Out are the quadratic ramp widths on either side.
            // Clamping mirrors the popup so a stale out-of-domain material value still displays sanely.
            var (start, end, fadeIn, fadeOut) =
                TrapezoidWindowGeometry.ClampToDomain(_axisMode, _value.x, _value.x + _value.y, _value.z, _value.w);
            _summaryLabel.text =
                $"{start:0.#}..{end:0.#}{unit}   in {fadeIn:0.#}   out {fadeOut:0.#}";
        }

        private void OnPreviewClicked(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            var anchor = _previewButton.worldBound;
            TrapezoidWindowPopup.Show(anchor, _material, _propertyName, _axisMode, _xMaxOverride, OnPopupChanged);
            evt.StopPropagation();
        }

        private void OnPopupChanged()
        {
            Refresh();
            _onChanged?.Invoke();
        }

        private void DrawThumbnail(MeshGenerationContext mgc)
        {
            var rect = _thumbnail.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            var painter = mgc.painter2D;
            GraphWidgetCommon.DrawBackground(painter, rect);
            TrapezoidWindowGeometry.DrawTrapezoid(painter, _value, XMin, _xAxisMax, rect, ThumbnailLineWidth);

            if (_axisMode != AxisMode.Slope)
            {
                var state = PlanetPreviewState.Active;
                if (state != null && state.HasTerrainSample)
                    GraphWidgetCommon.DrawReferenceLineAt(painter, state.TerrainElevationAtCamera, XMin, _xAxisMax, rect);
            }
        }
    }
}
