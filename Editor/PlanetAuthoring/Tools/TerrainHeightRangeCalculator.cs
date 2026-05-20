using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Two-phase sweep that finds the lowest and highest terrain elevations on the body's PQS to
    /// within ~1 m for any feature wider than ~1 km on the equator.
    /// </summary>
    /// <remarks>
    /// Phase 1 is a uniform lat/lon grid spaced at the angular equivalent of <see cref="CoarseStepMeters" />
    /// arc-length on the equator, so smaller bodies sample fewer points and larger bodies sample
    /// more to maintain the same physical spacing. Phase 2 takes the top <see cref="Candidates" />
    /// max samples and bottom <see cref="Candidates" /> min samples and recursively halves a 3x3
    /// mini-grid centered on each until the cell's arc length drops below <see cref="PrecisionMeters" />.
    /// Drives a cancelable progress bar so the artist can abort without killing the editor.
    /// </remarks>
    internal static class TerrainHeightRangeCalculator
    {
        /// <summary>Phase-1 grid spacing as a linear arc length on the body's equator, in meters.</summary>
        private const double CoarseStepMeters = 1000.0;

        /// <summary>Number of seed cells refined per extreme. Same value used for max and min seeds.</summary>
        private const int Candidates = 100;

        /// <summary>Refinement stop threshold. Cell is refined until arc length is below this many meters.</summary>
        private const double PrecisionMeters = 1.0;

        /// <summary>How often to push a progress-bar update (and check for cancellation) during Phase 1.</summary>
        private const int ProgressUpdateInterval = 50_000;

        public readonly struct Result
        {
            public readonly bool Success;
            public readonly bool Cancelled;
            public readonly double MinHeight;
            public readonly double MaxHeight;
            public readonly int SampleCount;
            public readonly string FailureReason;

            private Result(bool success, bool cancelled, double min, double max, int samples, string reason)
            {
                Success = success;
                Cancelled = cancelled;
                MinHeight = min;
                MaxHeight = max;
                SampleCount = samples;
                FailureReason = reason;
            }

            public static Result Ok(double min, double max, int samples) => new(true, false, min, max, samples, null);
            public static Result Cancel(int samples) => new(false, true, 0, 0, samples, null);
            public static Result Fail(string reason) => new(false, false, 0, 0, 0, reason);
        }

        /// <summary>
        /// Sweeps the heightmap and returns min/max terrain height in meters relative to sea level.
        /// Caller is responsible for writing the result back to <see cref="CoreCelestialBodyData" />.
        /// </summary>
        public static Result Compute(PQS pqs)
        {
            if (pqs == null)
            {
                return Result.Fail("PQS is missing on this body.");
            }
            if (pqs.PQSRenderer == null
                || pqs.PQSRenderer.PqsDecalController == null
                || pqs.PQSRenderer.PqsDecalController.PqsDecalData == null)
            {
                return Result.Fail("PQS isn't bootstrapped. Enable a planet preview first so the renderer can sample heights.");
            }

            double radius = pqs.CoreCelestialBodyData?.Data?.radius ?? 0.0;
            if (radius <= 0.0)
            {
                return Result.Fail("Body radius is zero or missing.");
            }

            int samples = 0;
            try
            {
                double coarseStepDeg = CoarseStepMeters / radius * 180.0 / Math.PI;
                if (!CoarseSweep(pqs, coarseStepDeg, ref samples,
                    out var maxSeeds, out var minSeeds, out double rawMinR, out double rawMaxR))
                {
                    return Result.Cancel(samples);
                }

                double minR = rawMinR;
                double maxR = rawMaxR;
                foreach (var seed in maxSeeds)
                {
                    double r = RefineCandidate(pqs, seed.Lat, seed.Lon, coarseStepDeg, radius, refineUp: true, ref samples);
                    if (r > maxR) maxR = r;
                }
                foreach (var seed in minSeeds)
                {
                    double r = RefineCandidate(pqs, seed.Lat, seed.Lon, coarseStepDeg, radius, refineUp: false, ref samples);
                    if (r < minR) minR = r;
                }

                if (double.IsInfinity(minR) || double.IsInfinity(maxR))
                {
                    return Result.Fail("PQS returned no valid samples.");
                }
                return Result.Ok(minR - radius, maxR - radius, samples);
            }
            catch (ObjectDisposedException)
            {
                return Result.Fail("PQS native buffers were disposed during the sweep. Try again after the editor stabilizes.");
            }
        }

        // ---- Phase 1 -----------------------------------------------------------

        private readonly struct Candidate
        {
            public readonly double Lat;
            public readonly double Lon;
            public readonly double SampleR;
            public Candidate(double lat, double lon, double r) { Lat = lat; Lon = lon; SampleR = r; }
        }

        private sealed class CandidateComparer : IComparer<Candidate>
        {
            private readonly bool _ascending;
            public CandidateComparer(bool ascending) { _ascending = ascending; }
            public int Compare(Candidate a, Candidate b)
            {
                int c = _ascending ? a.SampleR.CompareTo(b.SampleR) : b.SampleR.CompareTo(a.SampleR);
                if (c != 0) return c;
                // Tiebreak so SortedSet doesn't reject distinct cells with identical sample values.
                c = a.Lat.CompareTo(b.Lat);
                if (c != 0) return c;
                return a.Lon.CompareTo(b.Lon);
            }
        }

        private static bool CoarseSweep(
            PQS pqs,
            double stepDeg,
            ref int sampleCount,
            out List<Candidate> maxSeeds,
            out List<Candidate> minSeeds,
            out double rawMinR,
            out double rawMaxR)
        {
            // Ascending comparer for the max-tracker (so .Min is the worst kept value, easy to evict).
            var maxSet = new SortedSet<Candidate>(new CandidateComparer(ascending: true));
            // Descending for the min-tracker (so .Min is the worst kept value = largest).
            var minSet = new SortedSet<Candidate>(new CandidateComparer(ascending: false));

            rawMinR = double.PositiveInfinity;
            rawMaxR = double.NegativeInfinity;

            int latSteps = Math.Max(2, (int)Math.Ceiling(180.0 / stepDeg));
            int lonSteps = Math.Max(2, (int)Math.Ceiling(360.0 / stepDeg));
            long totalCells = (long)(latSteps + 1) * lonSteps;
            int sinceProgress = 0;

            for (int li = 0; li <= latSteps; li++)
            {
                double lat = -90.0 + li * (180.0 / latSteps);
                for (int oi = 0; oi < lonSteps; oi++)
                {
                    double lon = -180.0 + oi * (360.0 / lonSteps);
                    Vector3 dir = LatLon.GetRelSurfaceNVector(lat, lon);
                    double r = pqs.GetSurfaceHeight(dir, includeDecals: false);
                    sampleCount++;
                    sinceProgress++;
                    if (r < rawMinR) rawMinR = r;
                    if (r > rawMaxR) rawMaxR = r;

                    var cand = new Candidate(lat, lon, r);
                    InsertBounded(maxSet, cand, keepLargest: true);
                    InsertBounded(minSet, cand, keepLargest: false);

                    if (sinceProgress >= ProgressUpdateInterval)
                    {
                        sinceProgress = 0;
                        float progress = (float)((double)sampleCount / totalCells);
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Recalculating terrain range",
                            $"Coarse sweep: {sampleCount:N0} of ~{totalCells:N0} samples",
                            Mathf.Clamp01(progress)))
                        {
                            maxSeeds = new List<Candidate>(maxSet);
                            minSeeds = new List<Candidate>(minSet);
                            return false;
                        }
                    }
                }
            }

            maxSeeds = new List<Candidate>(maxSet);
            minSeeds = new List<Candidate>(minSet);
            return true;
        }

        private static void InsertBounded(SortedSet<Candidate> set, Candidate c, bool keepLargest)
        {
            if (set.Count < Candidates)
            {
                set.Add(c);
                return;
            }
            // Min of the set is always the worst-kept value (smallest for max-tracker, largest for min-tracker).
            var worst = set.Min;
            bool beats = keepLargest ? c.SampleR > worst.SampleR : c.SampleR < worst.SampleR;
            if (!beats) return;
            set.Remove(worst);
            set.Add(c);
        }

        // ---- Phase 2 -----------------------------------------------------------

        private static double RefineCandidate(
            PQS pqs,
            double startLat,
            double startLon,
            double startStepDeg,
            double radius,
            bool refineUp,
            ref int sampleCount)
        {
            double lat = startLat;
            double lon = startLon;
            double stepDeg = startStepDeg;
            // Seed best with a fresh sample at the candidate so we always return a valid value
            // even on the very first iteration.
            double bestR = SampleAt(pqs, lat, lon, ref sampleCount);

            // Refine until the cell's arc length on the equator drops below the precision target.
            while (stepDeg * Math.PI / 180.0 * radius > PrecisionMeters)
            {
                double half = stepDeg * 0.5;
                double bestLat = lat;
                double bestLon = lon;
                double bestLocal = bestR;

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        double sLat = ClampLat(lat + dy * half);
                        double sLon = WrapLon(lon + dx * half);
                        double r = SampleAt(pqs, sLat, sLon, ref sampleCount);
                        if (refineUp ? r > bestLocal : r < bestLocal)
                        {
                            bestLocal = r;
                            bestLat = sLat;
                            bestLon = sLon;
                        }
                    }
                }

                lat = bestLat;
                lon = bestLon;
                bestR = bestLocal;
                stepDeg = half;
            }

            return bestR;
        }

        private static double SampleAt(PQS pqs, double lat, double lon, ref int sampleCount)
        {
            sampleCount++;
            return pqs.GetSurfaceHeight(LatLon.GetRelSurfaceNVector(lat, lon), includeDecals: false);
        }

        private static double ClampLat(double lat)
        {
            if (lat > 90.0) return 90.0;
            if (lat < -90.0) return -90.0;
            return lat;
        }

        private static double WrapLon(double lon)
        {
            // Normalize into [-180, 180). Refinement stays within a tiny neighborhood so this rarely
            // fires, but covers the case where a candidate sits near the dateline.
            while (lon >= 180.0) lon -= 360.0;
            while (lon < -180.0) lon += 360.0;
            return lon;
        }
    }
}
