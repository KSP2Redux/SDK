#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>One stat extractor: turns a stock part record into zero or more named scalars.</summary>
    /// <remarks>
    /// Implementations stay stateless and side-effect-free so the baker can iterate them
    /// across parts in any order. <see cref="Extract" /> yields zero entries when the part
    /// lacks the prerequisites (e.g. an engine extractor on a tank). Multi-yield supports
    /// per-sub-key fields like <c>tank.capacity.Methalox</c>, <c>engine.maxThrust.Hydrogen</c>:
    /// each container or engine mode contributes its own entry under a discriminated name,
    /// rather than being aggregated into a single number.
    /// </remarks>
    internal interface IStockFieldExtractor
    {
        /// <summary>Yields per-field <c>(Name, Value)</c> entries for the given part.</summary>
        /// <remarks>
        /// Names may be fixed (<c>"mass"</c>) or composed with sub-keys
        /// (<c>"engine.maxThrust.Methalox"</c>). Empty enumeration is valid for parts that
        /// don't have the field.
        /// </remarks>
        IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx);
    }

    /// <summary>Side-channel data the bake resolves once and passes to every extractor.</summary>
    /// <remarks>
    /// Today this is just resource mass-per-unit values, populated by the resource side-pass
    /// before parts are scanned. New cross-file lookups would join this struct rather than
    /// becoming extractor constructor parameters.
    /// </remarks>
    internal sealed class BakeContext
    {
        public BakeContext(IReadOnlyDictionary<string, float> resourceMassPerUnit)
        {
            ResourceMassPerUnit = resourceMassPerUnit ?? new Dictionary<string, float>();
        }

        /// <summary>Mass per resource unit, recipe-resolved. Missing key means "unknown resource".</summary>
        public IReadOnlyDictionary<string, float> ResourceMassPerUnit { get; }
    }
}
#endif
