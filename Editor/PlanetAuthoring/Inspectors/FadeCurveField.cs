using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Inline preview field for a shader fade-vector property stored as <c>(start, range, nearOpacity, farOpacity)</c>.
    /// </summary>
    /// <remarks>
    /// Renders like Unity's <c>CurveField</c> or <c>ColorField</c>. The label sits on the left and a clickable preview
    /// button on the right shows a thumbnail of the curve plus the current value summary. Clicking the preview opens
    /// <see cref="FadeCurvePopup" /> for full editing. Layout lives in
    /// <c>Assets/Windows/PropertyFields/FadeCurveField.uxml</c> with styling in <c>PropertyFields.uss</c>. Drawing math,
    /// palette, and axis tuning live on <see cref="GraphWidgetCommon" /> and <see cref="FadeCurveGeometry" /> so they
    /// stay in sync with the popup.
    /// </remarks>
    public class FadeCurveField : VisualElement
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/FadeCurveField.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";
        private const float ThumbnailLineWidth = 1.5f;
        private const float XMin = 0f;

        private readonly Material _material;
        private readonly string _propertyName;
        private readonly Action _onChanged;

        private readonly VisualElement _previewButton;
        private readonly VisualElement _thumbnail;
        private readonly Label _summaryLabel;

        private Vector4 _value;
        private float _xAxisMax = GraphWidgetCommon.MinAxisMax;

        /// <summary>
        /// Creates a fade-curve inline preview field bound to a shader vector property on <paramref name="material" />.
        /// </summary>
        /// <param name="material">Material whose vector property is being edited.</param>
        /// <param name="propertyName">Shader property name on <paramref name="material" />.</param>
        /// <param name="label">Inspector label shown to the left of the preview.</param>
        /// <param name="tooltip">Tooltip displayed on the inspector label.</param>
        /// <param name="onChanged">Callback invoked after the popup writes a new value back to <paramref name="material" />.</param>
        public FadeCurveField(
            Material material,
            string propertyName,
            string label,
            string tooltip,
            Action onChanged
        )
        {
            _material = material;
            _propertyName = propertyName;
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
            _xAxisMax = FadeCurveGeometry.ComputeAxisMax(_value);
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
            _summaryLabel.text =
                $"{_value.x:0.#}..{_value.x + _value.y:0.#} m   " +
                $"{_value.z:0.##} -> {_value.w:0.##}";
        }

        private void OnPreviewClicked(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            var anchor = _previewButton.worldBound;
            FadeCurvePopup.Show(anchor, _material, _propertyName, OnPopupChanged);
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
            FadeCurveGeometry.DrawCurve(painter, _value, XMin, _xAxisMax, rect, ThumbnailLineWidth);

            var state = PlanetPreviewState.Active;
            if (state != null && state.HasTerrainSample)
                GraphWidgetCommon.DrawReferenceLineAt(painter, state.CameraDistanceFromSurface, XMin, _xAxisMax, rect);
        }
    }
}
