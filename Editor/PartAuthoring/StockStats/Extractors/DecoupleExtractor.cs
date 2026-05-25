#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Stack / radial decoupler ejection force from Data_Decouple, in kN.</summary>
    internal sealed class DecoupleExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            DecoupleDataObjectMirror d = ModuleResolver.FindModuleData<DecoupleDataObjectMirror>(part);
            if (d == null)
            {
                yield break;
            }
            yield return (StockFieldNames.DecoupleEjectionForce, d.ejectionForce);
        }
    }
}
#endif
