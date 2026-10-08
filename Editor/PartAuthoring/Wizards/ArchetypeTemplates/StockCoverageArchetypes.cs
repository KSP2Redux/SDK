using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using KSP.Sim.ResourceSystem;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    public abstract class SimpleStockArchetypeBase : PartArchetypeBase
    {
        protected static readonly IReadOnlyList<AttachNodeTemplate> NoNodes = Array.Empty<AttachNodeTemplate>();

        protected static readonly IReadOnlyList<AttachNodeTemplate> StackNodes = new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down)
        };

        protected static readonly IReadOnlyList<AttachNodeTemplate> SurfaceNode = new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward)
        };

        public override string DefaultSizeKey => PartSizeRegistry.Sm;
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => StackNodes;

        public override void SeedDefaults(CorePartData part, BucketResolution bucket)
        {
            StockBucket source = FindFirstUsableBucket(bucket);
            if (source == null)
            {
                return;
            }

            SeedCommonPartData(part, source);
            SeedModuleDefaults(part, source);
        }

        protected virtual void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
        }
    }

    public abstract class StructuralArchetypeBase : SimpleStockArchetypeBase
    {
        public override string Category => "Structural";
        public override string Description => "Structural part with drag and color modules.";
        public override IReadOnlyList<Type> DefaultModules => new[] { typeof(Module_Drag), typeof(Module_Color) };
    }

    public abstract class ProceduralShellArchetypeBase : SimpleStockArchetypeBase
    {
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_CargoBay),
            typeof(Module_Fairing),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_CargoBay bay = FindModuleData<Data_CargoBay>(part);
            if (bay != null)
            {
                TrySeedScalar(source, StockFieldNames.CargoBayInternalLength, v => bay.BayInternalLength = v);
                TrySeedScalar(source, StockFieldNames.CargoBayLookUpRadius, v => bay.lookUpRadius = v);
            }
        }
    }

    public class CockpitArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Pods";
        public override string Family => "0020-Cockpit";
        public override string DisplayName => "Cockpit";
        public override string Description => "Crewed aircraft cockpit with command, reaction wheel, transmitter, lighting, and science support.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Command),
            typeof(Module_ReactionWheel),
            typeof(Module_ResourceCapacities),
            typeof(Module_CrewedInterior),
            typeof(Module_ScienceExperiment),
            typeof(Module_DataTransmitter),
            typeof(Module_LitPart),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            if (part?.Data != null)
            {
                TrySeedScalarInt(source, StockFieldNames.CrewCapacity, v => part.Data.crewCapacity = v);
            }
            Data_ReactionWheel wheel = FindModuleData<Data_ReactionWheel>(part);
            if (wheel != null)
            {
                TrySeedScalar(source, StockFieldNames.ReactionWheelPitchTorque, v => wheel.PitchTorque = v);
                TrySeedScalar(source, StockFieldNames.ReactionWheelYawTorque, v => wheel.YawTorque = v);
                TrySeedScalar(source, StockFieldNames.ReactionWheelRollTorque, v => wheel.RollTorque = v);
                wheel.RequiredResources ??= new List<PartModuleResourceSetting>();
                TrySeedScalar(source, StockFieldNames.ReactionWheelEcRate, v => UpsertElectricChargeRate(wheel.RequiredResources, v));
            }
            Data_Command command = FindModuleData<Data_Command>(part);
            if (command != null)
            {
                command.requiredResources ??= new List<PartModuleResourceSetting>();
                TrySeedScalar(source, StockFieldNames.CommandEcRate, v => UpsertElectricChargeRate(command.requiredResources, v));
            }
            Data_ScienceExperiment science = FindModuleData<Data_ScienceExperiment>(part);
            if (science != null)
            {
                science.Experiments ??= new List<ExperimentConfiguration>();
                SeedScienceExperimentsFromBucket(science, source);
            }
        }
    }

    public sealed class RoverCabinArchetype : CockpitArchetype
    {
        public override string Family => "0030-Rover";
        public override string DisplayName => "Rover cabin";
        public override string Description => "Crewed rover command body with cockpit-style command modules.";
        public override string DefaultSizeKey => PartSizeRegistry.Md;
    }

    public sealed class ExternalCommandSeatArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Pods";
        public override string Family => "0030-Rover";
        public override string DisplayName => "External command seat";
        public override string Description => "External crew seat with simple science and drag setup.";
        public override string DefaultSizeKey => PartSizeRegistry.Xs;
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_CrewedInterior),
            typeof(Module_ScienceExperiment),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public sealed class MethaneTankArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Fuel Tank";
        public override string Family => "0050-Methane";
        public override string DisplayName => "Methane tank";
        public override string Description => "Methane-only fuel tank.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
        public override void SeedDefaults(CorePartData part, BucketResolution bucket) =>
            SeedTankDefaults(part, bucket, "Methane", defaultCapacity: 400f);
    }

    public sealed class FuelLineArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Fuel Tank";
        public override string Family => "0090-Fuel Line";
        public override string DisplayName => "Fuel line";
        public override string Description => "Compound fuel transfer line.";
        public override string DefaultSizeKey => PartSizeRegistry.Xs;
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => NoNodes;
        public override IReadOnlyList<Type> DefaultModules => new[] { typeof(Module_FuelLine), typeof(Module_Color) };
    }

    public sealed class SolidBoosterArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Engines";
        public override string Family => "0110-Solid Fuel Booster";
        public override string DisplayName => "Solid fuel booster";
        public override string Description => "Solid rocket booster with built-in solid fuel storage.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Engine),
            typeof(Module_Gimbal),
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            if (part?.Data != null)
            {
                AddResourceContainer(part.Data, source, "SolidFuel", defaultCapacity: 100f,
                    capacityFieldName: StockFieldNames.TankCapacity + ".SolidFuel");
            }

            Data_Engine engine = FindModuleData<Data_Engine>(part);
            if (engine?.engineModes != null && engine.engineModes.Length > 0)
            {
                Data_Engine.EngineMode mode = engine.engineModes[0];
                TrySeedScalar(source, StockFieldNames.EngineMaxThrust + ".SolidFuel", v => mode.maxThrust = v);
                SeedEngineModeIsp(mode, source, "SolidFuel");
            }

            Data_Gimbal gimbal = FindModuleData<Data_Gimbal>(part);
            if (gimbal != null)
            {
                TrySeedScalar(source, StockFieldNames.EngineGimbalRange, v => gimbal.gimbalRange = v);
                TrySeedScalar(source, StockFieldNames.EngineGimbalResponseSpeed, v => gimbal.gimbalResponseSpeed = v);
            }
        }
    }

    public sealed class LaunchClampArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Structural";
        public override string Family => "0170-Clamp";
        public override string DisplayName => "Launch clamp";
        public override string Description => "Ground launch clamp with generator support.";
        public override string DefaultSizeKey => PartSizeRegistry.Md;
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => NoNodes;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_GroundLaunchClamp),
            typeof(Module_Generator),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            SeedAlternatorOutput(FindModuleData<Data_ModuleGenerator>(part), source);
        }
    }

    public sealed class EngineMountArchetype : ProceduralShellArchetypeBase
    {
        public override string Category => "Structural";
        public override string Family => "0180-Engine Mount";
        public override string DisplayName => "Engine mount";
        public override string Description => "Engine mount with fairing/cargo-bay occlusion modules.";
    }

    public sealed class StructuralEngineMountArchetype : StructuralArchetypeBase
    {
        public override string Family => "0180-Engine Mount";
        public override string DisplayName => "Structural engine mount";
    }

    public sealed class AdapterArchetype : StructuralArchetypeBase
    {
        public override string Family => "0190-Adapter";
        public override string DisplayName => "Adapter";
    }

    public sealed class BeamArchetype : StructuralArchetypeBase
    {
        public override string Family => "0200-Beam";
        public override string DisplayName => "Beam";
    }

    public sealed class StructuralBodyArchetype : StructuralArchetypeBase
    {
        public override string Family => "0210-Body";
        public override string DisplayName => "Structural body";
    }

    public sealed class PanelArchetype : StructuralArchetypeBase
    {
        public override string Family => "0220-Panel";
        public override string DisplayName => "Panel";
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
    }

    public sealed class HubArchetype : StructuralArchetypeBase
    {
        public override string Family => "0230-Hub";
        public override string DisplayName => "Hub";
    }

    public sealed class TrussArchetype : StructuralArchetypeBase
    {
        public override string Family => "0240-Truss";
        public override string DisplayName => "Truss";
    }

    public sealed class TrussAdapterArchetype : StructuralArchetypeBase
    {
        public override string Family => "0250-Truss Adapter";
        public override string DisplayName => "Truss adapter";
    }

    public sealed class TrussResizerArchetype : StructuralArchetypeBase
    {
        public override string Family => "0260-Truss Resizer";
        public override string DisplayName => "Truss resizer";
    }

    public sealed class ProceduralTubeArchetype : ProceduralShellArchetypeBase
    {
        public override string Category => "Structural";
        public override string Family => "0270-Tube";
        public override string DisplayName => "Procedural tube";
        public override string Description => "Procedural tube with fairing and cargo-bay occlusion modules.";
    }

    public sealed class StackSeparatorArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Coupling";
        public override string Family => "0290-Stack Separator";
        public override string DisplayName => "Stack separator";
        public override string Description => "Stack separator with ejection force and crossfeed toggle.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Decouple),
            typeof(Module_ToggleCrossfeed),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_Decouple decouple = FindModuleData<Data_Decouple>(part);
            if (decouple != null)
            {
                TrySeedScalar(source, StockFieldNames.DecoupleEjectionForce, v => decouple.ejectionForce = v);
            }
        }
    }

    public class DockingPortArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Coupling";
        public override string Family => "0310-Docking Port";
        public override string DisplayName => "Docking port";
        public override string Description => "Docking port with crossfeed toggle.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_DockingNode),
            typeof(Module_ToggleCrossfeed),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source) =>
            SeedDockingDefaults(part, source);
    }

    public sealed class ShieldedDockingPortArchetype : DockingPortArchetype
    {
        public override string DisplayName => "Shielded docking port";
        public override string Description => "Deployable shielded docking port with crossfeed toggle.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_DockingNode),
            typeof(Module_Deployable),
            typeof(Module_ToggleCrossfeed),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public sealed class FairingArchetype : ProceduralShellArchetypeBase
    {
        public override string Category => "Payload";
        public override string Family => "0320-Fairing";
        public override string DisplayName => "Fairing";
        public override string Description => "Procedural fairing with cargo-bay occlusion.";
    }

    public sealed class PayloadTrussArchetype : StructuralArchetypeBase
    {
        public override string Category => "Payload";
        public override string Family => "0350-Truss";
        public override string DisplayName => "Payload truss";
    }

    public sealed class NoseConeArchetype : StructuralArchetypeBase
    {
        public override string Category => "Aerodynamics";
        public override string Family => "0360-Nose Cone";
        public override string DisplayName => "Nose cone";
        public override string Description => "Aerodynamic nose cone with drag and color modules.";
    }

    public class ProceduralWingArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Aerodynamics";
        public override string Family => "0380-Wing";
        public override string DisplayName => "Procedural wing";
        public override string Description => "Procedural wing using control-surface and reinforced-connection modules.";
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ControlSurface),
            typeof(Module_ProceduralPart),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source) =>
            SeedControlSurfaceDefaults(part, source);
    }

    public sealed class ProceduralStabilizerArchetype : ProceduralWingArchetype
    {
        public override string Family => "0390-Stabilizer";
        public override string DisplayName => "Procedural stabilizer";
        public override string Description => "Procedural stabilizer using control-surface and reinforced-connection modules.";
    }

    public sealed class ProceduralControlSurfaceArchetype : ProceduralWingArchetype
    {
        public override string Family => "0400-Control Surface";
        public override string DisplayName => "Procedural control surface";
        public override string Description => "Procedural control surface with reinforced connection.";
    }

    public sealed class StructuralTailSectionArchetype : StructuralArchetypeBase
    {
        public override string Category => "Aerodynamics";
        public override string Family => "0410-Tail Section";
        public override string DisplayName => "Tail section";
        public override string Description => "Structural tail section with drag and color modules.";
    }

    public sealed class LandingLegArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Ground";
        public override string Family => "0420-Landing Leg";
        public override string DisplayName => "Landing leg";
        public override string Description => "Deployable landing leg with suspension and lock modules.";
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelSuspension),
            typeof(Module_WheelLock),
            typeof(Module_Deployable),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source) =>
            SeedWheelDefaults(part, source);
    }

    public class LandingGearArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Ground";
        public override string Family => "0430-Landing Gear";
        public override string DisplayName => "Landing gear";
        public override string Description => "Deployable landing gear with brakes, steering, lights, and suspension.";
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelBrakes),
            typeof(Module_WheelSteering),
            typeof(Module_WheelSuspension),
            typeof(Module_Deployable),
            typeof(Module_Light),
            typeof(Module_StatusLight),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source) =>
            SeedWheelDefaults(part, source);
    }

    public sealed class FixedLandingGearArchetype : LandingGearArchetype
    {
        public override string DisplayName => "Fixed landing gear";
        public override string Description => "Fixed landing gear with wheel, brakes, and suspension.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelBrakes),
            typeof(Module_WheelSuspension),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public class RoverWheelArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Ground";
        public override string Family => "0440-Wheel";
        public override string DisplayName => "Rover wheel";
        public override string Description => "Rover wheel with motor, steering, brakes, and suspension.";
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelMotor),
            typeof(Module_WheelSteering),
            typeof(Module_WheelBrakes),
            typeof(Module_WheelSuspension),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source) =>
            SeedWheelDefaults(part, source);
    }

    public sealed class MotorSteeringRoverWheelArchetype : RoverWheelArchetype
    {
        public override string DisplayName => "Motor-steering rover wheel";
        public override string Description => "Rover wheel using the combined motor-steering module.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelMotorSteering),
            typeof(Module_WheelBrakes),
            typeof(Module_WheelSuspension),
            typeof(Module_ReinforcedConnection),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public sealed class InflatableHeatShieldArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Thermal";
        public override string Family => "0450-Heat Shield";
        public override string DisplayName => "Inflatable heat shield";
        public override string Description => "Deployable inflatable heat shield shell.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Deployable),
            typeof(Module_Fairing),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public sealed class FuelCellArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Electrical";
        public override string Family => "0490-Generator";
        public override string DisplayName => "Fuel cell";
        public override string Description => "Resource converter that produces electric charge from stored resources.";
        public override string DefaultSizeKey => PartSizeRegistry.Xs;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceConverter),
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_ResourceConverter converter = FindModuleData<Data_ResourceConverter>(part);
            if (converter?.conversionRate != null)
            {
                TrySeedScalar(source, StockFieldNames.ConverterConversionRate, v => converter.conversionRate.SetValue(v));
            }
        }
    }

    public sealed class ReactionWheelArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Utility";
        public override string Family => "0530-Stabilizer";
        public override string DisplayName => "Reaction wheel";
        public override string Description => "Standalone attitude-control reaction wheel.";
        public override string DefaultSizeKey => PartSizeRegistry.Xs;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ReactionWheel),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_ReactionWheel wheel = FindModuleData<Data_ReactionWheel>(part);
            if (wheel == null)
            {
                return;
            }

            TrySeedScalar(source, StockFieldNames.ReactionWheelPitchTorque, v => wheel.PitchTorque = v);
            TrySeedScalar(source, StockFieldNames.ReactionWheelYawTorque, v => wheel.YawTorque = v);
            TrySeedScalar(source, StockFieldNames.ReactionWheelRollTorque, v => wheel.RollTorque = v);
            wheel.RequiredResources ??= new List<PartModuleResourceSetting>();
            TrySeedScalar(source, StockFieldNames.ReactionWheelEcRate, v => UpsertElectricChargeRate(wheel.RequiredResources, v));
        }
    }

    public sealed class LadderArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Utility";
        public override string Family => "0550-Ladder";
        public override string DisplayName => "Ladder";
        public override string Description => "Fixed or deployable ladder.";
        public override string DefaultSizeKey => PartSizeRegistry.Xs;
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => SurfaceNode;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Deployable),
            typeof(Module_Drag),
            typeof(Module_Color)
        };
    }

    public sealed class ScienceLabArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Science";
        public override string Family => "0560-Science Collector";
        public override string DisplayName => "Science lab";
        public override string Description => "Crewed science lab with interior and experiment modules.";
        public override string DefaultSizeKey => PartSizeRegistry.Md;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_CrewedInterior),
            typeof(Module_ScienceExperiment),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            if (part?.Data != null)
            {
                TrySeedScalarInt(source, StockFieldNames.CrewCapacity, v => part.Data.crewCapacity = v);
            }
            Data_ScienceExperiment science = FindModuleData<Data_ScienceExperiment>(part);
            if (science != null)
            {
                science.Experiments ??= new List<ExperimentConfiguration>();
                SeedScienceExperimentsFromBucket(science, source);
            }
        }
    }

    public sealed class ExperimentPartArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Science";
        public override string Family => "Experiment";
        public override string DisplayName => "Experiment part";
        public override string Description => "Legacy experiment-family science part.";
        public override string DefaultSizeKey => PartSizeRegistry.Sm;
        public override IReadOnlyList<Type> DefaultModules => new[] { typeof(Module_ScienceExperiment), typeof(Module_Color) };
    }

    public sealed class FactoryArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Utility";
        public override string Family => "Factory";
        public override string DisplayName => "Factory";
        public override string Description => "Resource-converter factory part.";
        public override string DefaultSizeKey => PartSizeRegistry.Md;
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceConverter),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_ResourceConverter converter = FindModuleData<Data_ResourceConverter>(part);
            if (converter?.conversionRate != null)
            {
                TrySeedScalar(source, StockFieldNames.ConverterConversionRate, v => converter.conversionRate.SetValue(v));
            }
        }
    }

    public sealed class ServiceBayArchetype : SimpleStockArchetypeBase
    {
        public override string Category => "Payload";
        public override string Family => "Service Bay";
        public override string DisplayName => "Service bay";
        public override string Description => "Service bay with cargo-bay occlusion.";
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_CargoBay),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        protected override void SeedModuleDefaults(CorePartData part, StockBucket source)
        {
            Data_CargoBay bay = FindModuleData<Data_CargoBay>(part);
            if (bay != null)
            {
                TrySeedScalar(source, StockFieldNames.CargoBayInternalLength, v => bay.BayInternalLength = v);
                TrySeedScalar(source, StockFieldNames.CargoBayLookUpRadius, v => bay.lookUpRadius = v);
            }
        }
    }
}
