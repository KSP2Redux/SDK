namespace Ksp2UnityTools.Editor.PlanetAuthoring.Overlays
{
    /// <summary>
    /// Identifies a preview overlay surfaced through the Preview Controls window.
    /// </summary>
    public enum PreviewOverlayKind
    {
        /// <summary>
        /// Colored RGBA-channel view of the body's biome mask texture.
        /// </summary>
        BiomeMask,

        /// <summary>
        /// Colored RGBA-channel view of the body's subzone mask texture.
        /// </summary>
        SubzoneMask,

        /// <summary>
        /// Slope visualization derived from the surface shader's prepass world-normal RT.
        /// </summary>
        Slope,

        /// <summary>
        /// Altitude contour bands derived from elevation above the planet radius.
        /// </summary>
        AltitudeBands,

        /// <summary>
        /// Per-pixel winner of the 16 small-biome layers, colorized by biome channel and layer index.
        /// </summary>
        ActiveLayer,

        /// <summary>
        /// Science region visualization in either baked-palette or source-texture mode.
        /// </summary>
        ScienceRegion,
    }
}
