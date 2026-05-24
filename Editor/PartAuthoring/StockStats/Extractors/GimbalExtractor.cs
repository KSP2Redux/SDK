#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Gimbal range and response speed from Data_Gimbal. Engine-side fields.</summary>
    internal sealed class GimbalExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            GimbalDataObjectMirror gimbal = ModuleResolver.FindModuleData<GimbalDataObjectMirror>(part);
            if (gimbal == null)
            {
                yield break;
            }
            yield return (StockFieldNames.EngineGimbalRange, gimbal.gimbalRange);
            yield return (StockFieldNames.EngineGimbalResponseSpeed, gimbal.gimbalResponseSpeed);
        }
    }
}
#endif
