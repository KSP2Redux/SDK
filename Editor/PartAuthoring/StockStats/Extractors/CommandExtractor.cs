#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>EC consumption rate from <c>Data_Command.requiredResources[ElectricCharge].Rate</c>.</summary>
    /// <remarks>
    /// Only ElectricCharge is tracked. Probe cores and crewed pods both populate
    /// <c>requiredResources</c> with EC; emitting a single canonical field lets the wizard seed
    /// either archetype from the same bucket data.
    /// </remarks>
    internal sealed class CommandExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            CommandDataObjectMirror cmd = ModuleResolver.FindModuleData<CommandDataObjectMirror>(part);
            if (cmd?.requiredResources == null)
            {
                yield break;
            }
            foreach (PartModuleResourceSettingMirror setting in cmd.requiredResources)
            {
                if (setting != null && setting.ResourceName == "ElectricCharge")
                {
                    yield return (StockFieldNames.CommandEcRate, setting.Rate);
                    yield break;
                }
            }
        }
    }
}
#endif
