#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>
    /// Lift coefficient from <c>Data_LiftingSurface</c>. Wing and tail-fin parts only -
    /// control surfaces have their own Data_ControlSurface fields.
    /// </summary>
    internal sealed class LiftingSurfaceExtractor : IStockFieldExtractor
    {
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            LiftingSurfaceDataObjectMirror lift = ModuleResolver.FindModuleData<LiftingSurfaceDataObjectMirror>(part);
            if (lift == null)
            {
                yield break;
            }
            yield return (StockFieldNames.LiftSurfaceDeflectionCoeff, lift.deflectionLiftCoeff);
        }
    }
}
#endif
