using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Shared editor preference for the "Surface" framing altitude in meters.
    /// </summary>
    /// <remarks>
    /// Both the Preview Controls' Surface jump button and the Science Region inspector's
    /// "Look from surface" button read from here so a single slider drives both.
    /// </remarks>
    public static class SurfaceFramingPrefs
    {
        private const string PrefKey = "Ksp2UnityTools.Editor.PlanetAuthoring.SurfaceJumpAltitude";

        /// <summary>
        /// Minimum allowed surface framing altitude, in meters.
        /// </summary>
        public const float MinMeters = 1.0f;

        /// <summary>
        /// Maximum allowed surface framing altitude, in meters.
        /// </summary>
        public const float MaxMeters = 10.0f;

        /// <summary>
        /// Default surface framing altitude used when no preference has been saved, in meters.
        /// </summary>
        public const float DefaultMeters = 2.0f;

        /// <summary>
        /// Gets or sets the persisted surface framing altitude in meters, clamped to <see cref="MinMeters" />..<see cref="MaxMeters" />.
        /// </summary>
        public static float AltitudeMeters
        {
            get => EditorPrefs.GetFloat(PrefKey, DefaultMeters);
            set => EditorPrefs.SetFloat(PrefKey, Mathf.Clamp(value, MinMeters, MaxMeters));
        }
    }
}
