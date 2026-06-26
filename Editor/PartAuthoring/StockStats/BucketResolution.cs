using System.Collections.Generic;
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

        /// <summary>Size key that was requested.</summary>
        public string SizeKey { get; }

        /// <summary>Exact (family, size) bucket. Null when no stock part has that pair.</summary>
        public StockBucket InBucket { get; }

        /// <summary>
        /// Synthetic same-family bucket interpolated between the nearest lower and upper stock-size buckets.
        /// Null when the exact bucket exists or no bracketing buckets exist.
        /// </summary>
        public StockBucket Interpolated { get; }

        /// <summary>Lower stock-size bucket used by <see cref="Interpolated" />, if present.</summary>
        public StockBucket InterpolationLower { get; }

        /// <summary>Upper stock-size bucket used by <see cref="Interpolated" />, if present.</summary>
        public StockBucket InterpolationUpper { get; }

        /// <summary>Interpolation position, 0 at <see cref="InterpolationLower" /> and 1 at <see cref="InterpolationUpper" />.</summary>
        public float InterpolationT { get; }

        /// <summary>Same-family buckets one step smaller and one step larger in natural-size order. Nulls excluded.</summary>
        public IReadOnlyList<StockBucket> Adjacent { get; }

        /// <summary>
        /// Same-family buckets sorted by absolute size distance from <see cref="SizeKey" />, used when
        /// <see cref="InBucket" /> is null and the family has any other size buckets. Empty otherwise.
        /// </summary>
        public IReadOnlyList<StockBucket> FamilyFallback { get; }

        /// <summary>
        /// Creates a new bucket resolution result.
        /// </summary>
        /// <param name="family">Family that was requested.</param>
        /// <param name="sizeKey">Size key that was requested.</param>
        /// <param name="inBucket">The exact (family, size) bucket, or null when none exists.</param>
        /// <param name="adjacent">Same-family buckets one step smaller and one step larger.</param>
        /// <param name="familyFallback">Same-family buckets sorted by size distance, for fallback display.</param>
        /// <param name="interpolated">Synthetic interpolated bucket, or null when unavailable.</param>
        /// <param name="interpolationLower">Lower stock-size bucket used by <paramref name="interpolated" />.</param>
        /// <param name="interpolationUpper">Upper stock-size bucket used by <paramref name="interpolated" />.</param>
        /// <param name="interpolationT">Interpolation position between lower and upper buckets.</param>
        public BucketResolution(
            string family,
            string sizeKey,
            StockBucket inBucket,
            IReadOnlyList<StockBucket> adjacent,
            IReadOnlyList<StockBucket> familyFallback,
            StockBucket interpolated = null,
            StockBucket interpolationLower = null,
            StockBucket interpolationUpper = null,
            float interpolationT = 0f)
        {
            Family = family;
            SizeKey = sizeKey;
            InBucket = inBucket;
            Interpolated = interpolated;
            InterpolationLower = interpolationLower;
            InterpolationUpper = interpolationUpper;
            InterpolationT = interpolationT;
            Adjacent = adjacent;
            FamilyFallback = familyFallback;
        }
    }
}
