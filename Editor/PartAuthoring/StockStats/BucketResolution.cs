using System.Collections.Generic;
using KSP.OAB;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>
    /// Outcome of a <see cref="StockStatsLookup.ResolveBucket" /> query, carrying enough
    /// context to render a "reference parts in this bucket" panel with in-bucket,
    /// adjacent-size, and closest-family-fallback rows.
    /// </summary>
    public sealed class BucketResolution
    {
        /// <summary>Family that was requested.</summary>
        public string Family { get; }

        /// <summary>Size that was requested.</summary>
        public MetaAssemblySizeFilterType Size { get; }

        /// <summary>Exact (family, size) bucket. Null when no stock part has that pair.</summary>
        public StockBucket InBucket { get; }

        /// <summary>Same-family buckets one step smaller and one step larger in natural-size order. Nulls excluded.</summary>
        public IReadOnlyList<StockBucket> Adjacent { get; }

        /// <summary>
        /// Same-family buckets sorted by absolute size distance from <see cref="Size" />, used when
        /// <see cref="InBucket" /> is null and the family has any other size buckets. Empty otherwise.
        /// </summary>
        public IReadOnlyList<StockBucket> FamilyFallback { get; }

        /// <summary>
        /// Creates a new bucket resolution result.
        /// </summary>
        /// <param name="family">Family that was requested.</param>
        /// <param name="size">Size that was requested.</param>
        /// <param name="inBucket">The exact (family, size) bucket, or null when none exists.</param>
        /// <param name="adjacent">Same-family buckets one step smaller and one step larger.</param>
        /// <param name="familyFallback">Same-family buckets sorted by size distance, for fallback display.</param>
        public BucketResolution(
            string family,
            MetaAssemblySizeFilterType size,
            StockBucket inBucket,
            IReadOnlyList<StockBucket> adjacent,
            IReadOnlyList<StockBucket> familyFallback)
        {
            Family = family;
            Size = size;
            InBucket = inBucket;
            Adjacent = adjacent;
            FamilyFallback = familyFallback;
        }
    }
}
