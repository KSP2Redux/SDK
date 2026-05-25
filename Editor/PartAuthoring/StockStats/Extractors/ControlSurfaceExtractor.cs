#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Control surface deflection range (deg), area (m²), and actuator speed from Data_ControlSurface.</summary>
    internal sealed class ControlSurfaceExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ControlSurfaceDataObjectMirror cs = ModuleResolver.FindModuleData<ControlSurfaceDataObjectMirror>(part);
            if (cs == null)
            {
                yield break;
            }
            yield return (StockFieldNames.ControlSurfaceRange, cs.CtrlSurfaceRange);
            yield return (StockFieldNames.ControlSurfaceArea, cs.CtrlSurfaceArea);
            yield return (StockFieldNames.ControlSurfaceActuatorSpeed, cs.ActuatorSpeedNormalScale);
        }
    }
}
#endif
