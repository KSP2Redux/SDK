#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Extractor for direct scalar fields on <see cref="PartDataMirror" />.</summary>
    /// <remarks>
    /// Used for the seven PartData scalars (mass, cost, crashTolerance, breakingForce,
    /// breakingTorque, explosionPotential, maxTemp). Yields exactly one entry per part.
    /// </remarks>
    internal sealed class ScalarFieldExtractor : IStockFieldExtractor
    {
        /// <summary>Reads a single scalar from a part's <see cref="PartDataMirror" />.</summary>
        /// <param name="data">Part data to read.</param>
        /// <returns>The selected scalar value.</returns>
        public delegate float ScalarSelector(PartDataMirror data);

        private readonly string _name;
        private readonly ScalarSelector _selector;

        /// <summary>
        /// Creates a new scalar extractor bound to the given field name and selector.
        /// </summary>
        /// <param name="name">Canonical field name to emit.</param>
        /// <param name="selector">Function that reads the scalar from the part data.</param>
        public ScalarFieldExtractor(string name, ScalarSelector selector)
        {
            _name = name;
            _selector = selector;
        }

        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            PartDataMirror data = part?.Data;
            if (data == null)
            {
                yield break;
            }
            yield return (_name, _selector(data));
        }
    }
}
#endif
