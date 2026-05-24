#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Ablator characteristics from <c>Data_Heatshield</c>.</summary>
    internal sealed class HeatshieldExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            HeatshieldDataObjectMirror shield = ModuleResolver.FindModuleData<HeatshieldDataObjectMirror>(part);
            if (shield == null)
            {
                yield break;
            }
            yield return (StockFieldNames.HeatshieldAblationTempThreshold, (float)shield.AblationTempThreshold);
            yield return (StockFieldNames.HeatshieldAblationMaxOverThreshold, (float)shield.AblationMaximumOverThreshold);
            yield return (StockFieldNames.HeatshieldPyrolysisLossFactor, (float)shield.PyrolysisLossFactor);
            yield return (StockFieldNames.HeatshieldShieldingScale, shield.ShieldingScale);
        }
    }
}
#endif
