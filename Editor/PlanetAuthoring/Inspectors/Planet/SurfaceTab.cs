using System.Globalization;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Surface tab: the biome map, the per-biome layers, small detail and blending.
    /// </summary>
    public static class SurfaceTab
    {
        private const string SELECTED_LAYER_KEY = "PlanetAuthoring.BiomeLayer";
        private const string DEFAULT_LAYER = "large-r";
        private const string ACTIVE_CLASS = "is-active";
        private static readonly string[] TIERS = { "large", "mid", "sub3", "sub4" };

        /// <summary>
        /// Builds the tab into <paramref name="content" />.
        /// </summary>
        /// <param name="view">The inspector.</param>
        /// <param name="content">The tab content area.</param>
        public static void Build(PlanetInspectorView view, VisualElement content)
        {
            HelpBox problem = SurfaceAuthoringBuilder.TryResolve(view.Context.Pqs, out SurfaceAuthoringBuilder.Inputs inputs);
            if (problem != null)
            {
                content.Add(problem);
                return;
            }

            if (!PlanetInspectorView.CloneTemplate(content, "SurfaceTab.uxml"))
                return;

            VisualElement biome = content.Q("surface-biome-sections");
            biome.Add(SurfaceAuthoringBuilder.BuildBiomeControlSection(inputs.Material, inputs.DataObject));
            biome.Add(SurfaceAuthoringBuilder.BuildBiomeLookupBakeSection(inputs.Material, inputs.AuthoringObject, inputs.Data));
            biome.Add(SurfaceAuthoringBuilder.BuildTriplanarSection(inputs.Material));

            WireLayerGrid(content, inputs);

            VisualElement detail = content.Q("surface-detail-sections");
            detail.Add(SurfaceAuthoringBuilder.BuildSmallBiomeDetailSection(inputs.Material, inputs.DataObject, inputs.AuthoringObject, inputs.Data));
            detail.Add(SurfaceAuthoringBuilder.BuildDecalsSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildDistanceCascadeSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildCrossBiomeBlendSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildMiscSection(inputs.Material));
        }

        private static void WireLayerGrid(VisualElement content, SurfaceAuthoringBuilder.Inputs inputs)
        {
            bool subzones = inputs.SubzonesOn;
            content.Query(className: "planet-layer-grid__subzone").ForEach(element => PlanetInspectorView.SetShown(element, subzones));

            PQSDataAuthoring authoring = AuthoringSidecars.Find(inputs.Data);
            string[] channels = PlanetAuthoringNaming.BiomeChannels;
            for (var index = 0; index < channels.Length; index++)
            {
                string biomeName = BiomeName(authoring, index);
                foreach (string tier in TIERS)
                {
                    var cell = content.Q<Button>(CellName(tier, channels[index]));
                    cell.text = biomeName;
                    cell.AddToClassList("planet-live-ok");
                    string captured = CellName(tier, channels[index]).Substring("layer-".Length);
                    cell.clicked += () => Select(content, inputs, captured);
                }
            }

            string selected = SessionState.GetString(SELECTED_LAYER_KEY, DEFAULT_LAYER);
            if (!subzones && selected.StartsWith("sub"))
            {
                selected = DEFAULT_LAYER;
            }

            Select(content, inputs, selected);
        }

        private static void Select(VisualElement content, SurfaceAuthoringBuilder.Inputs inputs, string layer)
        {
            SessionState.SetString(SELECTED_LAYER_KEY, layer);
            content.Query<Button>(className: "planet-layer-grid__cell")
                .ForEach(cell => cell.EnableInClassList(ACTIVE_CLASS, cell.name == $"layer-{layer}"));

            VisualElement detail = content.Q("layer-detail");
            detail.Clear();
            Foldout section = BuildLayerSection(inputs, layer);
            if (section == null)
                return;

            section.value = true;
            detail.Add(section);
        }

        // A layer is "<tier>-<channel>", such as "mid-g".
        private static Foldout BuildLayerSection(SurfaceAuthoringBuilder.Inputs inputs, string layer)
        {
            string[] parts = layer.Split('-');
            if (parts.Length != 2)
                return null;

            string channel = parts[1].ToUpperInvariant();
            int index = System.Array.IndexOf(PlanetAuthoringNaming.BiomeChannels, channel);
            if (index < 0)
                return null;

            return parts[0] switch
            {
                "large" => SurfaceAuthoringBuilder.BuildLargeBiomeSection(inputs.Material, inputs.DataObject, channel, index, inputs.SubzonesOn),
                "mid" => SurfaceAuthoringBuilder.BuildMidBiomeSection(inputs.Material, inputs.DataObject, channel, index, inputs.SubzonesOn),
                "sub3" => SurfaceAuthoringBuilder.BuildSubzoneTierBiomeSection(inputs.Material, inputs.DataObject, inputs.AuthoringObject, inputs.Data, 3, channel, index),
                "sub4" => SurfaceAuthoringBuilder.BuildSubzoneTierBiomeSection(inputs.Material, inputs.DataObject, inputs.AuthoringObject, inputs.Data, 4, channel, index),
                _ => null,
            };
        }

        private static string CellName(string tier, string channel) => $"layer-{tier}-{channel.ToLowerInvariant()}";

        // The biome type the channel maps to, as a label: "ROCK" reads "Rock". An unmapped channel shows its letter.
        private static string BiomeName(PQSDataAuthoring authoring, int index)
        {
            if (authoring == null || authoring.biomeChannelMapping == null || index >= authoring.biomeChannelMapping.Length)
                return PlanetAuthoringNaming.BiomeChannels[index];

            PQSData.KSP2BiomeType type = authoring.biomeChannelMapping[index];
            if (type == PQSData.KSP2BiomeType.NONE)
                return PlanetAuthoringNaming.BiomeChannels[index];

            string words = type.ToString().Replace('_', ' ').ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words);
        }
    }
}
