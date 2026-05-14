using System;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// 4 x 4 matrix view for the per-(biome, layer) small-biome detail tile assignments.
    /// </summary>
    /// <remarks>
    /// Rows are biome channels (R/G/B/A), columns are layers 1-4. Each cell shows the bound
    /// albedo <see cref="Texture2D" /> and a 2-3 line at-a-glance summary covering altitude
    /// window, slope window, and weight. Clicking a cell selects it for the inline detail
    /// editor below the grid.
    /// <para>
    /// The trapezoidal window vector <c>hp</c> decodes as
    /// <c>(center, upRange, downRange, fadeOut)</c>, so the displayed altitude shoulders are
    /// computed as <c>leftShoulder = x - z</c> and <c>rightShoulder = x + y</c>. The slope
    /// vector <c>sp</c> follows the same convention.
    /// </para>
    /// <para>
    /// Layout lives in <c>Assets/Windows/PropertyFields/SmallLayerMatrix.uxml</c> with styling
    /// in <c>PropertyFields.uss</c>. Selection state and texture-change notifications are
    /// surfaced through <see cref="OnSelectionChanged" /> and <see cref="OnTextureChanged" />
    /// callbacks so the parent inspector can rebind detail fields and trigger the packer.
    /// </para>
    /// </remarks>
    public class SmallLayerMatrix : VisualElement
    {
        private const string UxmlPath = "/Assets/Windows/PropertyFields/SmallLayerMatrix.uxml";
        private const string UssPath = "/Assets/Windows/PropertyFields/PropertyFields.uss";

        // SerializedObject of the PQSDataAuthoring sidecar - tile texture fields bind here, not to PQSData.
        private readonly SerializedObject _authoringSO;
        private readonly Material _material;

        private readonly VisualElement[] _cells = new VisualElement[16];
        private readonly Label[] _summaries = new Label[16];

        private int _selectedBiome = -1;
        private int _selectedLayer = -1;

        /// <summary>
        /// Raised when a different cell is selected.
        /// </summary>
        /// <remarks>
        /// Arguments are the newly selected biome row index and layer column index.
        /// </remarks>
        public event Action<int, int> OnSelectionChanged;

        /// <summary>
        /// Raised when the bound albedo texture for a cell changes.
        /// </summary>
        /// <remarks>
        /// The argument is the cell slot index, computed as <c>biome * 4 + layer</c>.
        /// </remarks>
        public event Action<int> OnTextureChanged;

        /// <summary>
        /// Gets the currently selected biome row index, or -1 when no cell is selected.
        /// </summary>
        public int SelectedBiome => _selectedBiome;

        /// <summary>
        /// Gets the currently selected layer column index, or -1 when no cell is selected.
        /// </summary>
        public int SelectedLayer => _selectedLayer;

        /// <summary>
        /// Creates a new matrix view bound to the given PQSDataAuthoring serialized object and surface material.
        /// </summary>
        /// <param name="pqsDataAuthoringSO">The serialized PQSDataAuthoring sidecar hosting the small-tile property arrays.</param>
        /// <param name="material">The surface material used to read window and weight summary values.</param>
        public SmallLayerMatrix(SerializedObject pqsDataAuthoringSO, Material material)
        {
            _authoringSO = pqsDataAuthoringSO;
            _material = material;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree != null)
                tree.CloneTree(this);

            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + UssPath);
            if (styles != null)
                styleSheets.Add(styles);

            for (var b = 0; b < 4; b++)
            {
                var row = this.Q<VisualElement>("row-" + PqsAuthoringNaming.BiomeChannels[b]);
                if (row == null) continue;

                var rowHeader = new Label("Biome " + PqsAuthoringNaming.BiomeChannels[b]);
                rowHeader.AddToClassList("small-layer-matrix-row-header");
                row.Add(rowHeader);

                for (var l = 0; l < 4; l++)
                {
                    var slot = b * 4 + l;
                    var cell = BuildCell(b, l, slot);
                    row.Add(cell);
                    _cells[slot] = cell;
                }
            }
        }

        private VisualElement BuildCell(int biome, int layer, int slot)
        {
            var cell = new VisualElement();
            cell.AddToClassList("small-layer-matrix-cell");

            var preview = new VisualElement();
            preview.AddToClassList("small-layer-matrix-cell-preview");
            cell.Add(preview);

            // SmallLayerMaterial drop slot - the matrix's primary write target. Drag an SO in to
            // assign the slot's shared material. Clear it to fall back to slot-local override values.
            var materialField = new ObjectField
            {
                objectType = typeof(SmallLayerMaterial),
                allowSceneObjects = false,
                tooltip = "Drop a SmallLayerMaterial asset to share visual-identity defaults across bodies. Per-cell overrides remain accessible in the detail panel below.",
            };
            materialField.AddToClassList("small-layer-matrix-cell-material");
            var materialProp = _authoringSO?.FindProperty(PqsAuthoringNaming.SmallLayerSlotFieldPath(slot, "Material"));
            if (materialProp != null) materialField.BindProperty(materialProp);
            cell.Add(materialField);

            // contentHash-guarded so post-Bind spurious fires don't trigger the heavy Repack().
            var ovProp = _authoringSO?.FindProperty(PqsAuthoringNaming.SmallLayerSlotFieldPath(slot, "OverrideAlbedoTexture"));
            var localProp = _authoringSO?.FindProperty(PqsAuthoringNaming.SmallLayerSlotFieldPath(slot, "AlbedoTexture"));
            void OnRelevantChange()
            {
                OnTextureChanged?.Invoke(slot);
                UpdateCellState(biome, layer);
            }
            SmallBiomeDetailEditor.TrackChangedOnly(materialField, materialProp, OnRelevantChange);
            SmallBiomeDetailEditor.TrackChangedOnly(materialField, ovProp, OnRelevantChange);
            SmallBiomeDetailEditor.TrackChangedOnly(materialField, localProp, OnRelevantChange);
            SmallBiomeDetailEditor.ResetSlotOnMaterialCleared(materialField, _authoringSO, slot);

            var summary = new Label();
            summary.AddToClassList("small-layer-matrix-cell-summary");
            cell.Add(summary);
            _summaries[slot] = summary;

            cell.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                if (evt.target is ObjectField || (evt.target is VisualElement ve && ve.GetFirstAncestorOfType<ObjectField>() != null))
                    return;
                Select(biome, layer);
            });

            // Register the cell in _cells BEFORE the first UpdateCellState so its null-guards
            // don't bail and skip the initial preview / summary paint.
            _cells[slot] = cell;
            UpdateCellState(biome, layer);
            return cell;
        }

        private void UpdateCellState(int biome, int layer)
        {
            var slot = biome * 4 + layer;
            var cell = _cells[slot];
            var summary = _summaries[slot];
            if (cell == null || summary == null)
                return;

            var effectiveAlbedo = GetEffectiveAlbedo(slot);
            var hasAlbedo = effectiveAlbedo != null;

            // Preview: prefer the cached SmallLayerMaterial sphere thumbnail when this cell has
            // an SO assigned, falling back to the effective albedo otherwise. The cached preview
            // may be null on first display - render it lazily via delayCall so we don't stall
            // the inspector's layout pass, and re-enter UpdateCellState once it's ready.
            var preview = cell.Q<VisualElement>(className: "small-layer-matrix-cell-preview");
            if (preview != null)
            {
                var slotMaterial = GetSlotMaterial(slot);
                Texture2D bg = null;
                if (slotMaterial != null)
                {
                    bg = SmallLayerMaterialPreview.GetCached(slotMaterial);
                    if (bg == null)
                    {
                        var capturedBiome = biome;
                        var capturedLayer = layer;
                        var capturedMaterial = slotMaterial;
                        EditorApplication.delayCall += () =>
                        {
                            SmallLayerMaterialPreview.Render(capturedMaterial);
                            UpdateCellState(capturedBiome, capturedLayer);
                        };
                    }
                }
                bg ??= effectiveAlbedo;
                preview.style.backgroundImage = bg != null ? new StyleBackground(bg) : new StyleBackground();
            }

            cell.EnableInClassList("small-layer-matrix-cell--empty", !hasAlbedo);

            if (!hasAlbedo)
            {
                summary.text = "(empty - cell disabled)";
                summary.AddToClassList("small-layer-matrix-cell-empty-label");
                return;
            }

            summary.RemoveFromClassList("small-layer-matrix-cell-empty-label");

            if (_material == null)
            {
                summary.text = string.Empty;
                return;
            }

            var c = PqsAuthoringNaming.BiomeChannels[biome];
            var i = layer + 1;
            var hp = _material.GetVector($"_SmallBiome{c}HeightParams{i}");
            var sp = _material.GetVector($"_SmallBiome{c}SlopeParams{i}");
            var weight = _material.GetVector($"_SmallHeightWeight{c}")[layer];
            summary.text =
                $"alt {hp.x - hp.z:0}..{hp.x + hp.y:0} m\n" +
                $"slope {sp.x - sp.z:0}..{sp.x + sp.y:0}°\n" +
                $"wt {weight:0.##}";
        }

        /// <summary>
        /// Selects the cell at the given biome row and layer column.
        /// </summary>
        /// <param name="biome">The biome row index (0-3).</param>
        /// <param name="layer">The layer column index (0-3).</param>
        public void Select(int biome, int layer)
        {
            if (_selectedBiome >= 0 && _selectedLayer >= 0)
            {
                var prevSlot = _selectedBiome * 4 + _selectedLayer;
                _cells[prevSlot]?.RemoveFromClassList("small-layer-matrix-cell--selected");
            }
            _selectedBiome = biome;
            _selectedLayer = layer;
            var slot = biome * 4 + layer;
            _cells[slot]?.AddToClassList("small-layer-matrix-cell--selected");
            OnSelectionChanged?.Invoke(biome, layer);
        }

        /// <summary>
        /// Refreshes the at-a-glance summary text on every cell in the matrix.
        /// </summary>
        public void RefreshAllSummaries()
        {
            for (var b = 0; b < 4; b++)
                for (var l = 0; l < 4; l++)
                    UpdateCellState(b, l);
        }

        private Texture2D GetEffectiveAlbedo(int slot)
        {
            var authoring = _authoringSO?.targetObject as PQSDataAuthoring;
            if (authoring?.smallLayerSlots == null || slot < 0 || slot >= authoring.smallLayerSlots.Length)
                return null;
            return authoring.smallLayerSlots[slot]?.EffectiveAlbedoTexture;
        }

        private SmallLayerMaterial GetSlotMaterial(int slot)
        {
            var authoring = _authoringSO?.targetObject as PQSDataAuthoring;
            if (authoring?.smallLayerSlots == null || slot < 0 || slot >= authoring.smallLayerSlots.Length)
                return null;
            return authoring.smallLayerSlots[slot]?.Material;
        }
    }
}
