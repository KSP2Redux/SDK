using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Science
{
    /// <summary>
    /// K-means RGB color clustering driven from the Import &amp; cluster colors flow.
    /// </summary>
    /// <remarks>
    /// Includes a 3x3 mode-snap pre-pass (collapses anti-aliased edges between solid regions onto
    /// the dominant neighborhood color) and an optional bit-depth posterize pre-pass.
    /// Algorithm: k-means++ initialization on a random pixel sample, then Lloyd iterations until
    /// centroids stop moving meaningfully (or <c>MaxIterations</c> hits). Distance is sum-of-squares
    /// over RGB only. Alpha is ignored. Returns one entry per cluster sorted by descending pixel
    /// coverage so the largest clusters become the first region rows.
    /// Iterations run on a random subset of pixels (capped at <see cref="IterationSampleCap" />)
    /// rather than the full source. With 4K maps this drops a one-minute run to a few seconds with
    /// no visible quality loss. The final assignment still walks every pixel for the coverage stats.
    /// </remarks>
    internal static class KMeansColorClustering
    {
        /// <summary>
        /// Maximum number of Lloyd iterations before the algorithm gives up on convergence.
        /// </summary>
        public const int MaxIterations = 30;

        /// <summary>
        /// Average centroid shift below which iteration stops, expressed in [0,255] color units.
        /// </summary>
        public const float ConvergenceEpsilon = 1f;

        /// <summary>
        /// Iteration budget per Lloyd step.
        /// </summary>
        /// <remarks>
        /// 64K samples is enough to converge on stable centroids for typical region maps. The final
        /// assignment walks every pixel for accurate coverage regardless of this cap.
        /// </remarks>
        public const int IterationSampleCap = 64 * 1024;

        /// <summary>
        /// Tunable inputs to <see cref="Run" />.
        /// </summary>
        public struct Options
        {
            /// <summary>
            /// The target cluster count, clamped to [1, 64].
            /// </summary>
            public int K;

            /// <summary>
            /// True to run the 3x3 mode-snap pre-pass that collapses anti-aliased boundary pixels.
            /// </summary>
            public bool SnapAntiAliased;

            /// <summary>
            /// True to posterize the source to <see cref="PosterizeBitsPerChannel" /> bits per channel before clustering.
            /// </summary>
            public bool PosterizeFirst;

            /// <summary>
            /// Bits per channel for the posterize pre-pass, in the range [1, 8].
            /// </summary>
            /// <remarks>Only used when <see cref="PosterizeFirst" /> is true.</remarks>
            public int PosterizeBitsPerChannel;

            /// <summary>
            /// Seed for the random pixel sampler and k-means++ initialization.
            /// </summary>
            public int RandomSeed;
        }

        /// <summary>
        /// One discovered color cluster returned by <see cref="Run" />.
        /// </summary>
        public struct Cluster
        {
            /// <summary>
            /// The cluster's centroid color rounded to 8-bit RGBA.
            /// </summary>
            public Color32 Centroid;

            /// <summary>
            /// Number of source pixels assigned to this cluster.
            /// </summary>
            public int PixelCount;

            /// <summary>
            /// Fraction of source pixels assigned to this cluster, in the range [0, 1].
            /// </summary>
            public float PixelFraction;
        }

        /// <summary>
        /// Runs k-means against the source texture's pixels and returns the discovered clusters, sorted largest-first.
        /// </summary>
        /// <remarks>The source must be Read/Write-enabled.</remarks>
        /// <param name="source">The texture to cluster.</param>
        /// <param name="opts">Clustering options.</param>
        /// <param name="progress">
        /// Optional progress reporter, called as iterations advance with (fraction in 0..1, label).
        /// Used to drive a progress bar in the inspector window.
        /// </param>
        /// <returns>The discovered clusters sorted by descending pixel coverage, or an empty array on invalid input.</returns>
        public static Cluster[] Run(Texture2D source, Options opts, Action<float, string> progress = null)
        {
            if (source == null) return Array.Empty<Cluster>();
            if (!source.isReadable)
            {
                Debug.LogWarning($"[KMeansColorClustering] Source texture '{source.name}' is not Read/Write enabled.");
                return Array.Empty<Cluster>();
            }
            var k = Mathf.Clamp(opts.K, 1, 64);

            progress?.Invoke(0.05f, "Reading pixels");
            var pixels = source.GetPixels32();
            if (pixels.Length == 0) return Array.Empty<Cluster>();

            if (opts.PosterizeFirst)
            {
                progress?.Invoke(0.10f, "Posterizing");
                ApplyPosterize(pixels, Mathf.Clamp(opts.PosterizeBitsPerChannel, 1, 8));
            }
            if (opts.SnapAntiAliased)
            {
                progress?.Invoke(0.15f, "Snapping anti-aliased edges");
                ApplyModeSnap(pixels, source.width, source.height);
            }

            progress?.Invoke(0.20f, "Sampling pixel subset");
            var sample = SampleSubset(pixels, IterationSampleCap, opts.RandomSeed);

            progress?.Invoke(0.25f, "Initializing centroids");
            var centroids = InitializeCentroidsKMeansPlusPlus(sample, k, opts.RandomSeed);
            var assignments = new int[sample.Length];

            // Lloyd iterations on the sample only; reports progress per iteration.
            const float iterStart = 0.30f;
            const float iterEnd = 0.85f;
            for (var iter = 0; iter < MaxIterations; iter++)
            {
                var iterFrac = iterStart + (iterEnd - iterStart) * iter / MaxIterations;
                progress?.Invoke(iterFrac, $"Clustering (iteration {iter + 1}/{MaxIterations})");
                AssignPixelsToCentroids(sample, centroids, assignments);
                var next = RecomputeCentroids(sample, assignments, k, centroids);
                var shift = AverageCentroidShift(centroids, next);
                centroids = next;
                if (shift < ConvergenceEpsilon) break;
            }

            // Final pass over every pixel to get accurate coverage stats.
            progress?.Invoke(0.90f, "Building cluster summary");
            return BuildClusterSummary(pixels, centroids, k);
        }

        // Picks a uniformly random subset of pixels for iteration. When the source is smaller than
        // the cap the entire array is returned (no copy needed for the all-pixels case).
        private static Color32[] SampleSubset(Color32[] pixels, int cap, int seed)
        {
            if (pixels.Length <= cap) return pixels;
            var rng = new System.Random(seed);
            var sample = new Color32[cap];
            for (var i = 0; i < cap; i++)
            {
                sample[i] = pixels[rng.Next(pixels.Length)];
            }
            return sample;
        }

        private static Vector3[] InitializeCentroidsKMeansPlusPlus(Color32[] pixels, int k, int seed)
        {
            var rng = new System.Random(seed);
            var centroids = new Vector3[k];
            centroids[0] = ToVec(pixels[rng.Next(pixels.Length)]);
            var distSq = new float[pixels.Length];

            for (var c = 1; c < k; c++)
            {
                var total = 0.0;
                for (var i = 0; i < pixels.Length; i++)
                {
                    distSq[i] = NearestCentroidDistanceSquared(pixels[i], centroids, c);
                    total += distSq[i];
                }
                if (total <= 0.0)
                {
                    centroids[c] = ToVec(pixels[rng.Next(pixels.Length)]);
                    continue;
                }
                var pick = rng.NextDouble() * total;
                var running = 0.0;
                var chosen = pixels.Length - 1;
                for (var i = 0; i < pixels.Length; i++)
                {
                    running += distSq[i];
                    if (running >= pick)
                    {
                        chosen = i;
                        break;
                    }
                }
                centroids[c] = ToVec(pixels[chosen]);
            }
            return centroids;
        }

        // Inline byte arithmetic instead of allocating a Vector3 per pixel.
        private static float NearestCentroidDistanceSquared(Color32 px, Vector3[] centroids, int validCount)
        {
            var best = float.MaxValue;
            float pr = px.r, pg = px.g, pb = px.b;
            for (var i = 0; i < validCount; i++)
            {
                var c = centroids[i];
                float dr = pr - c.x, dg = pg - c.y, db = pb - c.z;
                var s = dr * dr + dg * dg + db * db;
                if (s < best) best = s;
            }
            return best;
        }

        private static void AssignPixelsToCentroids(Color32[] pixels, Vector3[] centroids, int[] assignments)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                var px = pixels[i];
                float pr = px.r, pg = px.g, pb = px.b;
                var best = 0;
                var bestSq = float.MaxValue;
                for (var c = 0; c < centroids.Length; c++)
                {
                    var ce = centroids[c];
                    float dr = pr - ce.x, dg = pg - ce.y, db = pb - ce.z;
                    var s = dr * dr + dg * dg + db * db;
                    if (s < bestSq)
                    {
                        bestSq = s;
                        best = c;
                    }
                }
                assignments[i] = best;
            }
        }

        private static Vector3[] RecomputeCentroids(Color32[] pixels, int[] assignments, int k, Vector3[] previous)
        {
            var sums = new Vector3[k];
            var counts = new int[k];
            for (var i = 0; i < pixels.Length; i++)
            {
                var a = assignments[i];
                var px = pixels[i];
                sums[a].x += px.r;
                sums[a].y += px.g;
                sums[a].z += px.b;
                counts[a]++;
            }
            var next = new Vector3[k];
            for (var c = 0; c < k; c++)
            {
                next[c] = counts[c] > 0 ? sums[c] / counts[c] : previous[c];
            }
            return next;
        }

        private static float AverageCentroidShift(Vector3[] a, Vector3[] b)
        {
            var total = 0f;
            for (var i = 0; i < a.Length; i++)
            {
                total += (a[i] - b[i]).magnitude;
            }
            return total / a.Length;
        }

        // Final pass: assign every source pixel to its nearest centroid for accurate coverage.
        private static Cluster[] BuildClusterSummary(Color32[] pixels, Vector3[] centroids, int k)
        {
            var counts = new int[k];
            for (var i = 0; i < pixels.Length; i++)
            {
                var px = pixels[i];
                float pr = px.r, pg = px.g, pb = px.b;
                var best = 0;
                var bestSq = float.MaxValue;
                for (var c = 0; c < centroids.Length; c++)
                {
                    var ce = centroids[c];
                    float dr = pr - ce.x, dg = pg - ce.y, db = pb - ce.z;
                    var s = dr * dr + dg * dg + db * db;
                    if (s < bestSq)
                    {
                        bestSq = s;
                        best = c;
                    }
                }
                counts[best]++;
            }
            var clusters = new Cluster[k];
            for (var c = 0; c < k; c++)
            {
                clusters[c] = new Cluster
                {
                    Centroid = ToColor32(centroids[c]),
                    PixelCount = counts[c],
                    PixelFraction = pixels.Length > 0 ? counts[c] / (float)pixels.Length : 0f,
                };
            }
            Array.Sort(clusters, (a, b) => b.PixelCount.CompareTo(a.PixelCount));
            return clusters;
        }

        // -------- Pre-passes --------------------------------------------------

        private static void ApplyPosterize(Color32[] pixels, int bitsPerChannel)
        {
            var levels = 1 << bitsPerChannel;
            var step = 255 / Mathf.Max(1, levels - 1);
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                p.r = (byte)(Mathf.RoundToInt(p.r / 255f * (levels - 1)) * step);
                p.g = (byte)(Mathf.RoundToInt(p.g / 255f * (levels - 1)) * step);
                p.b = (byte)(Mathf.RoundToInt(p.b / 255f * (levels - 1)) * step);
                pixels[i] = p;
            }
        }

        /// <summary>
        /// 3x3 mode filter. For each pixel, replaces it with the most frequent color in its 3x3
        /// neighborhood. Anti-aliased boundaries between two solid regions collapse to one side or
        /// the other, killing the spurious "transition" colors a naive k-means would pick up.
        /// </summary>
        private static void ApplyModeSnap(Color32[] pixels, int width, int height)
        {
            var copy = new Color32[pixels.Length];
            Array.Copy(pixels, copy, pixels.Length);

            // Reusable 9-entry scratch buffer, hoisted outside the per-pixel loop.
            var window = new Color32[9];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var count = 0;
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        var ny = y + dy;
                        if (ny < 0 || ny >= height) continue;
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            var nx = x + dx;
                            if (nx < 0 || nx >= width) continue;
                            window[count++] = copy[ny * width + nx];
                        }
                    }
                    pixels[y * width + x] = MostFrequent(window, count);
                }
            }
        }

        private static Color32 MostFrequent(Color32[] window, int count)
        {
            var best = window[0];
            var bestCount = 1;
            for (var i = 0; i < count; i++)
            {
                var matches = 0;
                for (var j = 0; j < count; j++)
                {
                    if (ColorsEqual(window[i], window[j])) matches++;
                }
                if (matches > bestCount)
                {
                    bestCount = matches;
                    best = window[i];
                }
            }
            return best;
        }

        private static bool ColorsEqual(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        private static Vector3 ToVec(Color32 c) => new(c.r, c.g, c.b);

        private static Color32 ToColor32(Vector3 v) => new(
            (byte)Mathf.Clamp(Mathf.RoundToInt(v.x), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(v.y), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(v.z), 0, 255),
            255);
    }
}
