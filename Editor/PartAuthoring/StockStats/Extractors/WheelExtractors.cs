#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Max brake torque from Data_WheelBrakes, in kN·m.</summary>
    internal sealed class WheelBrakeTorqueExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            WheelBrakesDataObjectMirror w = ModuleResolver.FindModuleData<WheelBrakesDataObjectMirror>(part);
            if (w == null || w.MaxBrakeTorque <= 0f)
            {
                yield break;
            }
            yield return (StockFieldNames.WheelMaxBrakeTorque, w.MaxBrakeTorque);
        }
    }

    /// <summary>Suspension travel distance from Data_WheelSuspension, in m.</summary>
    internal sealed class WheelSuspensionExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            WheelSuspensionDataObjectMirror w = ModuleResolver.FindModuleData<WheelSuspensionDataObjectMirror>(part);
            if (w == null || w.suspensionDistance <= 0f)
            {
                yield break;
            }
            yield return (StockFieldNames.WheelSuspensionDistance, w.suspensionDistance);
        }
    }
}
#endif
