using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Biomes;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    public static partial class SurfaceAuthoringBuilder
    {
        // ===================== Biome lookup bake =====================

        /// <summary>
        /// Builds the foldout that lets the artist map each <c>_BiomeMaskTex</c> channel (and
        /// optionally each biome+subzone pair) to a <see cref="PQSData.KSP2BiomeType" />, then
        /// bake the resulting hash table via <see cref="BiomeLookupBaker" />.
        /// </summary>
        /// <remarks>
        /// Replaces the deleted BiomeLookupUtility flow. The 4x4 subzone matrix is rendered only
        /// when SUB_ZONES_ENABLED is on, mirroring how the rest of the inspector gates subzone
        /// fields. NONE in a subzone slot means "inherit the row's base mapping".
        /// </remarks>
        /// <param name="material">The surface material whose keywords gate the subzone matrix.</param>
        /// <param name="pqsDataAuthoringSO">Serialized object for the PQSData authoring sidecar that holds the channel and subzone mappings.</param>
        /// <param name="pqsData">The runtime PQSData asset receiving the baked hash table.</param>
        /// <returns>The biome lookup foldout.</returns>
        public static Foldout BuildBiomeLookupBakeSection(Material material, SerializedObject pqsDataAuthoringSO, PQSData pqsData)
        {
            var foldout = new Foldout { text = "Biome lookup", value = true };
            foldout.AddToClassList("pqs-inspector-section");

            if (pqsDataAuthoringSO == null)
            {
                foldout.Add(new HelpBox(
                    "PQSDataAuthoring sidecar missing. Save the PQSData asset, then reopen the inspector.",
                    HelpBoxMessageType.Warning));
                return foldout;
            }

            bool subzonesOn = material.IsKeywordEnabled("SUB_ZONES_ENABLED");
            foldout.Add(BuildChannelMappingGrid(pqsDataAuthoringSO));
            if (subzonesOn)
                foldout.Add(BuildSubzoneMappingGrid(pqsDataAuthoringSO));

            var bakeStatus = new Label(string.Empty);
            bakeStatus.AddToClassList("sdk-hint");
            var bakeButton = new Button(() =>
            {
                pqsDataAuthoringSO.ApplyModifiedProperties();
                BiomeLookupBaker.BakeResult result = BiomeLookupBaker.Bake(pqsData);
                bakeStatus.text = result.Success ? $"Baked: {result.HashTablePath}" : $"Bake failed: {result.Message}";
            })
            {
                text = "Bake biome lookup",
            };
            foldout.Add(bakeButton);
            foldout.Add(bakeStatus);

            return foldout;
        }

        // 4-row grid: each row is a R/G/B/A biome channel with an EnumField for its KSP2BiomeType.
        private static VisualElement BuildChannelMappingGrid(SerializedObject pqsDataAuthoringSO)
        {
            var container = new VisualElement();
            container.Add(GroupLabel("Channel mapping"));
            for (int i = 0; i < PqsAuthoringNaming.BiomeChannels.Length; i++)
            {
                string channel = PqsAuthoringNaming.BiomeChannels[i];
                string path = $"biomeChannelMapping.Array.data[{i}]";
                var prop = pqsDataAuthoringSO.FindProperty(path);
                var field = new PropertyField(prop, $"Biome {channel}")
                {
                    tooltip = $"KSP2BiomeType assigned to mask channel {channel}. " +
                              "Used by physics, footstep audio, and surface classification. " +
                              "Set to NONE to leave the channel unmapped.",
                };
                field.BindProperty(prop);
                container.Add(field);
            }
            return container;
        }

        // 4x4 grid: rows = biome channel, columns = subzone channel. NONE in a cell inherits the
        // row's base mapping so artists fill only the cells where a subzone meaningfully changes
        // the surface type (e.g. arctic subzone of a grass biome reads as SNOW instead of GRASS).
        private static VisualElement BuildSubzoneMappingGrid(SerializedObject pqsDataAuthoringSO)
        {
            var container = new VisualElement();
            container.Add(GroupLabel("Subzone overrides (NONE = inherit row)"));
            for (int biome = 0; biome < PqsAuthoringNaming.BiomeChannels.Length; biome++)
            {
                string biomeChannel = PqsAuthoringNaming.BiomeChannels[biome];
                for (int subzone = 0; subzone < PqsAuthoringNaming.BiomeChannels.Length; subzone++)
                {
                    string subzoneChannel = PqsAuthoringNaming.BiomeChannels[subzone];
                    var index = PqsAuthoringNaming.CellIndex(biome, subzone);
                    var path = $"biomeSubzoneMapping.Array.data[{index}]";
                    var prop = pqsDataAuthoringSO.FindProperty(path);
                    var field = new PropertyField(prop, $"Biome {biomeChannel} (Subzone {subzoneChannel})")
                    {
                        tooltip = $"Override KSP2BiomeType for biome channel {biomeChannel} when " +
                                  $"subzone channel {subzoneChannel} is dominant. NONE keeps the " +
                                  $"row's base mapping.",
                    };
                    field.BindProperty(prop);
                    container.Add(field);
                }
            }
            return container;
        }
    }
}
