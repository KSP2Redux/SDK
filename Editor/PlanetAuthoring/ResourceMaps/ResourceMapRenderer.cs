using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.ResourceMaps
{
    /// <summary>
    /// Renders a resource's density map from a biome mask and its four noise channels.
    /// </summary>
    /// <remarks>
    /// Runs on the CPU across every available core. Nothing here touches the Unity API, so it is
    /// safe to call from a worker thread for the interactive previews as well as from the main
    /// thread for a full bake.
    /// </remarks>
    public static class ResourceMapRenderer
    {
        /// <summary>
        /// Output rows rendered between two calls to the progress callback.
        /// </summary>
        /// <remarks>
        /// Chunking exists so a bake can report progress and accept cancellation without the
        /// callback being invoked from a worker thread, which a Unity progress bar would not
        /// survive. Each chunk is parallel internally, and the callback runs on the caller's thread
        /// between chunks.
        /// </remarks>
        public const int PROGRESS_CHUNK_ROWS = 64;

        /// <summary>
        /// Renders a density map.
        /// </summary>
        /// <param name="mask">The body's biome mask.</param>
        /// <param name="entry">The resource whose four channels are composited.</param>
        /// <param name="outputSize">Side length of the output, in pixels.</param>
        /// <param name="supersample">Samples per output pixel along each axis. 1 disables supersampling.</param>
        /// <param name="onProgress">Invoked on the calling thread between chunks with a fraction from 0 to 1. Return false to cancel.</param>
        /// <returns>The density map in row-major order with row 0 at the south pole, or null when cancelled.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask" /> or <paramref name="entry" /> is null.</exception>
        public static float[] Render(
            BiomeMask mask,
            ResourceMapEntry entry,
            int outputSize,
            int supersample,
            Func<float, bool> onProgress = null)
        {
            if (mask == null)
                throw new ArgumentNullException(nameof(mask));
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            int size = Mathf.Max(1, outputSize);
            int samplesPerPixel = Mathf.Max(1, supersample);
            int sampleSize = size * samplesPerPixel;
            float sampleWeight = 1f / (samplesPerPixel * samplesPerPixel);

            var densities = new float[size * size];
            FractalNoiseChannel[] channels = ResolveChannels(entry);

            for (var chunkStart = 0; chunkStart < size; chunkStart += PROGRESS_CHUNK_ROWS)
            {
                int chunkEnd = Mathf.Min(chunkStart + PROGRESS_CHUNK_ROWS, size);

                Parallel.For(chunkStart, chunkEnd, () => CreateEvaluators(channels), (row, _, evaluators) =>
                {
                    RenderRow(mask, evaluators, densities, row, size, sampleSize, samplesPerPixel, sampleWeight);
                    return evaluators;
                }, _ => { });

                if (onProgress != null && !onProgress(chunkEnd / (float)size))
                    return null;
            }

            return densities;
        }

        private static void RenderRow(
            BiomeMask mask,
            SphericalFractalNoise[] evaluators,
            float[] densities,
            int row,
            int size,
            int sampleSize,
            int samplesPerPixel,
            float sampleWeight)
        {
            int rowOffset = row * size;
            for (var column = 0; column < size; column++)
            {
                var accumulated = 0f;
                for (var subRow = 0; subRow < samplesPerPixel; subRow++)
                {
                    int sampleY = row * samplesPerPixel + subRow;
                    double v = (sampleY + 0.5) / sampleSize;
                    int maskY = sampleY * mask.Height / sampleSize;

                    for (var subColumn = 0; subColumn < samplesPerPixel; subColumn++)
                    {
                        int sampleX = column * samplesPerPixel + subColumn;
                        double u = (sampleX + 0.5) / sampleSize;
                        int maskX = sampleX * mask.Width / sampleSize;

                        accumulated += ComposeSample(mask.Sample(maskX, maskY), evaluators, u, v);
                    }
                }

                densities[rowOffset + column] = accumulated * sampleWeight;
            }
        }

        private static float ComposeSample(Vector4 coverage, SphericalFractalNoise[] evaluators, double u, double v)
        {
            // Evaluating a channel that covers nothing at this pixel would be wasted work, and on a
            // typical body most pixels sit inside one or two biomes rather than all four.
            var noise = new Vector4(
                coverage.x > ResourceMapComposer.COVERAGE_EPSILON ? evaluators[0].Evaluate(u, v) : 0f,
                coverage.y > ResourceMapComposer.COVERAGE_EPSILON ? evaluators[1].Evaluate(u, v) : 0f,
                coverage.z > ResourceMapComposer.COVERAGE_EPSILON ? evaluators[2].Evaluate(u, v) : 0f,
                coverage.w > ResourceMapComposer.COVERAGE_EPSILON ? evaluators[3].Evaluate(u, v) : 0f
            );

            return ResourceMapComposer.Compose(coverage, noise);
        }

        /// <summary>
        /// Renders one channel's noise field on its own, with no biome mask applied.
        /// </summary>
        /// <remarks>
        /// Backs the per-channel viewer, which shows what a channel's settings produce before the
        /// mask restricts it to that channel's biome.
        /// </remarks>
        /// <param name="channel">The channel to evaluate.</param>
        /// <param name="size">Side length of the output, in pixels.</param>
        /// <returns>The field in row-major order with row 0 at the south pole.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="channel" /> is null.</exception>
        public static float[] RenderChannel(FractalNoiseChannel channel, int size)
        {
            if (channel == null)
                throw new ArgumentNullException(nameof(channel));

            int side = Mathf.Max(1, size);
            var field = new float[side * side];

            Parallel.For(0, side, () => new SphericalFractalNoise(channel), (row, _, evaluator) =>
            {
                double v = (row + 0.5) / side;
                int rowOffset = row * side;
                for (var column = 0; column < side; column++)
                {
                    field[rowOffset + column] = evaluator.Evaluate((column + 0.5) / side, v);
                }
                return evaluator;
            }, _ => { });

            return field;
        }

        /// <summary>
        /// Returns an entry's four channels, substituting defaults for any that are missing.
        /// </summary>
        /// <param name="entry">The resource whose channels to resolve.</param>
        /// <returns>Exactly four channels.</returns>
        public static FractalNoiseChannel[] ResolveChannels(ResourceMapEntry entry)
        {
            FractalNoiseChannel[] stored = entry.Channels;
            if (stored != null && stored.Length == ResourceMapAuthoring.CHANNEL_COUNT)
            {
                var complete = true;
                foreach (FractalNoiseChannel channel in stored)
                {
                    if (channel == null)
                    {
                        complete = false;
                        break;
                    }
                }
                if (complete)
                    return stored;
            }

            var resolved = new FractalNoiseChannel[ResourceMapAuthoring.CHANNEL_COUNT];
            FractalNoiseChannel[] defaults = ResourceMapEntry.CreateDefaultChannels();
            for (var index = 0; index < resolved.Length; index++)
            {
                resolved[index] = stored != null && index < stored.Length && stored[index] != null
                    ? stored[index]
                    : defaults[index];
            }
            return resolved;
        }

        // One evaluator set per worker. A LibNoise module only reads its own fields during
        // evaluation, but building per worker removes the question entirely.
        private static SphericalFractalNoise[] CreateEvaluators(FractalNoiseChannel[] channels)
        {
            var evaluators = new SphericalFractalNoise[ResourceMapAuthoring.CHANNEL_COUNT];
            for (var index = 0; index < evaluators.Length; index++)
            {
                evaluators[index] = new SphericalFractalNoise(channels[index]);
            }
            return evaluators;
        }

        /// <summary>
        /// Converts a density map into the pixels a resource map PNG stores.
        /// </summary>
        /// <remarks>
        /// Written as grey with the value repeated across red, green and blue, matching the shipped
        /// maps. The runtime reads the red channel alone.
        /// </remarks>
        /// <param name="densities">The density map, each value 0 to 1.</param>
        /// <returns>Pixels in the same order as <paramref name="densities" />.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="densities" /> is null.</exception>
        public static Color32[] ToPixels(float[] densities)
        {
            if (densities == null)
                throw new ArgumentNullException(nameof(densities));

            var pixels = new Color32[densities.Length];
            for (var index = 0; index < densities.Length; index++)
            {
                var value = (byte)Mathf.RoundToInt(Mathf.Clamp01(densities[index]) * 255f);
                pixels[index] = new Color32(value, value, value, 255);
            }
            return pixels;
        }
    }
}
