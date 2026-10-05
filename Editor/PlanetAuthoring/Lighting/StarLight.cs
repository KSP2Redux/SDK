using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Lighting
{
    /// <summary>
    /// Lights the preview sun the way the game lights a body from its star: the star's brightness, falling off with the
    /// body's distance along Kerbol's curve, in Kerbol's colour.
    /// </summary>
    public static class StarLight
    {
        /// <summary>
        /// Kerbin's distance from Kerbol in gigameters, its semi major axis.
        /// </summary>
        public const float KERBIN_DISTANCE_GIGAMETERS = 13.59984f;

        /// <summary>
        /// The distance in meters at which Kerbol's light has fallen to nothing.
        /// </summary>
        public const double FALLOFF_DISTANCE = 5e11;

        // Kerbol's intensityAtPeriapsis, the light it gives before falloff. Star brightness is a multiple of this.
        private const float KERBOL_INTENSITY = 1.75f;

        private const double METERS_PER_GIGAMETER = 1e9;
        private const string PREFS_PREFIX = "Ksp2UnityTools.PlanetPreview.Sun.";

        /// <summary>
        /// Kerbol's light colour, which the game gives the local star light.
        /// </summary>
        public static readonly Color STAR_COLOR = new(0.918f, 0.914f, 0.824f, 1f);

        // Kerbol's falloff curve over distance as a fraction of the falloff distance: steep near the star and flat by
        // the end. Clamped past both ends, as the stock curve is.
        private static readonly AnimationCurve FALLOFF_CURVE = new(
            new Keyframe(0f, 1f, -2.801886796951294f, -2.801886796951294f),
            new Keyframe(1f, 0f, 0f, 0f)
        );

        /// <summary>
        /// Gets or sets the star's brightness as a multiple of Kerbol's, kept across sessions.
        /// </summary>
        public static float StarBrightness
        {
            get => EditorPrefs.GetFloat(PREFS_PREFIX + "StarBrightness", 1f);
            set => EditorPrefs.SetFloat(PREFS_PREFIX + "StarBrightness", Mathf.Max(0f, value));
        }

        /// <summary>
        /// Gets or sets the body's distance from its star in gigameters, kept across sessions.
        /// </summary>
        public static float DistanceGigameters
        {
            get => EditorPrefs.GetFloat(PREFS_PREFIX + "DistanceGigameters", KERBIN_DISTANCE_GIGAMETERS);
            set => EditorPrefs.SetFloat(PREFS_PREFIX + "DistanceGigameters", Mathf.Max(0f, value));
        }

        /// <summary>
        /// Calculates the intensity of the star's light at a distance, as the game sets its local star light.
        /// </summary>
        /// <param name="starBrightness">The star's brightness as a multiple of Kerbol's.</param>
        /// <param name="distanceGigameters">The distance from the star in gigameters.</param>
        /// <returns>The light intensity.</returns>
        public static float CalculateLightIntensity(float starBrightness, double distanceGigameters) =>
            starBrightness
            * KERBOL_INTENSITY
            * FALLOFF_CURVE.Evaluate((float)(distanceGigameters * METERS_PER_GIGAMETER / FALLOFF_DISTANCE));

        /// <summary>
        /// Calculates how bright the star's light is at a distance compared with the light Kerbin gets.
        /// </summary>
        /// <param name="starBrightness">The star's brightness as a multiple of Kerbol's.</param>
        /// <param name="distanceGigameters">The distance from the star in gigameters.</param>
        /// <returns>The ratio, where 1 is Kerbin's sunlight.</returns>
        public static float CalculateRelativeToKerbin(float starBrightness, double distanceGigameters) =>
            CalculateLightIntensity(starBrightness, distanceGigameters)
            / CalculateLightIntensity(1f, KERBIN_DISTANCE_GIGAMETERS);

        /// <summary>
        /// Lights a light from the stored star brightness and distance.
        /// </summary>
        /// <param name="light">The light, usually the preview sun.</param>
        public static void Apply(Light light)
        {
            if (light == null)
                return;

            light.intensity = CalculateLightIntensity(StarBrightness, DistanceGigameters);
            light.color = STAR_COLOR;
        }
    }
}
