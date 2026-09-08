using System;
using AwesomeTechnologies.Utility;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// The candidate-count arithmetic the spawner performs, as pure functions.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>VegetationCellSpawner.SpawnVegetationCellItem</c>, so an inspector can show what a
    /// Grid Spacing value costs before an author commits to it and have that readout agree with the
    /// spawner exactly.
    ///
    /// Covers the arithmetic. Which candidates survive is decided by the GPU placement rules.
    /// </remarks>
    public static class ScatterCountMath
    {
        /// <summary>
        /// Density factor below which the spawner skips a cell entirely.
        /// </summary>
        public const float PolarCutoff = 0.2f;

        /// <summary>
        /// Returns the latitude-driven density factor for a cell.
        /// </summary>
        /// <remarks>
        /// <c>sqrt(cos(latitude))</c>. The grid tightens in one axis only, so the square root is what
        /// holds instances per unit area constant as meridians converge.
        /// </remarks>
        /// <param name="cellCentreLatitudeDegrees">Latitude of the cell's centre.</param>
        /// <returns>The density factor, or zero past the poles where the cosine goes negative.</returns>
        public static float PolarDensityFactor(float cellCentreLatitudeDegrees)
        {
            float cosine = Mathf.Cos(Mathf.PI * cellCentreLatitudeDegrees / 180f);
            return cosine <= 0f ? 0f : Mathf.Sqrt(cosine);
        }

        /// <summary>
        /// Reports whether a cell at this latitude is skipped outright.
        /// </summary>
        /// <remarks>
        /// The skipped band is narrow, the last couple of degrees at each pole, but it is silent:
        /// an item whose only permitted region falls inside it spawns nothing and reports nothing.
        /// </remarks>
        /// <param name="cellCentreLatitudeDegrees">Latitude of the cell's centre.</param>
        /// <returns>True if the spawner refuses to spawn at this latitude, false otherwise.</returns>
        public static bool IsBeyondPolarCutoff(float cellCentreLatitudeDegrees) =>
            PolarDensityFactor(cellCentreLatitudeDegrees) < PolarCutoff;

        /// <summary>
        /// Returns the grid spacing actually used, after the global per-type density multiplier.
        /// </summary>
        /// <remarks>
        /// The spawner divides the authored Grid Spacing by the global multiplier for the item's
        /// vegetation type, so a quality setting that halves density doubles the spacing and quarters
        /// the candidates.
        /// </remarks>
        /// <param name="authoredSampleDistanceMeters">The item's Grid Spacing.</param>
        /// <param name="globalDensity">Global density multiplier for the item's vegetation type.</param>
        /// <returns>Effective spacing in metres, or zero when the multiplier is zero.</returns>
        public static float EffectiveSpacingMeters(float authoredSampleDistanceMeters, float globalDensity)
        {
            if (globalDensity <= 0f)
                return 0f;

            return authoredSampleDistanceMeters / globalDensity;
        }

        /// <summary>
        /// Returns the candidate count for one cell.
        /// </summary>
        /// <remarks>
        /// Counts candidates. Every placement rule runs afterwards and rejects some fraction, so this
        /// is the ceiling on the instances a cell can hold.
        ///
        /// The grid samples both edges of the cell, hence the two <c>+1</c>s, so a cell exactly one
        /// spacing across yields four candidates.
        /// </remarks>
        /// <param name="cellSizeDegrees">Cell extent in degrees, which is square in this system.</param>
        /// <param name="authoredSampleDistanceMeters">The item's Grid Spacing.</param>
        /// <param name="globalDensity">Global density multiplier for the item's vegetation type.</param>
        /// <param name="cellCentreLatitudeDegrees">Latitude of the cell's centre.</param>
        /// <param name="sphereRadiusMeters">The body's polar sphere radius.</param>
        /// <returns>The candidate count, or zero when the cell is skipped.</returns>
        public static long CandidateCount(
            float cellSizeDegrees,
            float authoredSampleDistanceMeters,
            float globalDensity,
            float cellCentreLatitudeDegrees,
            float sphereRadiusMeters)
        {
            float densityFactor = PolarDensityFactor(cellCentreLatitudeDegrees);
            if (densityFactor < PolarCutoff || sphereRadiusMeters <= 0f)
                return 0L;

            float spacingMeters = EffectiveSpacingMeters(authoredSampleDistanceMeters, globalDensity);
            if (spacingMeters <= 0f)
                return 0L;

            float spacingDegrees = CoordinateUtility.MetersToDegrees(spacingMeters, sphereRadiusMeters) / densityFactor;
            if (spacingDegrees <= 0f)
                return 0L;

            // Square, so one axis is the whole story. A long rather than an int because a mistyped Grid
            // Spacing reaches billions of candidates per cell, and an int product wraps negative there,
            // which would read as "well under budget" to anything checking for a cell that is too
            // expensive.
            long samplesPerAxis = Mathf.CeilToInt(cellSizeDegrees / spacingDegrees) + 1L;
            return samplesPerAxis * samplesPerAxis;
        }

        /// <summary>
        /// Reports whether a minimum and maximum pair describes a window nothing can satisfy.
        /// </summary>
        /// <remarks>
        /// An inverted window rejects every candidate in the spawn path, so the item vanishes with no
        /// diagnostic.
        /// </remarks>
        /// <param name="minimum">Lower bound.</param>
        /// <param name="maximum">Upper bound.</param>
        /// <returns>True if no value can fall inside the window, false otherwise.</returns>
        public static bool IsImpossibleWindow(float minimum, float maximum) => minimum > maximum;

        /// <summary>
        /// Reports whether two windows share any value.
        /// </summary>
        /// <remarks>
        /// Touching endpoints count as overlapping, since both callers compare the windows against a
        /// sampled float.
        /// </remarks>
        /// <param name="firstMinimum">Lower bound of the first window.</param>
        /// <param name="firstMaximum">Upper bound of the first window.</param>
        /// <param name="secondMinimum">Lower bound of the second window.</param>
        /// <param name="secondMaximum">Upper bound of the second window.</param>
        /// <returns>True if the two windows intersect, false otherwise.</returns>
        public static bool WindowsOverlap(
            float firstMinimum,
            float firstMaximum,
            float secondMinimum,
            float secondMaximum) =>
            firstMinimum <= secondMaximum && secondMinimum <= firstMaximum;
    }
}
