using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.StockStats
{
    /// <summary>Applies a value from the lookup back onto the active part.</summary>
    /// <param name="target">Authored part to mutate.</param>
    /// <param name="value">Value to apply.</param>
    /// <param name="error">Receives a human-readable explanation when the copy fails.</param>
    /// <returns>True if the value was applied, false otherwise.</returns>
    public delegate bool StockFieldCopier(CorePartData target, float value, out string error);

    /// <summary>Per-field display metadata and Copy behaviour.</summary>
    public sealed class StockFieldEntry
    {
        /// <summary>Canonical field name as defined in <see cref="StockFieldNames" />.</summary>
        public string Name;
        /// <summary>Human-readable display name for the field.</summary>
        public string DisplayName;
        /// <summary>Sub-key for fields keyed by propellant, resource, or experiment id.</summary>
        public string SubKey;
        /// <summary>Units suffix appended after the formatted value.</summary>
        public string UnitsSuffix;
        /// <summary>Format string applied to the numeric value.</summary>
        public string Format;
        /// <summary>Delegate that copies a value from the lookup back onto the active part. Null when the field is read-only.</summary>
        public StockFieldCopier Copier;
        /// <summary>Reason surfaced to the user when <see cref="Copier" /> is null.</summary>
        public string NonCopyableReason;
        /// <summary>True when the field is computed from other authored values (e.g. Resource %, Peak Output, Fuel Flow). Surfaces should hide derived fields when showing "what will be seeded."</summary>
        public bool IsDerived;
        /// <summary>True when the underlying PartData / module field is an int. Editors should use IntegerField instead of FloatField.</summary>
        public bool IsInteger;
        /// <summary>True when <see cref="Copier" /> is non-null.</summary>
        public bool IsCopyable => Copier != null;
    }

    /// <summary>Catalog of <see cref="StockFieldEntry" /> records keyed by canonical field name.</summary>
    public static class StockFieldPaths
    {
        private static readonly List<StockFieldEntry> _staticEntries = BuildStaticEntries();
        private static readonly Dictionary<string, StockFieldEntry> _staticByName = BuildStaticIndex(_staticEntries);

        /// <summary>
        /// Returns the entry for the given canonical field name, building a dynamic entry for sub-keyed names when needed.
        /// </summary>
        /// <param name="name">Canonical field name.</param>
        /// <returns>The matching entry, or null when no static or dynamic entry resolves.</returns>
        public static StockFieldEntry Find(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return _staticByName.TryGetValue(name, out var entry) ? entry : BuildDynamic(name);
        }

        private static Dictionary<string, StockFieldEntry> BuildStaticIndex(List<StockFieldEntry> entries)
        {
            var dict = new Dictionary<string, StockFieldEntry>(entries.Count, StringComparer.Ordinal);
            foreach (var entry in entries) dict[entry.Name] = entry;
            return dict;
        }

        private static List<StockFieldEntry> BuildStaticEntries()
        {
            return new List<StockFieldEntry>
            {
                // PartData scalars
                ScalarEntry(StockFieldNames.Mass,               "Mass",                " t",     "{0:0.000}", "Copy mass",                (d, v) => d.mass = v),
                ScalarEntry(StockFieldNames.Cost,               "Cost",                "",       "{0:0}",     "Copy cost",                (d, v) => d.cost = (int)v, isInteger: true),
                ScalarEntry(StockFieldNames.CrashTolerance,     "Crash Tolerance",     " m/s",   "{0:0.0}",   "Copy crash tolerance",     (d, v) => d.crashTolerance = v),
                ScalarEntry(StockFieldNames.BreakingForce,      "Breaking Force",      " kN",    "{0:0.0}",   "Copy breaking force",      (d, v) => d.breakingForce = v),
                ScalarEntry(StockFieldNames.BreakingTorque,     "Breaking Torque",     " kN·m",  "{0:0.0}",   "Copy breaking torque",     (d, v) => d.breakingTorque = v),
                ScalarEntry(StockFieldNames.ExplosionPotential, "Explosion Potential", "",       "{0:0.00}",  "Copy explosion potential", (d, v) => d.explosionPotential = v),
                ScalarEntry(StockFieldNames.MaxTemp,            "Max Temp",            " K",     "{0:0}",     "Copy max temp",            (d, v) => d.maxTemp = v),
                ScalarEntry(StockFieldNames.CrewCapacity,       "Crew Capacity",       "",       "{0:0}",     "Copy crew capacity",       (d, v) => d.crewCapacity = (int)v, isInteger: true),
                ScalarEntry(StockFieldNames.HeatConductivity,   "Heat Conductivity",   "",       "{0:0.000}", "Copy heat conductivity",   (d, v) => d.heatConductivity = v),
                ScalarEntry(StockFieldNames.SkinMaxTemp,        "Skin Max Temp",       " K",     "{0:0}",     "Copy skin max temp",       (d, v) => d.skinMaxTemp = v),
                ScalarEntry(StockFieldNames.MaxLength,          "Max Length",          " m",     "{0:0}",     "Copy max length",          (d, v) => d.maxLength = (int)v, isInteger: true),
                ScalarEntry(StockFieldNames.Buoyancy,           "Buoyancy",            "",       "{0:0.000}", "Copy buoyancy",            (d, v) => d.buoyancy = v),
                ScalarEntry(StockFieldNames.AngularDrag,         "Angular Drag",        "",       "{0:0.###}", "Copy angular drag",        (d, v) => d.angularDrag = v),
                VectorComponentEntry(StockFieldNames.CoMassOffsetX,     "CoM Offset X", "Copy coMassOffset.x",     d => d.coMassOffset,     (d, v) => d.coMassOffset = v,     0),
                VectorComponentEntry(StockFieldNames.CoMassOffsetY,     "CoM Offset Y", "Copy coMassOffset.y",     d => d.coMassOffset,     (d, v) => d.coMassOffset = v,     1),
                VectorComponentEntry(StockFieldNames.CoMassOffsetZ,     "CoM Offset Z", "Copy coMassOffset.z",     d => d.coMassOffset,     (d, v) => d.coMassOffset = v,     2),
                VectorComponentEntry(StockFieldNames.CoLiftOffsetX,     "CoL Offset X", "Copy coLiftOffset.x",     d => d.coLiftOffset,     (d, v) => d.coLiftOffset = v,     0),
                VectorComponentEntry(StockFieldNames.CoLiftOffsetY,     "CoL Offset Y", "Copy coLiftOffset.y",     d => d.coLiftOffset,     (d, v) => d.coLiftOffset = v,     1),
                VectorComponentEntry(StockFieldNames.CoLiftOffsetZ,     "CoL Offset Z", "Copy coLiftOffset.z",     d => d.coLiftOffset,     (d, v) => d.coLiftOffset = v,     2),
                VectorComponentEntry(StockFieldNames.CoPressureOffsetX, "CoP Offset X", "Copy coPressureOffset.x", d => d.coPressureOffset, (d, v) => d.coPressureOffset = v, 0),
                VectorComponentEntry(StockFieldNames.CoPressureOffsetY, "CoP Offset Y", "Copy coPressureOffset.y", d => d.coPressureOffset, (d, v) => d.coPressureOffset = v, 1),
                VectorComponentEntry(StockFieldNames.CoPressureOffsetZ, "CoP Offset Z", "Copy coPressureOffset.z", d => d.coPressureOffset, (d, v) => d.coPressureOffset = v, 2),

                // Tank
                NonCopyableEntry(StockFieldNames.TankResourcePercent, "Resource %", " %", "{0:0.0}",
                    "Resource percentage derives from dry mass and fuel mass. Edit mass and container capacities directly.",
                    isDerived: true),

                // Gimbal (engine-paired)
                ModuleEntry<Data_Gimbal>(StockFieldNames.EngineGimbalRange, "Gimbal Range", " °", "{0:0.0}",
                    "Copy engine.gimbalRange", (g, v) => g.gimbalRange = v),
                ModuleEntry<Data_Gimbal>(StockFieldNames.EngineGimbalResponseSpeed, "Gimbal Response", "/s", "{0:0.0}",
                    "Copy engine.gimbalResponseSpeed", (g, v) => g.gimbalResponseSpeed = v),

                // Reaction wheel
                ModuleEntry<Data_ReactionWheel>(StockFieldNames.ReactionWheelPitchTorque, "Pitch Torque", " kN·m", "{0:0.00}",
                    "Copy reactionWheel.pitchTorque", (r, v) => r.PitchTorque = v),
                ModuleEntry<Data_ReactionWheel>(StockFieldNames.ReactionWheelYawTorque, "Yaw Torque", " kN·m", "{0:0.00}",
                    "Copy reactionWheel.yawTorque", (r, v) => r.YawTorque = v),
                ModuleEntry<Data_ReactionWheel>(StockFieldNames.ReactionWheelRollTorque, "Roll Torque", " kN·m", "{0:0.00}",
                    "Copy reactionWheel.rollTorque", (r, v) => r.RollTorque = v),
                ModuleEntry<Data_ReactionWheel>(StockFieldNames.ReactionWheelEcRate, "EC Rate", " EC/s", "{0:0.000}",
                    "Copy reactionWheel.ecRate", (r, v) => UpsertElectricChargeRate(r.RequiredResources, v)),

                // Command (pod / probe core EC requirement)
                ModuleEntry<Data_Command>(StockFieldNames.CommandEcRate, "EC Rate", " EC/s", "{0:0.000}",
                    "Copy command.ecRate", (c, v) => UpsertElectricChargeRate(c.requiredResources, v)),

                // Decouple
                ModuleEntry<Data_Decouple>(StockFieldNames.DecoupleEjectionForce, "Ejection Force", " kN", "{0:0.0}",
                    "Copy decouple.ejectionForce", (d, v) => d.ejectionForce = v),

                // Solar (non-copyable - derived from Rate × Efficiency)
                NonCopyableEntry(StockFieldNames.SolarPeakOutput, "Peak Output", " EC/s", "{0:0.00}",
                    "Peak output derives from Rate × Efficiency. Edit Rate on the Solar Panel module.",
                    isDerived: true),

                // Antenna
                ModuleEntry<Data_Transmitter>(StockFieldNames.AntennaRange, "Range", " m", "{0:0,0}",
                    "Copy antenna.range", (t, v) => t.CommunicationRange = v),
                NonCopyableEntry(StockFieldNames.AntennaDataRate, "Data Rate", "", "{0:0.0}",
                    "Data rate derives from PacketSize / Interval. Edit those instead.",
                    isDerived: true),

                // Parachute
                ModuleEntry<Data_Parachute>(StockFieldNames.ParachuteDeployAltitude, "Deploy Altitude", " m", "{0:0}",
                    "Copy parachute.deployAltitude", (p, v) => p.defaultDeployAltitude = v),
                ModuleEntry<Data_Parachute>(StockFieldNames.ParachuteMinPressureToOpen, "Min Pressure", " atm", "{0:0.00}",
                    "Copy parachute.minPressureToOpen", (p, v) => p.defaultMinAirPressureToOpen = v),
                ModuleEntry<Data_Parachute>(StockFieldNames.ParachuteAreaDeployed, "Area", " m²", "{0:0.0}",
                    "Copy parachute.areaDeployed", (p, v) => p.areaDeployed = v),
                ModuleEntry<Data_Parachute>(StockFieldNames.ParachuteMaxTemp, "Chute Max Temp", " K", "{0:0}",
                    "Copy parachute.maxTemp", (p, v) => p.chuteMaxTemp = v),

                // Converter
                ModuleEntry<Data_ResourceConverter>(StockFieldNames.ConverterConversionRate, "Conversion Rate", "", "{0:0.000}",
                    "Copy converter.conversionRate", (c, v) => c.conversionRate?.SetValue(v)),

                // Wheel
                ModuleEntry<Data_WheelBrakes>(StockFieldNames.WheelMaxBrakeTorque, "Max Brake Torque", " kN·m", "{0:0.0}",
                    "Copy wheel.maxBrakeTorque", (w, v) => w.MaxBrakeTorque = v),
                ModuleEntry<Data_WheelSuspension>(StockFieldNames.WheelSuspensionDistance, "Suspension Distance", " m", "{0:0.000}",
                    "Copy wheel.suspensionDistance", (w, v) => w.suspensionDistance = v),

                // Docking
                ModuleEntry<Data_DockingNode>(StockFieldNames.DockingAcquireRange, "Acquire Range", " m", "{0:0.00}",
                    "Copy docking.acquireRange", (d, v) => d.AcquireRange = v),
                ModuleEntry<Data_DockingNode>(StockFieldNames.DockingAcquireForce, "Acquire Force", " kN", "{0:0.0}",
                    "Copy docking.acquireForce", (d, v) => d.AcquireForce = v),
                ModuleEntry<Data_DockingNode>(StockFieldNames.DockingCaptureRange, "Capture Range", " m", "{0:0.000}",
                    "Copy docking.captureRange", (d, v) => d.CaptureRange = v),

                // Control surface
                ModuleEntry<Data_ControlSurface>(StockFieldNames.ControlSurfaceRange, "Deflection Range", " °", "{0:0.0}",
                    "Copy controlSurface.range", (cs, v) => cs.CtrlSurfaceRange = v),
                ModuleEntry<Data_ControlSurface>(StockFieldNames.ControlSurfaceArea, "Area", " m²", "{0:0.00}",
                    "Copy controlSurface.area", (cs, v) => cs.CtrlSurfaceArea = v),
                ModuleEntry<Data_ControlSurface>(StockFieldNames.ControlSurfaceActuatorSpeed, "Actuator Speed", "/s", "{0:0.0}",
                    "Copy controlSurface.actuatorSpeed", (cs, v) => cs.ActuatorSpeedNormalScale = v),

                // Resource intake (jet engine air intake)
                ModuleEntry<Data_ResourceIntake>(StockFieldNames.IntakeArea, "Intake Area", " m²", "{0:0.00}",
                    "Copy intake.area", (i, v) => i.area = v),
                ModuleEntry<Data_ResourceIntake>(StockFieldNames.IntakeSpeed, "Intake Speed", " m/s", "{0:0}",
                    "Copy intake.intakeSpeed", (i, v) => i.intakeSpeed = v),

                // Lifting surface (wings, tail fins)
                ModuleEntry<Data_LiftingSurface>(StockFieldNames.LiftSurfaceDeflectionCoeff, "Lift Coefficient", "", "{0:0.00}",
                    "Copy liftSurface.deflectionLiftCoeff", (l, v) => l.deflectionLiftCoeff = v),

                // Heatshield
                ModuleEntry<Data_Heatshield>(StockFieldNames.HeatshieldAblationTempThreshold, "Ablation Temp", " K", "{0:0}",
                    "Copy heatshield.ablationTempThreshold", (h, v) => h.AblationTempThreshold = v),
                ModuleEntry<Data_Heatshield>(StockFieldNames.HeatshieldAblationMaxOverThreshold, "Ablation Max", " K", "{0:0.0}",
                    "Copy heatshield.ablationMaxOverThreshold", (h, v) => h.AblationMaximumOverThreshold = v),
                ModuleEntry<Data_Heatshield>(StockFieldNames.HeatshieldPyrolysisLossFactor, "Pyrolysis Loss", "", "{0:0.00}",
                    "Copy heatshield.pyrolysisLossFactor", (h, v) => h.PyrolysisLossFactor = v),
                ModuleEntry<Data_Heatshield>(StockFieldNames.HeatshieldShieldingScale, "Shielding Scale", "", "{0:0.00}",
                    "Copy heatshield.shieldingScale", (h, v) => h.ShieldingScale = v),

                // Active radiator
                ModuleEntry<Data_ActiveRadiator>(StockFieldNames.RadiatorFluxPerAreaUnit, "Flux per Area", " kW/m²", "{0:0.0}",
                    "Copy radiator.fluxPerAreaUnit", (r, v) => r.ProceduralRadiatorFluxPerAreaUnit = v),

                // Cargo bay
                ModuleEntry<Data_CargoBay>(StockFieldNames.CargoBayInternalLength, "Internal Length", " m", "{0:0.00}",
                    "Copy cargoBay.internalLength", (c, v) => c.BayInternalLength = v),
                ModuleEntry<Data_CargoBay>(StockFieldNames.CargoBayLookUpRadius, "Look-up Radius", " m", "{0:0.00}",
                    "Copy cargoBay.lookUpRadius", (c, v) => c.lookUpRadius = v),
            };
        }

        private static StockFieldEntry BuildDynamic(string name)
        {
            if (TryStripPrefix(name, StockFieldNames.EngineMaxThrust + ".", out string emtProp))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Max Thrust",
                    SubKey = emtProp,
                    UnitsSuffix = " kN",
                    Format = "{0:0.0}",
                    Copier = MakeEngineMaxThrustCopier(emtProp),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.EngineIspVac + ".", out string evProp))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Isp vac",
                    SubKey = evProp,
                    UnitsSuffix = " s",
                    Format = "{0:0}",
                    Copier = MakeEngineIspCopier(evProp, time: 0f, undoLabel: "Copy engine.isp_vac"),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.EngineIspSl + ".", out string esProp))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Isp SL",
                    SubKey = esProp,
                    UnitsSuffix = " s",
                    Format = "{0:0}",
                    Copier = MakeEngineIspCopier(esProp, time: 1f, undoLabel: "Copy engine.isp_sl"),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.EngineFuelFlow + ".", out string ffProp))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Fuel Flow",
                    SubKey = ffProp,
                    UnitsSuffix = " kg/s",
                    Format = "{0:0.00}",
                    Copier = null,
                    NonCopyableReason = "Fuel flow is derived from thrust and Isp. Edit those instead.",
                    IsDerived = true,
                };
            }
            if (TryStripPrefix(name, StockFieldNames.TankCapacity + ".", out string tcResource))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Capacity",
                    SubKey = tcResource,
                    UnitsSuffix = " u",
                    Format = "{0:0.###}",
                    Copier = MakeTankCapacityCopier(tcResource),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.RcsThrust + ".", out string rtProp))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Thrust",
                    SubKey = rtProp,
                    UnitsSuffix = " kN",
                    Format = "{0:0.00}",
                    Copier = MakeRcsThrustCopier(rtProp),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.GeneratorOutput + ".", out string genResource))
            {
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = "Output",
                    SubKey = genResource,
                    UnitsSuffix = "/s",
                    Format = "{0:0.00}",
                    Copier = MakeGeneratorOutputCopier(genResource),
                };
            }
            if (TryStripPrefix(name, StockFieldNames.ScienceExperiment + ".", out string sciTail))
            {
                int dotIdx = sciTail.IndexOf('.');
                string expId = dotIdx > 0 ? sciTail.Substring(0, dotIdx) : sciTail;
                string prop = dotIdx > 0 ? sciTail.Substring(dotIdx + 1) : null;
                string display;
                string units;
                string fmt;
                bool sciIsInteger = false;
                switch (prop)
                {
                    case "timeToComplete":
                        display = "Time to Complete"; units = " s"; fmt = "{0:0.#}";
                        break;
                    case "crewRequired":
                        display = "Crew Required"; units = ""; fmt = "{0:0}"; sciIsInteger = true;
                        break;
                    case "ecRate":
                        display = "EC Rate"; units = " EC/s"; fmt = "{0:0.000}";
                        break;
                    default:
                        display = "Experiment"; units = ""; fmt = "{0:0.##}";
                        break;
                }
                return new StockFieldEntry
                {
                    Name = name,
                    DisplayName = display,
                    SubKey = expId,
                    UnitsSuffix = units,
                    Format = fmt,
                    Copier = null,
                    NonCopyableReason = "Science experiment settings are opinionated. Edit via the Module_ScienceExperiment inspector.",
                    IsInteger = sciIsInteger,
                };
            }
            return null;
        }

        private static void UpsertElectricChargeRate(List<PartModuleResourceSetting> list, float rate)
        {
            if (list == null)
            {
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].ResourceName == "ElectricCharge")
                {
                    PartModuleResourceSetting updated = list[i];
                    updated.Rate = rate;
                    list[i] = updated;
                    return;
                }
            }
            list.Add(new PartModuleResourceSetting
            {
                ResourceName = "ElectricCharge",
                Rate = rate
            });
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

        private static StockFieldEntry ScalarEntry(
            string name,
            string display,
            string units,
            string format,
            string undoLabel,
            Action<PartData, float> mutator,
            bool isInteger = false)
        {
            StockFieldCopier copier = delegate (CorePartData target, float value, out string error)
            {
                error = null;
                if (target == null || target.Data == null)
                {
                    error = "Active part has no PartData.";
                    return false;
                }
                Undo.RecordObject(target, undoLabel);
                mutator(target.Data, value);
                EditorUtility.SetDirty(target);
                return true;
            };
            return new StockFieldEntry
            {
                Name = name,
                DisplayName = display,
                UnitsSuffix = units,
                Format = format,
                Copier = copier,
                IsInteger = isInteger,
            };
        }

        private static StockFieldEntry VectorComponentEntry(
            string name,
            string display,
            string undoLabel,
            Func<PartData, Vector3> get,
            Action<PartData, Vector3> set,
            int axis)
        {
            StockFieldCopier copier = delegate (CorePartData target, float value, out string error)
            {
                error = null;
                if (target == null || target.Data == null)
                {
                    error = "Active part has no PartData.";
                    return false;
                }
                Undo.RecordObject(target, undoLabel);
                PartData data = target.Data;
                Vector3 vector = get(data);
                switch (axis)
                {
                    case 0:
                        vector.x = value;
                        break;
                    case 1:
                        vector.y = value;
                        break;
                    default:
                        vector.z = value;
                        break;
                }
                set(data, vector);
                EditorUtility.SetDirty(target);
                return true;
            };
            return new StockFieldEntry
            {
                Name = name,
                DisplayName = display,
                UnitsSuffix = " m",
                Format = "{0:0.###}",
                Copier = copier,
            };
        }

        private static StockFieldEntry ModuleEntry<TModule>(
            string name,
            string display,
            string units,
            string format,
            string undoLabel,
            Action<TModule, float> mutator) where TModule : class
        {
            StockFieldCopier copier = delegate (CorePartData target, float value, out string error)
            {
                TModule module = FindModule<TModule>(target, out error);
                if (module == null)
                {
                    return false;
                }
                Undo.RecordObject(target, undoLabel);
                mutator(module, value);
                EditorUtility.SetDirty(target);
                return true;
            };
            return new StockFieldEntry
            {
                Name = name,
                DisplayName = display,
                UnitsSuffix = units,
                Format = format,
                Copier = copier,
            };
        }

        private static StockFieldEntry NonCopyableEntry(
            string name,
            string display,
            string units,
            string format,
            string reason,
            bool isDerived = false)
        {
            return new StockFieldEntry
            {
                Name = name,
                DisplayName = display,
                UnitsSuffix = units,
                Format = format,
                Copier = null,
                NonCopyableReason = reason,
                IsDerived = isDerived,
            };
        }

        private static StockFieldCopier MakeEngineMaxThrustCopier(string propellantName)
        {
            return delegate (CorePartData target, float value, out string error)
            {
                error = null;
                Data_Engine engine = FindModule<Data_Engine>(target, out error);
                if (engine == null)
                {
                    return false;
                }
                Data_Engine.EngineMode mode = FindModeByPropellant(engine, propellantName, out error);
                if (mode == null)
                {
                    return false;
                }
                Undo.RecordObject(target, "Copy engine.maxThrust");
                mode.maxThrust = value;
                EditorUtility.SetDirty(target);
                return true;
            };
        }

        private static StockFieldCopier MakeEngineIspCopier(string propellantName, float time, string undoLabel)
        {
            return delegate (CorePartData target, float value, out string error)
            {
                error = null;
                Data_Engine engine = FindModule<Data_Engine>(target, out error);
                if (engine == null)
                {
                    return false;
                }
                Data_Engine.EngineMode mode = FindModeByPropellant(engine, propellantName, out error);
                if (mode == null)
                {
                    return false;
                }
                if (mode.atmosphereCurve == null)
                {
                    error = $"Mode '{propellantName}' has no atmosphereCurve.";
                    return false;
                }
                Undo.RecordObject(target, undoLabel);
                SetCurveKeyValue(mode.atmosphereCurve.Curve, time, value);
                EditorUtility.SetDirty(target);
                return true;
            };
        }

        private static StockFieldCopier MakeTankCapacityCopier(string resourceName)
        {
            return delegate (CorePartData target, float value, out string error)
            {
                error = null;
                if (target == null || target.Data == null)
                {
                    error = "Active part has no PartData.";
                    return false;
                }
                ContainedResourceDefinition container = FindContainer(target.Data, resourceName);
                if (container == null)
                {
                    error = $"Active part has no container for '{resourceName}'.";
                    return false;
                }
                Undo.RecordObject(target, "Copy tank.capacity");
                container.capacityUnits = value;
                if (container.initialUnits > container.capacityUnits)
                {
                    container.initialUnits = container.capacityUnits;
                }
                EditorUtility.SetDirty(target);
                return true;
            };
        }

        private static StockFieldCopier MakeRcsThrustCopier(string propellantName)
        {
            return delegate (CorePartData target, float value, out string error)
            {
                error = null;
                Data_RCS rcs = FindModule<Data_RCS>(target, out error);
                if (rcs == null)
                {
                    return false;
                }
                string activeProp = rcs.Propellant?.mixtureName;
                if (!string.Equals(activeProp, propellantName, StringComparison.Ordinal))
                {
                    error = $"Active RCS uses propellant '{activeProp ?? "(none)"}', not '{propellantName}'.";
                    return false;
                }
                float pct = rcs.thrustPercentage != null ? rcs.thrustPercentage.GetValue() : 100f;
                if (pct <= 0f)
                {
                    error = "thrustPercentage is zero; cannot back-derive maxThrust.";
                    return false;
                }
                Undo.RecordObject(target, "Copy rcs.thrust");
                rcs.maxThrust = value * 100f / pct;
                EditorUtility.SetDirty(target);
                return true;
            };
        }

        private static StockFieldCopier MakeGeneratorOutputCopier(string resourceName)
        {
            return delegate (CorePartData target, float value, out string error)
            {
                error = null;
                Data_ModuleGenerator gen = FindModule<Data_ModuleGenerator>(target, out error);
                if (gen == null)
                {
                    return false;
                }
                if (!string.Equals(gen.ResourceSetting.ResourceName, resourceName, StringComparison.Ordinal))
                {
                    error = $"Active generator outputs '{gen.ResourceSetting.ResourceName ?? "(none)"}', not '{resourceName}'.";
                    return false;
                }
                Undo.RecordObject(target, "Copy generator.output");
                gen.ResourceSetting.Rate = value;
                EditorUtility.SetDirty(target);
                return true;
            };
        }

        private static T FindModule<T>(CorePartData target, out string error) where T : class
        {
            error = null;
            var modules = target?.Core?.modules;
            if (modules == null)
            {
                error = "Active part has no modules.";
                return null;
            }
            foreach (var module in modules)
            {
                if (module is T match)
                {
                    return match;
                }
            }
            error = $"Active part has no {typeof(T).Name} module.";
            return null;
        }

        private static Data_Engine.EngineMode FindModeByPropellant(Data_Engine engine, string propellantName, out string error)
        {
            error = null;
            if (engine.engineModes == null || engine.engineModes.Length == 0)
            {
                error = "Engine has no modes.";
                return null;
            }
            foreach (Data_Engine.EngineMode mode in engine.engineModes)
            {
                if (mode == null)
                {
                    continue;
                }
                if (string.Equals(mode.propellant?.mixtureName, propellantName, StringComparison.Ordinal))
                {
                    return mode;
                }
            }
            error = $"Engine has no mode using propellant '{propellantName}'.";
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

        private static void SetCurveKeyValue(AnimationCurve curve, float time, float value)
        {
            if (curve == null)
            {
                return;
            }
            Keyframe[] keys = curve.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (Mathf.Approximately(keys[i].time, time))
                {
                    keys[i].value = value;
                    curve.keys = keys;
                    return;
                }
            }
            curve.AddKey(time, value);
        }
    }
}
