using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Combines a biome mask's four coverage channels with their four noise fields into the single
    /// density value a resource map stores.
    /// </summary>
    public static class ResourceMapComposer
    {
        /// <summary>
        /// Coverage total below which a pixel is treated as belonging to no biome at all.
        /// </summary>
        public const float COVERAGE_EPSILON = 1e-4f;

        /// <summary>
        /// Lowest overlay brightness <see cref="ComputeAutoMapBrightness" /> will return.
        /// </summary>
        public const int MIN_MAP_BRIGHTNESS = 1;

        /// <summary>
        /// Highest overlay brightness <see cref="ComputeAutoMapBrightness" /> will return, matching the runtime's own fallback.
        /// </summary>
        public const int MAX_MAP_BRIGHTNESS = 10;

        /// <summary>
        /// Composes one pixel's density from its biome coverage and its per-biome noise.
        /// </summary>
        /// <remarks>
        /// The weighted sum is divided by the total coverage rather than used raw, because the stock
        /// masks are not a partition of unity. Moho's four channels average 142 of 255, so a raw sum
        /// would land at roughly half the intended density across most of the body, while its
        /// doubly covered pixels would run past 1.
        /// </remarks>
        /// <param name="coverage">The mask's four channels at this pixel, each 0 to 1.</param>
        /// <param name="noise">Each channel's noise value at this pixel, each 0 to 1.</param>
        /// <returns>Density in the range 0 to 1.</returns>
        public static float Compose(Vector4 coverage, Vector4 noise)
        {
            float totalCoverage = coverage.x + coverage.y + coverage.z + coverage.w;
            if (totalCoverage <= COVERAGE_EPSILON)
                return 0f;

            float weighted = coverage.x * noise.x
                + coverage.y * noise.y
                + coverage.z * noise.z
                + coverage.w * noise.w;

            return Mathf.Clamp01(weighted / totalCoverage);
        }

        /// <summary>
        /// Derives the overlay brightness that makes a baked map readable in map view.
        /// </summary>
        /// <remarks>
        /// Brightness only scales the map-view overlay and never affects mining yield, so it can be
        /// derived from the map itself. Targeting the 99th percentile rather than the maximum keeps
        /// a handful of bright outliers from flattening the rest of the map. The formula reproduces
        /// both of the values that were set deliberately in the shipped definitions: Kerbin's water
        /// map peaks near full white and is set to 1, Moho's ammonia map peaks near 35 of 255 and is
        /// set to 10.
        /// </remarks>
        /// <param name="densities">The baked map's density values, each 0 to 1.</param>
        /// <returns>An overlay brightness between <see cref="MIN_MAP_BRIGHTNESS" /> and <see cref="MAX_MAP_BRIGHTNESS" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="densities" /> is null.</exception>
        public static int ComputeAutoMapBrightness(float[] densities)
        {
            if (densities == null)
                throw new ArgumentNullException(nameof(densities));
            if (densities.Length == 0)
                return MAX_MAP_BRIGHTNESS;

            float percentile = Percentile(densities, 0.99f);
            if (percentile <= 1e-6f)
                return MAX_MAP_BRIGHTNESS;

            return Mathf.Clamp(Mathf.RoundToInt(1f / percentile), MIN_MAP_BRIGHTNESS, MAX_MAP_BRIGHTNESS);
        }

        /// <summary>
        /// Returns the value at a fraction of the way through the sorted distribution.
        /// </summary>
        /// <remarks>
        /// Histogram based rather than a sort, because a full map holds four million samples and the
        /// caller only needs a percentile to two decimal places.
        /// </remarks>
        /// <param name="values">The samples, each expected in the range 0 to 1.</param>
        /// <param name="fraction">Where in the distribution to read, from 0 to 1.</param>
        /// <returns>The sample value at that point in the distribution.</returns>
        public static float Percentile(float[] values, float fraction)
        {
            if (values == null || values.Length == 0)
                return 0f;

            const int BUCKET_COUNT = 1024;
            var histogram = new int[BUCKET_COUNT];
            foreach (float value in values)
            {
                int bucket = Mathf.Clamp((int)(Mathf.Clamp01(value) * (BUCKET_COUNT - 1)), 0, BUCKET_COUNT - 1);
                histogram[bucket]++;
            }

            var target = (int)(values.Length * Mathf.Clamp01(fraction));
            var running = 0;
            for (var bucket = 0; bucket < BUCKET_COUNT; bucket++)
            {
                running += histogram[bucket];
                if (running > target)
                    return bucket / (float)(BUCKET_COUNT - 1);
            }

            return 1f;
        }
    }
}
