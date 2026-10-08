using System;
using KSP;
using KSP.Modules;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Reads tracked-field values directly from a live <see cref="CorePartData" />.</summary>
    /// <remarks>
    /// Mirrors the bake's extractor logic but reads from the in-memory authored data instead of
    /// JSON. Used by the reference window to display the active part's current value alongside
    /// the bucket's min/max. Supports the same field shapes the baker produces.
    /// </remarks>
    public static class ActivePartFieldReader
    {
        private const float G0 = 9.80665f;

        /// <summary>
        /// Reads the current value of a tracked stock field from an in-memory authored part.
        /// </summary>
        /// <param name="fieldName">Name of the tracked field, as defined in <see cref="StockFieldNames" />.</param>
        /// <param name="target">Authored part to read from.</param>
        /// <param name="value">Receives the read value when the call succeeds, otherwise zero.</param>
        /// <returns>True if the field was found and read, false otherwise.</returns>
        public static bool TryRead(string fieldName, CorePartData target, out float value)
        {
            value = 0f;
            if (target?.Data == null || string.IsNullOrEmpty(fieldName))
            {
                return false;
            }
            PartData data = target.Data;
            switch (fieldName)
            {
                case StockFieldNames.Mass: value = (float)data.mass; return true;
                case StockFieldNames.Cost: value = data.cost; return true;
                case StockFieldNames.CrashTolerance: value = (float)data.crashTolerance; return true;
                case StockFieldNames.BreakingForce: value = (float)data.breakingForce; return true;
                case StockFieldNames.BreakingTorque: value = (float)data.breakingTorque; return true;
                case StockFieldNames.ExplosionPotential: value = (float)data.explosionPotential; return true;
                case StockFieldNames.MaxTemp: value = (float)data.maxTemp; return true;
                case StockFieldNames.CrewCapacity: value = data.crewCapacity; return true;
                case StockFieldNames.HeatConductivity: value = (float)data.heatConductivity; return true;
                case StockFieldNames.SkinMaxTemp: value = (float)data.skinMaxTemp; return true;
                case StockFieldNames.MaxLength: value = data.maxLength; return true;
                case StockFieldNames.Buoyancy: value = (float)data.buoyancy; return true;
                case StockFieldNames.AngularDrag: value = (float)data.angularDrag; return true;
                case StockFieldNames.CoMassOffsetX: value = data.coMassOffset.x; return true;
                case StockFieldNames.CoMassOffsetY: value = data.coMassOffset.y; return true;
                case StockFieldNames.CoMassOffsetZ: value = data.coMassOffset.z; return true;
                case StockFieldNames.CoLiftOffsetX: value = data.coLiftOffset.x; return true;
                case StockFieldNames.CoLiftOffsetY: value = data.coLiftOffset.y; return true;
                case StockFieldNames.CoLiftOffsetZ: value = data.coLiftOffset.z; return true;
                case StockFieldNames.CoPressureOffsetX: value = data.coPressureOffset.x; return true;
                case StockFieldNames.CoPressureOffsetY: value = data.coPressureOffset.y; return true;
                case StockFieldNames.CoPressureOffsetZ: value = data.coPressureOffset.z; return true;
                case StockFieldNames.TankResourcePercent: return false;
                case StockFieldNames.EngineGimbalRange: return TryReadModuleFloat<Data_Gimbal>(target, g => g.gimbalRange, out value);
                case StockFieldNames.EngineGimbalResponseSpeed: return TryReadModuleFloat<Data_Gimbal>(target, g => g.gimbalResponseSpeed, out value);
                case StockFieldNames.ReactionWheelPitchTorque: return TryReadModuleFloat<Data_ReactionWheel>(target, r => r.PitchTorque, out value);
                case StockFieldNames.ReactionWheelYawTorque: return TryReadModuleFloat<Data_ReactionWheel>(target, r => r.YawTorque, out value);
                case StockFieldNames.ReactionWheelRollTorque: return TryReadModuleFloat<Data_ReactionWheel>(target, r => r.RollTorque, out value);
                case StockFieldNames.DecoupleEjectionForce: return TryReadModuleFloat<Data_Decouple>(target, d => d.ejectionForce, out value);
                case StockFieldNames.SolarPeakOutput: return TryReadSolarPeakOutput(target, out value);
                case StockFieldNames.AntennaRange: return TryReadModuleFloat<Data_Transmitter>(target, t => (float)t.CommunicationRange, out value);
                case StockFieldNames.AntennaDataRate: return TryReadAntennaDataRate(target, out value);
                case StockFieldNames.ParachuteDeployAltitude: return TryReadModuleFloat<Data_Parachute>(target, p => p.defaultDeployAltitude, out value);
                case StockFieldNames.ParachuteAreaDeployed: return TryReadModuleFloat<Data_Parachute>(target, p => (float)p.areaDeployed, out value);
                case StockFieldNames.ParachuteMinPressureToOpen: return TryReadModuleFloat<Data_Parachute>(target, p => p.defaultMinAirPressureToOpen, out value);
                case StockFieldNames.ParachuteMaxTemp: return TryReadModuleFloat<Data_Parachute>(target, p => (float)p.chuteMaxTemp, out value);
                case StockFieldNames.ConverterConversionRate: return TryReadModuleFloat<Data_ResourceConverter>(target, c => c.conversionRate != null ? c.conversionRate.GetValue() : 0f, out value);
                case StockFieldNames.WheelMaxBrakeTorque: return TryReadModuleFloat<Data_WheelBrakes>(target, w => w.MaxBrakeTorque, out value);
                case StockFieldNames.WheelSuspensionDistance: return TryReadModuleFloat<Data_WheelSuspension>(target, w => w.suspensionDistance, out value);
                case StockFieldNames.DockingAcquireRange: return TryReadModuleFloat<Data_DockingNode>(target, d => d.AcquireRange, out value);
                case StockFieldNames.DockingAcquireForce: return TryReadModuleFloat<Data_DockingNode>(target, d => d.AcquireForce, out value);
                case StockFieldNames.DockingCaptureRange: return TryReadModuleFloat<Data_DockingNode>(target, d => d.CaptureRange, out value);
                case StockFieldNames.ControlSurfaceRange: return TryReadModuleFloat<Data_ControlSurface>(target, cs => cs.CtrlSurfaceRange, out value);
                case StockFieldNames.ControlSurfaceArea: return TryReadModuleFloat<Data_ControlSurface>(target, cs => cs.CtrlSurfaceArea, out value);
                case StockFieldNames.ControlSurfaceActuatorSpeed: return TryReadModuleFloat<Data_ControlSurface>(target, cs => cs.ActuatorSpeedNormalScale, out value);
                case StockFieldNames.CommandEcRate: return TryReadCommandEcRate(target, out value);
                case StockFieldNames.ReactionWheelEcRate: return TryReadReactionWheelEcRate(target, out value);
                case StockFieldNames.IntakeArea: return TryReadModuleFloat<Data_ResourceIntake>(target, i => i.area, out value);
                case StockFieldNames.IntakeSpeed: return TryReadModuleFloat<Data_ResourceIntake>(target, i => (float)i.intakeSpeed, out value);
                case StockFieldNames.LiftSurfaceDeflectionCoeff: return TryReadModuleFloat<Data_LiftingSurface>(target, l => l.deflectionLiftCoeff, out value);
                case StockFieldNames.HeatshieldAblationTempThreshold: return TryReadModuleFloat<Data_Heatshield>(target, h => (float)h.AblationTempThreshold, out value);
                case StockFieldNames.HeatshieldAblationMaxOverThreshold: return TryReadModuleFloat<Data_Heatshield>(target, h => (float)h.AblationMaximumOverThreshold, out value);
                case StockFieldNames.HeatshieldPyrolysisLossFactor: return TryReadModuleFloat<Data_Heatshield>(target, h => (float)h.PyrolysisLossFactor, out value);
                case StockFieldNames.HeatshieldShieldingScale: return TryReadModuleFloat<Data_Heatshield>(target, h => h.ShieldingScale, out value);
                case StockFieldNames.RadiatorFluxPerAreaUnit: return TryReadModuleFloat<Data_ActiveRadiator>(target, r => r.ProceduralRadiatorFluxPerAreaUnit, out value);
                case StockFieldNames.CargoBayInternalLength: return TryReadModuleFloat<Data_CargoBay>(target, c => c.BayInternalLength, out value);
                case StockFieldNames.CargoBayLookUpRadius: return TryReadModuleFloat<Data_CargoBay>(target, c => c.lookUpRadius, out value);
            }

            if (TryStripPrefix(fieldName, StockFieldNames.EngineMaxThrust + ".", out string emtProp))
            {
                return TryReadEngineMaxThrust(target, emtProp, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.EngineIspVac + ".", out string evProp))
            {
                return TryReadEngineIsp(target, evProp, time: 0f, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.EngineIspSl + ".", out string esProp))
            {
                return TryReadEngineIsp(target, esProp, time: 1f, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.EngineFuelFlow + ".", out string ffProp))
            {
                return TryReadEngineFuelFlow(target, ffProp, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.TankCapacity + ".", out string tcResource))
            {
                return TryReadTankCapacity(target, tcResource, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.RcsThrust + ".", out string rtProp))
            {
                return TryReadRcsThrust(target, rtProp, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.GeneratorOutput + ".", out string genResource))
            {
                return TryReadGeneratorOutput(target, genResource, out value);
            }
            if (TryStripPrefix(fieldName, StockFieldNames.ScienceExperiment + ".", out string sciTail))
            {
                return TryReadScienceExperimentProperty(target, sciTail, out value);
            }
            return false;
        }

        private static bool TryReadCommandEcRate(CorePartData target, out float value)
        {
            value = 0f;
            Data_Command cmd = FindModule<Data_Command>(target);
            if (cmd?.requiredResources == null)
            {
                return false;
            }
            foreach (PartModuleResourceSetting setting in cmd.requiredResources)
            {
                if (setting.ResourceName == "ElectricCharge")
                {
                    value = setting.Rate;
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadReactionWheelEcRate(CorePartData target, out float value)
        {
            value = 0f;
            Data_ReactionWheel rw = FindModule<Data_ReactionWheel>(target);
            if (rw?.RequiredResources == null)
            {
                return false;
            }
            foreach (PartModuleResourceSetting setting in rw.RequiredResources)
            {
                if (setting.ResourceName == "ElectricCharge")
                {
                    value = setting.Rate;
                    return true;
                }
            }
            return false;
        }

        private static bool TryReadScienceExperimentProperty(CorePartData target, string tail, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(tail))
            {
                return false;
            }
            int dotIdx = tail.IndexOf('.');
            if (dotIdx <= 0 || dotIdx >= tail.Length - 1)
            {
                return false;
            }
            string expId = tail.Substring(0, dotIdx);
            string prop = tail.Substring(dotIdx + 1);

            Data_ScienceExperiment sci = FindModule<Data_ScienceExperiment>(target);
            if (sci?.Experiments == null)
            {
                return false;
            }
            foreach (ExperimentConfiguration exp in sci.Experiments)
            {
                if (!string.Equals(exp.ExperimentDefinitionID, expId, StringComparison.Ordinal))
                {
                    continue;
                }
                switch (prop)
                {
                    case "timeToComplete":
                        value = exp.TimeToComplete;
                        return true;
                    case "crewRequired":
                        value = exp.CrewRequired;
                        return true;
                    case "ecRate":
                        if (exp.ResourcesCost != null)
                        {
                            foreach (PartModuleResourceSetting cost in exp.ResourcesCost)
                            {
                                if (cost.ResourceName == "ElectricCharge")
                                {
                                    value = cost.Rate;
                                    return true;
                                }
                            }
                        }
                        value = 0f;
                        return true;
                    default:
                        return false;
                }
            }
            // Experiment not on part - 0.0 is a meaningful "absent" reading.
            value = 0f;
            return true;
        }

        private static bool TryReadModuleFloat<T>(CorePartData target, Func<T, float> selector, out float value) where T : class
        {
            value = 0f;
            T module = FindModule<T>(target);
            if (module == null)
            {
                return false;
            }
            value = selector(module);
            return true;
        }

        private static bool TryReadSolarPeakOutput(CorePartData target, out float value)
        {
            value = 0f;
            Data_SolarPanel sol = FindModule<Data_SolarPanel>(target);
            if (sol == null)
            {
                return false;
            }
            float eff = sol.EfficiencyMultiplier > 0f ? sol.EfficiencyMultiplier : 1f;
            value = sol.ResourceSettings.Rate * eff;
            return true;
        }

        private static bool TryReadAntennaDataRate(CorePartData target, out float value)
        {
            value = 0f;
            Data_Transmitter t = FindModule<Data_Transmitter>(target);
            if (t == null || t.DataTransmissionInterval <= 0f)
            {
                return false;
            }
            value = t.DataPacketSize / t.DataTransmissionInterval;
            return true;
        }

        private static bool TryReadEngineMaxThrust(CorePartData target, string propellant, out float value)
        {
            value = 0f;
            Data_Engine.EngineMode mode = FindEngineMode(target, propellant);
            if (mode == null)
            {
                return false;
            }
            value = mode.maxThrust;
            return true;
        }

        private static bool TryReadEngineIsp(CorePartData target, string propellant, float time, out float value)
        {
            value = 0f;
            Data_Engine.EngineMode mode = FindEngineMode(target, propellant);
            if (mode?.atmosphereCurve?.Curve == null)
            {
                return false;
            }
            value = mode.atmosphereCurve.Curve.Evaluate(time);
            return !float.IsNaN(value);
        }

        private static bool TryReadEngineFuelFlow(CorePartData target, string propellant, out float value)
        {
            value = 0f;
            Data_Engine.EngineMode mode = FindEngineMode(target, propellant);
            if (mode == null || mode.maxThrust <= 0f || mode.atmosphereCurve?.Curve == null)
            {
                return false;
            }
            float ispVac = mode.atmosphereCurve.Curve.Evaluate(0f);
            if (float.IsNaN(ispVac) || ispVac <= 0f)
            {
                return false;
            }
            value = mode.maxThrust * 1000f / (ispVac * G0);
            return true;
        }

        private static bool TryReadTankCapacity(CorePartData target, string resource, out float value)
        {
            value = 0f;
            ContainedResourceDefinition c = FindContainer(target.Data, resource);
            if (c == null)
            {
                return false;
            }
            value = (float)c.capacityUnits;
            return true;
        }

        private static bool TryReadRcsThrust(CorePartData target, string propellant, out float value)
        {
            value = 0f;
            Data_RCS rcs = FindModule<Data_RCS>(target);
            if (rcs == null)
            {
                return false;
            }
            string activeProp = rcs.Propellant?.mixtureName;
            if (!string.Equals(activeProp, propellant, StringComparison.Ordinal))
            {
                return false;
            }
            float pct = rcs.thrustPercentage != null ? rcs.thrustPercentage.GetValue() : 100f;
            value = rcs.maxThrust * (pct / 100f);
            return true;
        }

        private static bool TryReadGeneratorOutput(CorePartData target, string resource, out float value)
        {
            value = 0f;
            Data_ModuleGenerator gen = FindModule<Data_ModuleGenerator>(target);
            if (gen == null)
            {
                return false;
            }
            if (!string.Equals(gen.ResourceSetting.ResourceName, resource, StringComparison.Ordinal))
            {
                return false;
            }
            value = gen.ResourceSetting.Rate;
            return true;
        }

        private static T FindModule<T>(CorePartData target) where T : class
        {
            var modules = target?.Core?.modules;
            if (modules == null)
            {
                return null;
            }
            foreach (var module in modules)
            {
                if (module is T match)
                {
                    return match;
                }
            }
            return null;
        }

        private static Data_Engine.EngineMode FindEngineMode(CorePartData target, string propellant)
        {
            var modules = target?.Core?.modules;
            if (modules == null)
            {
                return null;
            }
            foreach (var module in modules)
            {
                if (module is not Data_Engine engine || engine.engineModes == null)
                {
                    continue;
                }
                foreach (Data_Engine.EngineMode mode in engine.engineModes)
                {
                    if (mode == null)
                    {
                        continue;
                    }
                    if (string.Equals(mode.propellant?.mixtureName, propellant, StringComparison.Ordinal))
                    {
                        return mode;
                    }
                }
            }
            return null;
        }

        private static ContainedResourceDefinition FindContainer(PartData data, string resourceName)
        {
            if (data?.resourceContainers == null)
            {
                return null;
            }
            foreach (ContainedResourceDefinition c in data.resourceContainers)
            {
                if (c == null)
                {
                    continue;
                }
                if (string.Equals(c.name, resourceName, StringComparison.Ordinal))
                {
                    return c;
                }
            }
            return null;
        }

        private static bool TryStripPrefix(string name, string prefix, out string suffix)
        {
            if (name != null && name.StartsWith(prefix, StringComparison.Ordinal) && name.Length > prefix.Length)
            {
                suffix = name.Substring(prefix.Length);
                return true;
            }
            suffix = null;
            return false;
        }
    }
}
