#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>
    /// Per-experiment property fields from <c>Data_ScienceExperiment.Experiments[]</c>.
    /// </summary>
    /// <remarks>
    /// Emits three fields per experiment seen on the part:
    /// <list type="bullet">
    /// <item><c>science.experiment.{id}.timeToComplete</c> - seconds to run the experiment.</item>
    /// <item><c>science.experiment.{id}.crewRequired</c> - crew count needed.</item>
    /// <item><c>science.experiment.{id}.ecRate</c> - ElectricCharge rate from ResourcesCost (0 if no EC cost).</item>
    /// </list>
    /// Field <c>Count</c> doubles as a presence-of-experiment signal for archetype seeding:
    /// experiments with no stock parts in the bucket have <c>Count = 0</c> for all three fields.
    /// </remarks>
    internal sealed class ScienceExperimentExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ScienceExperimentDataObjectMirror sci = ModuleResolver.FindModuleData<ScienceExperimentDataObjectMirror>(part);
            if (sci?.Experiments == null)
            {
                yield break;
            }
            foreach (ExperimentConfigurationMirror exp in sci.Experiments)
            {
                if (exp == null || string.IsNullOrEmpty(exp.ExperimentDefinitionID))
                {
                    continue;
                }
                string prefix = $"{StockFieldNames.ScienceExperiment}.{exp.ExperimentDefinitionID}";
                yield return ($"{prefix}.timeToComplete", exp.TimeToComplete);
                yield return ($"{prefix}.crewRequired", exp.CrewRequired);

                float ecRate = 0f;
                if (exp.ResourcesCost != null)
                {
                    foreach (PartModuleResourceSettingMirror cost in exp.ResourcesCost)
                    {
                        if (cost != null && cost.ResourceName == "ElectricCharge")
                        {
                            ecRate = cost.Rate;
                            break;
                        }
                    }
                }
                yield return ($"{prefix}.ecRate", ecRate);
            }
        }
    }
}
#endif
