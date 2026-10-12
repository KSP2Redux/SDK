using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Builds the Surface tab: the biome map, small detail and blending.
    /// </summary>
    public static class SurfaceTab
    {
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

            VisualElement detail = content.Q("surface-detail-sections");
            detail.Add(SurfaceAuthoringBuilder.BuildSmallBiomeDetailSection(inputs.Material, inputs.DataObject, inputs.AuthoringObject, inputs.Data));
            detail.Add(SurfaceAuthoringBuilder.BuildDecalsSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildDistanceCascadeSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildCrossBiomeBlendSection(inputs.Material));
            detail.Add(SurfaceAuthoringBuilder.BuildMiscSection(inputs.Material));
        }
    }
}
