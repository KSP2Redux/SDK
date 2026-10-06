using KSP.Rendering.Planets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Terrain tab: the PQS, its shader features, heightmaps, poles, scaled view and surface bake.
    /// </summary>
    public static class TerrainTab
    {
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
            }
            else
            {
                sections.Add(SurfaceAuthoringBuilder.BuildQualitySection(inputs.Material, inputs.DataObject, view.RebuildTab));
                sections.Add(SurfaceAuthoringBuilder.BuildHeightmapStackSection(inputs.DataObject, inputs.Material));
                sections.Add(SurfaceAuthoringBuilder.BuildPoleSettingsSection(inputs.DataObject));
                sections.Add(SurfaceAuthoringBuilder.BuildScaledSpaceSection(inputs.Material));
            }

            BodySurfaceBakeSection.Wire(content, () => context.Body);
        }
    }
}
