using System;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Identifies one stats bucket by part family and size category.</summary>
    /// <remarks>
    /// Used as the lookup key when the reference window resolves which bucket the active part
    /// belongs to. Both fields are taken from the part's PartData verbatim. Null inputs are
    /// normalised to empty strings so the equality contract is total.
    /// </remarks>
    public readonly struct PartBucketKey : IEquatable<PartBucketKey>
    {
        /// <summary>
        /// Creates a new bucket key from a family and size category.
        /// </summary>
        /// <param name="family">The PartData.family value.</param>
        /// <param name="sizeCategory">The PartData.sizeKey value.</param>
        public PartBucketKey(string family, string sizeCategory)
        {
            Family = family ?? string.Empty;
            SizeCategory = sizeCategory ?? string.Empty;
        }

        /// <summary>The PartData.family value, e.g. "0100-Methalox".</summary>
        public string Family { get; }

        /// <summary>The PartData.sizeKey value, e.g. "SM".</summary>
        public string SizeCategory { get; }

        /// <inheritdoc />
        public bool Equals(PartBucketKey other) =>
            Family == other.Family && SizeCategory == other.SizeCategory;

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is PartBucketKey other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() =>
            HashCode.Combine(Family, SizeCategory);

        /// <inheritdoc />
        public override string ToString() => $"{Family} / {SizeCategory}";
    }
}
