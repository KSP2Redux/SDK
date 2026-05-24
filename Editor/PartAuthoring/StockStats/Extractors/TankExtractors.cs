#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Per-resource tank capacity, in units. Yields one entry per container.</summary>
    /// <remarks>
    /// Aggregating across containers would treat Methalox units and Hydrogen units as
    /// fungible, which is meaningless physically. Splitting by resource name keeps each
    /// container's capacity comparable against same-resource neighbours.
    /// </remarks>
    internal sealed class TankCapacityExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            var containers = part?.Data?.resourceContainers;
            if (containers == null)
            {
                yield break;
            }
            foreach (ResourceContainerMirror c in containers)
            {
                if (c == null || string.IsNullOrEmpty(c.name))
                {
                    continue;
                }
                if (c.capacityUnits <= 0f)
                {
                    continue;
                }
                yield return ($"{StockFieldNames.TankCapacity}.{c.name}", c.capacityUnits);
            }
        }
    }

    /// <summary>Resource fraction of full-loaded mass: <c>Σ capacity·massPerUnit / (dryMass + Σ capacity·massPerUnit) × 100</c>, in percent.</summary>
    /// <remarks>
    /// Stays whole-tank rather than per-resource because the percentage is a single property of
    /// the tank as a whole. Requires recipe-resolved mass-per-unit values for every resource
    /// the tank stores. A single unknown resource yields no entry.
    /// </remarks>
    internal sealed class TankResourcePercentExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            PartDataMirror data = part?.Data;
            if (data?.resourceContainers == null || data.resourceContainers.Count == 0)
            {
                yield break;
            }
            if (data.mass <= 0f)
            {
                yield break;
            }
            float fuelMass = 0f;
            foreach (ResourceContainerMirror c in data.resourceContainers)
            {
                if (c == null || string.IsNullOrEmpty(c.name))
                {
                    continue;
                }
                if (!ctx.ResourceMassPerUnit.TryGetValue(c.name, out float massPerUnit))
                {
                    yield break;
                }
                fuelMass += c.capacityUnits * massPerUnit;
            }
            if (fuelMass <= 0f)
            {
                yield break;
            }
            yield return (StockFieldNames.TankResourcePercent, fuelMass / (data.mass + fuelMass) * 100f);
        }
    }
}
#endif
