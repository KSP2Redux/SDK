#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Heat-reject flux per area unit from <c>Data_ActiveRadiator</c>.</summary>
    internal sealed class ActiveRadiatorExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ActiveRadiatorDataObjectMirror rad = ModuleResolver.FindModuleData<ActiveRadiatorDataObjectMirror>(part);
            if (rad == null)
            {
                yield break;
            }
            yield return (StockFieldNames.RadiatorFluxPerAreaUnit, rad.ProceduralRadiatorFluxPerAreaUnit);
        }
    }
}
#endif
