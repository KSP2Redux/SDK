#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Resource converter's top-level conversion rate from Data_ResourceConverter.</summary>
    /// <remarks>
    /// Per-formula input/output breakdowns are out of scope for V1 because formulas are
    /// structurally a list of <c>ResourceConverterFormulaDefinition</c> records with input /
    /// output resource arrays. Future passes can extend with <c>converter.input.{formula}.{resource}</c>
    /// keyed entries.
    /// </remarks>
    internal sealed class ConverterExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ConverterDataObjectMirror c = ModuleResolver.FindModuleData<ConverterDataObjectMirror>(part);
            if (c?.conversionRate == null)
            {
                yield break;
            }
            yield return (StockFieldNames.ConverterConversionRate, c.conversionRate.storedValue);
        }
    }
}
#endif
