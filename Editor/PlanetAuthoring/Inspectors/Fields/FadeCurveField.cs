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
    /// Inline preview field for a Vector4 stored as <c>(start, range, nearOpacity, farOpacity)</c>.
    /// </summary>
    /// <remarks>
    /// Renders like Unity's <c>CurveField</c>. The label sits on the left (provided by <see cref="BaseField{T}" />)
    /// and a clickable preview on the right shows a thumbnail of the curve plus the current value summary.
    /// Clicking the preview opens <see cref="FadeCurvePopup" /> for full editing. The widget is decoupled
    /// from any specific source: callers wire it to a material/PQSData via the binder helpers and listen for
    /// value changes via the standard <c>RegisterValueChangedCallback</c> path.
    /// </remarks>
    public class FadeCurveField : BaseField<Vector4>
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/FadeCurveField.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";
        private const float ThumbnailLineWidth = 1.5f;
        private const float XMin = 0f;

        private readonly VisualElement _previewButton;
        private readonly VisualElement _thumbnail;
        private readonly Label _summaryLabel;

        private float _xAxisMax = GraphWidgetCommon.MinAxisMax;

        /// <summary>
        /// Creates a fade-curve inline preview field.
        /// </summary>
        /// <param name="label">Inspector label shown to the left of the preview.</param>
        /// <param name="tooltip">Tooltip shown on hover (routed onto the label by BaseField).</param>
        public FadeCurveField(string label, string tooltip) : base(label, null)
        {
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
            _xAxisMax = FadeCurveGeometry.ComputeAxisMax(value);
            if (_summaryLabel != null)
                _summaryLabel.text =
                    $"{value.x:0.#}..{value.x + value.y:0.#} m   " +
                    $"{value.z:0.##} -> {value.w:0.##}";
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
            FadeCurvePopup.Show(anchor, value, newValue => this.value = newValue);
            evt.StopPropagation();
        }

        private void DrawThumbnail(MeshGenerationContext mgc)
        {
            var rect = _thumbnail.contentRect;
            if (rect.width <= 0 || rect.height <= 0)
                return;

            var painter = mgc.painter2D;
            GraphWidgetCommon.DrawBackground(painter, rect);
            FadeCurveGeometry.DrawCurve(painter, value, XMin, _xAxisMax, rect, ThumbnailLineWidth);

            var state = PlanetPreviewState.Active;
            if (state != null && state.HasTerrainSample)
                GraphWidgetCommon.DrawReferenceLineAt(painter, state.CameraDistanceFromSurface, XMin, _xAxisMax, rect);
        }
    }
}
