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
                detailSlot.Add(SmallBiomeDetailEditor.Build(
                    material, pqsDataSO, pqsDataAuthoringSO, pqsData,
                    matrix.SelectedBiome, matrix.SelectedLayer,
                    repackTiles: Repack
                ));
            }

            matrix.OnSelectionChanged += (_, _) => RebuildDetail();
            matrix.OnTextureChanged += _ => Repack();

            RebuildDetail();
            return foldout;
        }
    }
}
