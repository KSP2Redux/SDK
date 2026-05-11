namespace Ksp2UnityTools.Editor.PlanetAuthoring.Science
{
    /// <summary>
    /// Constants shared between the Science Region inspector and the SR_* validators.
    /// </summary>
    /// <remarks>
    /// Hoisted out of <c>ScienceRegionEditor</c> so the validators in
    /// <c>Editor.PlanetAuthoring.Validation.Validators</c> don't have to take a reverse dependency
    /// on the inspector. Same numeric value as before. Single declaration so a future tolerance
    /// tweak lands once.
    /// </remarks>
    internal static class ScienceRegionConstants
    {
        /// <summary>
        /// Normalized Euclidean RGB distance below which two region colors are considered "the same".
        /// </summary>
        /// <remarks>
        /// Tuned for the bake's nearest-color classifier and reused for the inspector's collision
        /// warnings so both surfaces agree on what counts as a collision.
        /// </remarks>
        public const float ColorCollisionTolerance = 0.06f;
    }
}
