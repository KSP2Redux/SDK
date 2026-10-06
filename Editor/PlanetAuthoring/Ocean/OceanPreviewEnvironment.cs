using KSP.Rendering.Planets;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Ocean
{
    /// <summary>
    /// The ocean environment of a planet preview: the camera being rendered, the session's sun, and editor time.
    /// </summary>
    /// <remarks>
    /// The authoring scene has no time warp and its body has no ambient probe, so the probe reads as empty.
    /// </remarks>
    public class OceanPreviewEnvironment : IWaterEnvironment
    {
        /// <summary>
        /// Gets or sets the camera the ocean draws for.
        /// </summary>
        public Camera Camera { get; set; }

        /// <summary>
        /// Gets or sets the seconds the water animation advances this frame.
        /// </summary>
        public float DeltaTime { get; set; }

        /// <inheritdoc />
        public Light GetLocalLight() => SunCoupling.CurrentSun;

        /// <inheritdoc />
        public SphericalHarmonicsL2 GetGIProbeSHTerm(string bodyName) => default;
    }
}
