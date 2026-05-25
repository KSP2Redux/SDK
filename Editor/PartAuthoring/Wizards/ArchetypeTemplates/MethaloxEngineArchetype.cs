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
    /// <summary>Liquid methalox engine - Reliant, Swivel, Terrier, Vector, Mainsail, etc.</summary>
    public sealed class MethaloxEngineArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Engines";
        /// <inheritdoc />
        public override string Family => "0100-Methalox";
        /// <inheritdoc />
        public override string DisplayName => "Methalox engine";
        /// <inheritdoc />
        public override string Description => "Liquid-fuel engine burning methane and oxidizer.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Engine),
            typeof(Module_Gimbal),
            typeof(Module_Generator),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.S)
        };

        /// <inheritdoc />
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
                TrySeedScalar(source, StockFieldNames.EngineMaxThrust + ".Methalox", v => mode.maxThrust = v);
                SeedEngineModeIsp(mode, source, "Methalox");
            }

            Data_Gimbal gimbal = FindModuleData<Data_Gimbal>(part);
            if (gimbal != null)
            {
                TrySeedScalar(source, StockFieldNames.EngineGimbalRange, v => gimbal.gimbalRange = v);
                TrySeedScalar(source, StockFieldNames.EngineGimbalResponseSpeed, v => gimbal.gimbalResponseSpeed = v);
            }

            Data_ModuleGenerator alt = FindModuleData<Data_ModuleGenerator>(part);
            SeedAlternatorOutput(alt, source);
        }
    }
}
