using System.Linq;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Fields
{
    /// <summary>
    /// Picker for the surface stratum a procedural texture rule applies to.
    /// </summary>
    /// <remarks>
    /// The value is the packed slice index, which is what <c>TerrainTextureRule.TextureIndex</c>
    /// stores and what the sampling compute shader compares against. The 0-15 cell index an author
    /// sees in the PQS inspector is a separate number, so the field shows both.
    /// <see cref="ScatterStratumLookup" /> owns the resolution between them.
    ///
    /// The numeric field is always present. It carries the value that is stored on disk, and it is
    /// the escape hatch for repairing imported or hand-written data that resolves to nothing.
    ///
    /// Cells that were never packed appear as disabled entries labelled as unpacked, so a slot
    /// filled in the PQS inspector that never made it into the array is still visible here.
    ///
    /// A body with no authoring sidecar hides the dropdown and keeps the numeric field.
    /// </remarks>
    public class ScatterStratumField : BaseField<int>
    {
        private const string UxmlPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/ScatterStratumField.uxml";
        private const string UssPath = "/Assets/Windows/PlanetAuthoring/PropertyFields/PropertyFields.uss";

        private readonly ScatterStratumLookup _lookup;
        private readonly VisualElement _swatch;
        private readonly Button _picker;
        private readonly IntegerField _rawIndex;
        private readonly Label _detail;

        /// <summary>
        /// Creates a stratum picker over a body's resolved strata.
        /// </summary>
        /// <param name="label">Inspector label shown to the left of the picker.</param>
        /// <param name="tooltip">Tooltip shown on hover.</param>
        /// <param name="lookup">The body's stratum lookup. Pass <c>ScatterStratumLookup.Unavailable</c> when names cannot be resolved.</param>
        public ScatterStratumField(string label, string tooltip, ScatterStratumLookup lookup)
            : base(label, null)
        {
            _lookup = lookup ?? ScatterStratumLookup.Unavailable;
            this.tooltip = tooltip;
            AddToClassList("unity-base-field__aligned");

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree != null)
            {
                tree.CloneTree(this);
            }

            // BaseField(label, null) leaves an empty .unity-base-field__input sibling that competes
            // with the cloned markup for row space. Drop it so the cloned wrapper owns the slot.
            this.Children()
                .FirstOrDefault(c => c.ClassListContains("unity-base-field__input") && c.childCount == 0)
                ?.RemoveFromHierarchy();

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
            {
                styleSheets.Add(styles);
            }

            _swatch = this.Q<VisualElement>("swatch");
            _picker = this.Q<Button>("picker");
            _rawIndex = this.Q<IntegerField>("raw-index");
            _detail = this.Q<Label>("detail");

            if (_picker != null)
            {
                _picker.clicked += ShowMenu;
                _picker.style.display = _lookup.IsAvailable ? DisplayStyle.Flex : DisplayStyle.None;
            }

            if (_swatch != null && !_lookup.IsAvailable)
            {
                _swatch.style.display = DisplayStyle.None;
            }

            _rawIndex?.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue != value)
                {
                    this.value = evt.newValue;
                }
            });

            UpdateDisplay();
        }

        /// <inheritdoc />
        public override void SetValueWithoutNotify(int newValue)
        {
            base.SetValueWithoutNotify(newValue);
            UpdateDisplay();
        }

        private void ShowMenu()
        {
            if (!_lookup.IsAvailable)
                return;

            var menu = new GenericDropdownMenu();
            foreach (ScatterStratum stratum in _lookup.Strata)
            {
                string entry = $"{stratum.DisplayName}  (cell {stratum.CellIndex})";
                if (!stratum.IsPacked)
                {
                    menu.AddDisabledItem($"{stratum.DisplayName}  (cell {stratum.CellIndex}, not packed)", false);
                    continue;
                }

                int slice = stratum.SliceIndex;
                menu.AddItem(entry, slice == value, () => value = slice);
            }

            menu.DropDown(_picker.worldBound, _picker, anchored: false);
        }

        private void UpdateDisplay()
        {
            if (_rawIndex != null && _rawIndex.value != value)
            {
                _rawIndex.SetValueWithoutNotify(value);
            }

            if (!_lookup.IsAvailable)
            {
                SetDetail("no stratum names for this body", warning: false);
                return;
            }

            if (!_lookup.TryGetBySlice(value, out ScatterStratum stratum))
            {
                if (_picker != null)
                {
                    _picker.text = "Unknown stratum";
                    _picker.EnableInClassList("scatter-stratum-picker--unresolved", true);
                }

                SetSwatch(null, null);
                SetDetail("matches no packed cell", warning: true);
                return;
            }

            if (_picker != null)
            {
                _picker.text = stratum.DisplayName;
                _picker.EnableInClassList("scatter-stratum-picker--unresolved", false);
            }

            SetSwatch(stratum.LayerMaterial, stratum.Albedo);
            SetDetail($"cell {stratum.CellIndex}", warning: false);
        }

        private void SetDetail(string text, bool warning)
        {
            if (_detail == null)
                return;

            _detail.text = text;
            _detail.EnableInClassList("scatter-stratum-detail--warning", warning);
        }

        private void SetSwatch(SmallLayerMaterial layerMaterial, Texture2D albedo)
        {
            if (_swatch == null)
                return;

            Texture2D image = null;
            if (layerMaterial != null)
            {
                // Same source and same lazy render the PQS inspector's cell grid uses, so a stratum
                // looks identical in both places. The cache is empty on first display.
                image = SmallLayerMaterialPreview.GetCached(layerMaterial);
                if (image == null)
                {
                    SmallLayerMaterial captured = layerMaterial;
                    EditorApplication.delayCall += () =>
                    {
                        if (panel == null)
                            return;

                        SmallLayerMaterialPreview.Render(captured);
                        UpdateDisplay();
                    };
                }
            }

            image ??= albedo;
            _swatch.style.backgroundImage = image != null ? new StyleBackground(image) : new StyleBackground();
        }
    }
}
