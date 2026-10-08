#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Parachute deployment, drag area, pressure threshold, and chute max temp from Data_Parachute.</summary>
    internal sealed class ParachuteExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ParachuteDataObjectMirror p = ModuleResolver.FindModuleData<ParachuteDataObjectMirror>(part);
            if (p == null)
            {
                yield break;
            }
            yield return (StockFieldNames.ParachuteDeployAltitude, p.defaultDeployAltitude);
            yield return (StockFieldNames.ParachuteMinPressureToOpen, p.defaultMinAirPressureToOpen);
            yield return (StockFieldNames.ParachuteAreaDeployed, (float)p.areaDeployed);
            yield return (StockFieldNames.ParachuteMaxTemp, (float)p.chuteMaxTemp);
        }
    }
}
#endif
