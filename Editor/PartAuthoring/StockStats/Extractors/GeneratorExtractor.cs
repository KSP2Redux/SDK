#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Generator output rate keyed by produced resource: <c>generator.output.{resource}</c>.</summary>
    internal sealed class GeneratorExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            GeneratorDataObjectMirror gen = ModuleResolver.FindModuleData<GeneratorDataObjectMirror>(part);
            if (gen?.ResourceSetting == null)
            {
                yield break;
            }
            var rs = gen.ResourceSetting;
            if (string.IsNullOrEmpty(rs.ResourceName) || rs.Rate <= 0f)
            {
                yield break;
            }
            yield return ($"{StockFieldNames.GeneratorOutput}.{rs.ResourceName}", rs.Rate);
        }
    }
}
#endif
