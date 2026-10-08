#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Effective RCS thrust keyed by propellant: <c>maxThrust * thrustPercentage / 100</c>, in kN.</summary>
    /// <remarks>
    /// <c>thrustPercentage</c> is the runtime-settable percentage of <c>maxThrust</c>. Authored
    /// value is typically 100, so effective thrust usually equals <c>maxThrust</c>. Keyed by
    /// propellant for consistency with engines.
    /// </remarks>
    internal sealed class RcsThrustExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            RcsDataObjectMirror rcs = ModuleResolver.FindModuleData<RcsDataObjectMirror>(part);
            if (rcs == null || rcs.maxThrust <= 0f)
            {
                yield break;
            }
            string propellant = rcs.propellant?.mixtureName;
            if (string.IsNullOrEmpty(propellant))
            {
                yield break;
            }
            float pct = rcs.thrustPercentage?.storedValue ?? 100f;
            float effective = rcs.maxThrust * (pct / 100f);
            yield return ($"{StockFieldNames.RcsThrust}.{propellant}", effective);
        }
    }
}
#endif
