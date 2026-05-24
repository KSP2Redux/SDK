using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>Air-breathing jet engine - Whiplash and other turboramjets.</summary>
    public sealed class JetEngineArchetype : PartArchetypeBase
    {
        public override string Category => "Engines";
        public override string Family => "0120-Jet Engine";
        public override string DisplayName => "Jet engine";
        public override string Description => "Atmospheric jet engine running on intake air.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Engine),
            typeof(Module_Gimbal),
            typeof(Module_ResourceIntake),
            typeof(Module_Generator),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.S)
        };

        public override void SeedDefaults(CorePartData part, BucketResolution bucket)
        {
            if (part?.Data == null)
            {
                return;
            }
            StockBucket source = FindFirstUsableBucket(bucket);
            if (source == null)
            {
                return;
            }

            PartData data = part.Data;
            TrySeedScalar(source, StockFieldNames.Mass, v => data.mass = v);
            TrySeedScalarInt(source, StockFieldNames.Cost, v => data.cost = v);
            TrySeedScalar(source, StockFieldNames.CrashTolerance, v => data.crashTolerance = v);
            TrySeedScalar(source, StockFieldNames.MaxTemp, v => data.maxTemp = v);

            Data_Engine engine = FindModuleData<Data_Engine>(part);
            if (engine?.engineModes != null && engine.engineModes.Length > 0)
            {
                Data_Engine.EngineMode mode = engine.engineModes[0];
                TrySeedScalar(source, StockFieldNames.EngineMaxThrust + ".MethaneAir", v => mode.maxThrust = v);
                SeedEngineModeIsp(mode, source, "MethaneAir");
            }

            Data_Gimbal gimbal = FindModuleData<Data_Gimbal>(part);
            if (gimbal != null)
            {
                TrySeedScalar(source, StockFieldNames.EngineGimbalRange, v => gimbal.gimbalRange = v);
                TrySeedScalar(source, StockFieldNames.EngineGimbalResponseSpeed, v => gimbal.gimbalResponseSpeed = v);
            }

            Data_ResourceIntake intake = FindModuleData<Data_ResourceIntake>(part);
            if (intake != null)
            {
                TrySeedScalar(source, StockFieldNames.IntakeArea, v => intake.area = v);
                TrySeedScalar(source, StockFieldNames.IntakeSpeed, v => intake.intakeSpeed = v);
            }

            Data_ModuleGenerator alt = FindModuleData<Data_ModuleGenerator>(part);
            SeedAlternatorOutput(alt, source);
        }
    }
}
