using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Clouds
{
    /// <summary>
    /// The settings a cloud layer's distribution map is generated from, kept so the map can be regenerated.
    /// </summary>
    [Serializable]
    public class CloudDistributionSettings
    {
        /// <summary>
        /// The layer these settings belong to.
        /// </summary>
        public string LayerName;

        /// <summary>
        /// Picks a different map with the same character.
        /// </summary>
        public int Seed = 1;

        /// <summary>
        /// The size of the largest cloud regions, in kilometers.
        /// </summary>
        [Min(1f)] public float FeatureSizeKm = 150f;

        /// <summary>
        /// How many octaves of finer detail are layered on.
        /// </summary>
        /// <remarks>
        /// The finest octave has to vary coverage within one repeat of the layer's base noise, a few kilometers, or every
        /// repeat looks the same.
        /// </remarks>
        [Range(1, 8)] public int Octaves = 8;

        /// <summary>
        /// How strongly each finer octave shows, from smooth to ragged.
        /// </summary>
        [Range(0.1f, 0.9f)] public float Roughness = 0.55f;

        /// <summary>
        /// How far the noise is bent into streaks and swirls.
        /// </summary>
        [Range(0f, 8f)] public float WarpStrength = 2f;

        /// <summary>
        /// The share of the sky with any cloud, from none to all of it.
        /// </summary>
        /// <remarks>
        /// Stock Kerbin's map covers about 30 percent.
        /// </remarks>
        [Tooltip("The share of the sky with any cloud. Stock Kerbin's map covers about 0.3.")]
        [Range(0f, 1f)] public float Coverage = 0.3f;

        /// <summary>
        /// The map's average value over the covered sky.
        /// </summary>
        /// <remarks>
        /// Values fall off exponentially from the densest cloud, as stock's do, so this also sets how rarely the map
        /// gets near 1. Stock Kerbin's averages about 0.12, and the stock layer settings presets copy expect a map that
        /// faint.
        /// </remarks>
        [Tooltip("The map's average value where there is cloud. Stock Kerbin's is about 0.12, and stock layer settings expect a map that faint.")]
        [Range(0.01f, 1f)] public float Density = 0.125f;

        /// <summary>
        /// Coverage scale from the equator, at 0, to the poles, at 1.
        /// </summary>
        public AnimationCurve LatitudeProfile = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        /// <summary>
        /// The edge length of each cube face, in texels.
        /// </summary>
        public int Resolution = 1024;

        /// <summary>
        /// Store the map as BC4, which the cloud shaders read in full since they only use the first channel.
        /// </summary>
        public bool Compress = true;
    }
}
