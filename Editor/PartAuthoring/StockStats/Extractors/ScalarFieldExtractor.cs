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
        public delegate float ScalarSelector(PartDataMirror data);

        private readonly string _name;
        private readonly ScalarSelector _selector;

        public ScalarFieldExtractor(string name, ScalarSelector selector)
        {
            _name = name;
            _selector = selector;
        }

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
