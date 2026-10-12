using System.Globalization;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Terrain tab: the PQS, its shader features, heightmaps, per-biome layers, poles, scaled view and
    /// surface bake.
    /// </summary>
    public static class TerrainTab
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
            PlanetInspectorContext context = view.Context;
            if (context.Pqs == null)
            {
                content.Add(new HelpBox("This body has no Local prefab with a PQS.", HelpBoxMessageType.Warning));
                return;
            }

            if (!PlanetInspectorView.CloneTemplate(content, "TerrainTab.uxml"))
                return;

            content.Q("pqs-fields").Bind(context.PqsObject);
            var advanced = content.Q("pqs-advanced");
            if (context.PqsData != null)
            {
                advanced.Bind(new SerializedObject(context.PqsData));
            }
            else
            {
                PlanetInspectorView.SetShown(advanced, false);
            }

            PQSRenderer renderer = context.Pqs.PQSRenderer != null ? context.Pqs.PQSRenderer : context.Pqs.GetComponentInChildren<PQSRenderer>(true);
            var rendererObject = renderer != null ? new SerializedObject(renderer) : null;
            foreach (string name in new[] { "pqs-rendering", "pqs-debug" })
            {
                VisualElement foldout = content.Q(name);
                PlanetInspectorView.SetShown(foldout, rendererObject != null);
                if (rendererObject != null)
                {
                    foldout.Bind(rendererObject);
                }
            }

            // Changing Data swaps every section below, so the tab is rebuilt against the new asset.
            SerializedProperty dataProperty = context.PqsObject.FindProperty("data");
            content.Q("pqs-fields").TrackPropertyValue(dataProperty, _ => view.RebuildTab());

            VisualElement sections = content.Q("terrain-sections");
            HelpBox problem = SurfaceAuthoringBuilder.TryResolve(context.Pqs, out SurfaceAuthoringBuilder.Inputs inputs);
            if (problem != null)
            {
                sections.Add(problem);
                PlanetInspectorView.SetShown(content.Q("section-layers"), false);
            }
            else
            {
                sections.Add(SurfaceAuthoringBuilder.BuildQualitySection(inputs.Material, inputs.DataObject, view.RebuildTab));
                sections.Add(SurfaceAuthoringBuilder.BuildHeightmapStackSection(inputs.DataObject, inputs.Material));
                WireLayerGrid(content, inputs);

                VisualElement late = content.Q("terrain-late-sections");
                late.Add(SurfaceAuthoringBuilder.BuildPoleSettingsSection(inputs.DataObject));
                late.Add(SurfaceAuthoringBuilder.BuildScaledSpaceSection(inputs.Material));
            }

            BodySurfaceBakeSection.Wire(content, () => context.Body);
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
