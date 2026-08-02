using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Pure checks over a scatter item's procedural texture rules.
    /// </summary>
    /// <remarks>
    /// Every fault here is silent. An item whose rules contradict each other spawns nothing at all,
    /// and looks exactly like an item whose camera is out of range or whose package is unassigned.
    ///
    /// Kept free of Unity objects so the headless EditMode suite can reach it.
    /// </remarks>
    public static class ScatterRuleAnalysis
    {
        /// <summary>
        /// One stratum an item both requires and forbids.
        /// </summary>
        public readonly struct Contradiction
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Contradiction" /> struct.
            /// </summary>
            /// <param name="sliceIndex">The stratum both rules name.</param>
            /// <param name="minimum">Lower bound of the overlapping weight window.</param>
            /// <param name="maximum">Upper bound of the overlapping weight window.</param>
            public Contradiction(int sliceIndex, float minimum, float maximum)
            {
                SliceIndex = sliceIndex;
                Minimum = minimum;
                Maximum = maximum;
            }

            /// <summary>
            /// The stratum both rules name.
            /// </summary>
            public int SliceIndex { get; }

            /// <summary>
            /// Lower bound of the weight range both rules cover.
            /// </summary>
            public float Minimum { get; }

            /// <summary>
            /// Upper bound of the weight range both rules cover.
            /// </summary>
            public float Maximum { get; }
        }


        /// <summary>
        /// Finds strata an item both includes and excludes over an overlapping weight window.
        /// </summary>
        /// <remarks>
        /// Exclusion wins, so an overlap means the include rule can never be satisfied on that
        /// stratum. When it is the item's only include rule, the item spawns nowhere.
        ///
        /// Rules whose own window is inverted are skipped.
        ///
        /// One entry per stratum. Where several pairs collide on one stratum the reported window
        /// spans them all.
        /// </remarks>
        /// <param name="includeRules">The item's include rules. May be null.</param>
        /// <param name="excludeRules">The item's exclude rules. May be null.</param>
        /// <returns>One entry per contradicting stratum.</returns>
        public static IReadOnlyList<Contradiction> FindContradictions(
            IReadOnlyList<TerrainTextureRule> includeRules,
            IReadOnlyList<TerrainTextureRule> excludeRules)
        {
            var found = new List<Contradiction>();
            if (includeRules == null || excludeRules == null)
                return found;

            var spanByStratum = new Dictionary<int, (float Minimum, float Maximum)>();
            foreach (TerrainTextureRule include in includeRules)
            {
                if (include == null || ScatterCountMath.IsImpossibleWindow(include.MinimumValue, include.MaximumValue))
                    continue;

                foreach (TerrainTextureRule exclude in excludeRules)
                {
                    if (exclude == null || exclude.TextureIndex != include.TextureIndex ||
                        ScatterCountMath.IsImpossibleWindow(exclude.MinimumValue, exclude.MaximumValue))
                        continue;

                    if (!ScatterCountMath.WindowsOverlap(
                            include.MinimumValue, include.MaximumValue,
                            exclude.MinimumValue, exclude.MaximumValue))
                        continue;

                    float minimum = Mathf.Max(include.MinimumValue, exclude.MinimumValue);
                    float maximum = Mathf.Min(include.MaximumValue, exclude.MaximumValue);
                    if (spanByStratum.TryGetValue(include.TextureIndex, out (float Minimum, float Maximum) span))
                    {
                        minimum = Mathf.Min(minimum, span.Minimum);
                        maximum = Mathf.Max(maximum, span.Maximum);
                    }

                    spanByStratum[include.TextureIndex] = (minimum, maximum);
                }
            }

            foreach (KeyValuePair<int, (float Minimum, float Maximum)> entry in spanByStratum)
            {
                found.Add(new Contradiction(entry.Key, entry.Value.Minimum, entry.Value.Maximum));
            }

            return found;
        }
    }
}
