#if REDUX
using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats.Extractors
{
    /// <summary>Engine peak thrust per mode keyed by propellant, in kN.</summary>
    internal sealed class EngineMaxThrustExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            EngineDataObjectMirror engine = ModuleResolver.FindModuleData<EngineDataObjectMirror>(part);
            if (engine?.engineModes == null)
            {
                yield break;
            }
            foreach (EngineModeMirror mode in engine.engineModes)
            {
                if (mode == null || mode.maxThrust <= 0f)
                {
                    continue;
                }
                string propellant = mode.propellant?.mixtureName;
                if (string.IsNullOrEmpty(propellant))
                {
                    continue;
                }
                yield return ($"{StockFieldNames.EngineMaxThrust}.{propellant}", mode.maxThrust);
            }
        }
    }

    /// <summary>Vacuum Isp per mode keyed by propellant, in seconds.</summary>
    internal sealed class EngineIspVacExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            EngineDataObjectMirror engine = ModuleResolver.FindModuleData<EngineDataObjectMirror>(part);
            if (engine?.engineModes == null)
            {
                yield break;
            }
            foreach (EngineModeMirror mode in engine.engineModes)
            {
                if (mode == null)
                {
                    continue;
                }
                string propellant = mode.propellant?.mixtureName;
                if (string.IsNullOrEmpty(propellant))
                {
                    continue;
                }
                float isp = CurveEvaluator.EvaluateAt(mode.atmosphereCurve, time: 0f);
                if (float.IsNaN(isp) || isp <= 0f)
                {
                    continue;
                }
                yield return ($"{StockFieldNames.EngineIspVac}.{propellant}", isp);
            }
        }
    }

    /// <summary>Sea-level Isp per mode keyed by propellant, in seconds.</summary>
    internal sealed class EngineIspSlExtractor : IStockFieldExtractor
    {
        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            EngineDataObjectMirror engine = ModuleResolver.FindModuleData<EngineDataObjectMirror>(part);
            if (engine?.engineModes == null)
            {
                yield break;
            }
            foreach (EngineModeMirror mode in engine.engineModes)
            {
                if (mode == null)
                {
                    continue;
                }
                string propellant = mode.propellant?.mixtureName;
                if (string.IsNullOrEmpty(propellant))
                {
                    continue;
                }
                float isp = CurveEvaluator.EvaluateAt(mode.atmosphereCurve, time: 1f);
                if (float.IsNaN(isp) || isp <= 0f)
                {
                    continue;
                }
                yield return ($"{StockFieldNames.EngineIspSl}.{propellant}", isp);
            }
        }
    }

    /// <summary>Engine mass flow per mode keyed by propellant, derived from thrust and vacuum Isp. Units: kg/s.</summary>
    /// <remarks>
    /// Computed as <c>maxThrust * 1000 / (Isp_vac * g0)</c> for each mode. Yields one entry per
    /// propellant when both inputs are positive.
    /// </remarks>
    internal sealed class EngineFuelFlowExtractor : IStockFieldExtractor
    {
        private const float G0 = 9.80665f;

        /// <inheritdoc />
        public IEnumerable<(string Name, float Value)> Extract(StockBakePartCore part, BakeContext ctx)
        {
            EngineDataObjectMirror engine = ModuleResolver.FindModuleData<EngineDataObjectMirror>(part);
            if (engine?.engineModes == null)
            {
                yield break;
            }
            foreach (EngineModeMirror mode in engine.engineModes)
            {
                if (mode == null || mode.maxThrust <= 0f)
                {
                    continue;
                }
                string propellant = mode.propellant?.mixtureName;
                if (string.IsNullOrEmpty(propellant))
                {
                    continue;
                }
                float ispVac = CurveEvaluator.EvaluateAt(mode.atmosphereCurve, time: 0f);
                if (float.IsNaN(ispVac) || ispVac <= 0f)
                {
                    continue;
                }
                float flow = mode.maxThrust * 1000f / (ispVac * G0);
                yield return ($"{StockFieldNames.EngineFuelFlow}.{propellant}", flow);
            }
        }
    }
}
#endif
