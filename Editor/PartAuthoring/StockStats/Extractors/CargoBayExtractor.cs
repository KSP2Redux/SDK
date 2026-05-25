#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Bay dimensions from <c>Data_CargoBay</c>.</summary>
    internal sealed class CargoBayExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            CargoBayDataObjectMirror bay = ModuleResolver.FindModuleData<CargoBayDataObjectMirror>(part);
            if (bay == null)
            {
                yield break;
            }
            yield return (StockFieldNames.CargoBayInternalLength, bay.BayInternalLength);
            yield return (StockFieldNames.CargoBayLookUpRadius, bay.lookUpRadius);
        }
    }
}
#endif
