using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Classifies a body into one of <see cref="BodyClassFlags" /> based on its data.
    /// </summary>
    /// <remarks>
    /// Computed once per validation run and reused to filter <see cref="IPlanetValidator.AppliesTo" />.
    /// </remarks>
    public static class BodyClassClassifier
    {
        /// <summary>
        /// Returns the body class of <paramref name="body" />.
        /// </summary>
        /// <remarks>
        /// Each body resolves to exactly one class. Star wins over solid-surface when both are flagged
        /// (a contradictory state caught separately by the STAR_HAS_SOLID_SURFACE validator, which
        /// runs unconditionally because it does not narrow its <see cref="IPlanetValidator.AppliesTo" />).
        /// Null bodies and missing data return <see cref="BodyClassFlags.None" />.
        /// </remarks>
        /// <param name="body">The body to classify. May be null.</param>
        public static BodyClassFlags Classify(CoreCelestialBodyData body)
        {
            var data = body?.Core?.data;
            if (data == null)
                return BodyClassFlags.None;
            if (data.isStar)
                return BodyClassFlags.Star;
            if (!data.hasSolidSurface)
                return BodyClassFlags.GasGiant;
            return BodyClassFlags.SolidSurface;
        }
    }
}
