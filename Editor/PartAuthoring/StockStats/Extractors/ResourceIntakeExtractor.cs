#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Intake area and intake speed from <c>Data_ResourceIntake</c>.</summary>
    internal sealed class ResourceIntakeExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ResourceIntakeDataObjectMirror intake = ModuleResolver.FindModuleData<ResourceIntakeDataObjectMirror>(part);
            if (intake == null)
            {
                yield break;
            }
            yield return (StockFieldNames.IntakeArea, intake.area);
            yield return (StockFieldNames.IntakeSpeed, (float)intake.intakeSpeed);
        }
    }
}
#endif
