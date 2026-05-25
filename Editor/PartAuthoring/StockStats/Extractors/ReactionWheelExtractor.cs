#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Pitch / yaw / roll torque from Data_ReactionWheel, in kN·m.</summary>
    internal sealed class ReactionWheelExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            ReactionWheelDataObjectMirror rw = ModuleResolver.FindModuleData<ReactionWheelDataObjectMirror>(part);
            if (rw == null)
            {
                yield break;
            }
            yield return (StockFieldNames.ReactionWheelPitchTorque, rw.PitchTorque);
            yield return (StockFieldNames.ReactionWheelYawTorque, rw.YawTorque);
            yield return (StockFieldNames.ReactionWheelRollTorque, rw.RollTorque);

            if (rw.RequiredResources != null)
            {
                foreach (PartModuleResourceSettingMirror setting in rw.RequiredResources)
                {
                    if (setting != null && setting.ResourceName == "ElectricCharge")
                    {
                        yield return (StockFieldNames.ReactionWheelEcRate, setting.Rate);
                        break;
                    }
                }
            }
        }
    }
}
#endif
