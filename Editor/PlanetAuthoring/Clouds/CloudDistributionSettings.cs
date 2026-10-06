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
        [Range(1, 8)] public int Octaves = 5;

        /// <summary>
        /// How strongly each finer octave shows, from smooth to ragged.
        /// </summary>
        [Range(0.1f, 0.9f)] public float Roughness = 0.55f;

        /// <summary>
        /// How far the noise is bent into streaks and swirls.
        /// </summary>
        [Range(0f, 8f)] public float WarpStrength = 2f;

        /// <summary>
        /// Roughly how much of the sky is covered, from none to all of it.
        /// </summary>
        [Range(0f, 1f)] public float Coverage = 0.3f;

        /// <summary>
        /// How soft the edges of covered regions are.
        /// </summary>
        [Range(0.001f, 0.5f)] public float Softness = 0.08f;

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
