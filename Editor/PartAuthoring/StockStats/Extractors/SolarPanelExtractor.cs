#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Solar panel peak output: Rate × EfficiencyMultiplier from Data_SolarPanel. EC/s.</summary>
    internal sealed class SolarPanelExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            SolarPanelDataObjectMirror sol = ModuleResolver.FindModuleData<SolarPanelDataObjectMirror>(part);
            if (sol?.ResourceSettings == null)
            {
                yield break;
            }
            float rate = sol.ResourceSettings.Rate;
            float eff = sol.EfficiencyMultiplier > 0f ? sol.EfficiencyMultiplier : 1f;
            float peak = rate * eff;
            if (peak <= 0f)
            {
                yield break;
            }
            yield return (StockFieldNames.SolarPeakOutput, peak);
        }
    }
}
#endif
