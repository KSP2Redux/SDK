#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Docking port acquire / capture range and acquire force from Data_DockingNode.</summary>
    internal sealed class DockingNodeExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            DockingNodeDataObjectMirror d = ModuleResolver.FindModuleData<DockingNodeDataObjectMirror>(part);
            if (d == null)
            {
                yield break;
            }
            yield return (StockFieldNames.DockingAcquireRange, d.AcquireRange);
            yield return (StockFieldNames.DockingAcquireForce, d.AcquireForce);
            yield return (StockFieldNames.DockingCaptureRange, d.CaptureRange);
        }
    }
}
#endif
