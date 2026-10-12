using Ksp2UnityTools.Editor.PlanetAuthoring.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// A tab of the planet inspector.
    /// </summary>
    public enum PlanetInspectorTab
    {
        /// <summary>The body as a whole: identity, size, rotation, orbit, saving.</summary>
        General,

        /// <summary>The PQS shape: heightmaps, poles, the scaled view and its bake.</summary>
        Terrain,

        /// <summary>How the ground looks: biome map, layers, small detail, blending.</summary>
        Surface,

        /// <summary>Atmosphere, clouds, ocean, lighting, post-processing, heat and rings.</summary>
        Environment,

        /// <summary>Everything placed on the surface: landmarks, decals, spawners, scatter.</summary>
        Features,

        /// <summary>Science regions, discoverables and resources.</summary>
        Science,
    }

    /// <summary>
    /// Decides which planet inspector tabs a body shows.
    /// </summary>
    public static class PlanetInspectorTabs
    {
        /// <summary>
        /// Every tab, in tab bar order.
        /// </summary>
        public static readonly PlanetInspectorTab[] ALL =
        {
            PlanetInspectorTab.General,
            PlanetInspectorTab.Terrain,
            PlanetInspectorTab.Surface,
            PlanetInspectorTab.Environment,
            PlanetInspectorTab.Features,
            PlanetInspectorTab.Science,
        };

        /// <summary>
        /// Gets whether a body of <paramref name="bodyClass" /> shows <paramref name="tab" />.
        /// </summary>
        /// <remarks>
        /// Terrain, Surface and Features edit the PQS and what stands on it, which only a solid body has. Gas giants
        /// and stars, and a body whose data is missing, do not show them.
        /// </remarks>
        /// <param name="tab">The tab.</param>
        /// <param name="bodyClass">The body's class.</param>
        /// <returns>True if the tab is shown, false otherwise.</returns>
        public static bool IsShown(PlanetInspectorTab tab, BodyClassFlags bodyClass) =>
            tab is not (PlanetInspectorTab.Terrain or PlanetInspectorTab.Surface or PlanetInspectorTab.Features)
            || bodyClass == BodyClassFlags.SolidSurface;

        /// <summary>
        /// Gets the tab to open for a body, falling back to General when the remembered one does not apply.
        /// </summary>
        /// <param name="remembered">The tab last open.</param>
        /// <param name="bodyClass">The body's class.</param>
        /// <returns>The tab to open.</returns>
        public static PlanetInspectorTab Resolve(PlanetInspectorTab remembered, BodyClassFlags bodyClass) =>
            IsShown(remembered, bodyClass) ? remembered : PlanetInspectorTab.General;
    }
}
