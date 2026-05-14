using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== PARAMS 3.6: Small biome detail (matrix view) =====================

        /// <summary>
        /// Builds the Small biome detail foldout exposing the biome-by-layer matrix and per-cell editor.
        /// </summary>
        /// <remarks>
        /// Hosts a <see cref="SmallLayerMatrix" /> for cell selection and a slot that hosts the
        /// detail editor for the currently selected biome and layer. Tile texture changes drive a
        /// repack of the shared small-tile texture array.
        /// </remarks>
        /// <param name="material">The surface material whose small-biome detail properties are edited.</param>
        /// <param name="pqsDataSO">SerializedObject wrapping the bound PQSData for mirrored fields.</param>
        /// <param name="pqsData">The bound PQSData. Used to drive the small-tile texture-array repack.</param>
        /// <returns>The populated Small biome detail foldout.</returns>
        public static Foldout BuildSmallBiomeDetailSection(
            Material material,
            SerializedObject pqsDataSO,
            SerializedObject pqsDataAuthoringSO,
            PQSData pqsData
        )
        {
            var foldout = new Foldout { text = "Small biome detail", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            var helpBox = new HelpBox(string.Empty, HelpBoxMessageType.Warning)
            {
                style = { display = DisplayStyle.None },
            };
            foldout.Add(helpBox);

            var matrix = new SmallLayerMatrix(pqsDataAuthoringSO, material);
            foldout.Add(matrix);

            var detailSlot = new VisualElement();
            detailSlot.style.marginTop = 6;
            foldout.Add(detailSlot);

            // Cache one detail panel per cell. Building the panel costs ~hundreds of ms (15+
            // override rows, each with a PropertyField that does reflection on Bind), so reusing
            // the cached panel on every subsequent visit to the same cell makes selection
            // changes feel instant after the first build per cell.
            var cachedPanels = new VisualElement[16];
            VisualElement emptyHint = null;

            void Repack()
            {
                var result = Texture2DArrayPacker.RepackSmallTiles(pqsData, material);
                if (result.Success)
                {
                    helpBox.style.display = DisplayStyle.None;
                }
                else
                {
                    helpBox.text = result.ErrorMessage;
                    helpBox.style.display = DisplayStyle.Flex;
                }
                matrix.RefreshAllSummaries();
            }

            void RebuildDetail()
            {
                detailSlot.Clear();
                var biome = matrix.SelectedBiome;
                var layer = matrix.SelectedLayer;
                if (biome < 0 || layer < 0)
                {
                    if (emptyHint == null)
                    {
                        emptyHint = new Label("Select a cell in the matrix above to edit its layer parameters.");
                        emptyHint.AddToClassList("pqs-inspector-empty-selection");
                    }
                    detailSlot.Add(emptyHint);
                    return;
                }
                var slot = biome * 4 + layer;
                if (cachedPanels[slot] == null)
                {
                    cachedPanels[slot] = SmallBiomeDetailEditor.Build(
                        material, pqsDataSO, pqsDataAuthoringSO, pqsData,
                        biome, layer,
                        repackTiles: Repack
                    );
                }
                detailSlot.Add(cachedPanels[slot]);
            }

            matrix.OnSelectionChanged += (_, _) => RebuildDetail();
            matrix.OnTextureChanged += _ => Repack();

            RebuildDetail();
            return foldout;
        }
    }
}
