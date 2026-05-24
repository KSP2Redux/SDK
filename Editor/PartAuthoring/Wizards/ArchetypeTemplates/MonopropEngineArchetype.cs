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
    /// <summary>Monopropellant engine - Spider, Ant, Spark.</summary>
    public sealed class MonopropEngineArchetype : PartArchetypeBase
    {
        public override string Category => "Engines";
        public override string Family => "0130-Monopropellant";
        public override string DisplayName => "Monoprop engine";
        public override string Description => "Small thruster running on monopropellant.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Engine),
            typeof(Module_Generator),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.XS)
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
                TrySeedScalar(source, StockFieldNames.EngineMaxThrust + ".MonoPropellant", v => mode.maxThrust = v);
                SeedEngineModeIsp(mode, source, "MonoPropellant");
            }

            Data_ModuleGenerator alt = FindModuleData<Data_ModuleGenerator>(part);
            SeedAlternatorOutput(alt, source);
        }
    }
}
