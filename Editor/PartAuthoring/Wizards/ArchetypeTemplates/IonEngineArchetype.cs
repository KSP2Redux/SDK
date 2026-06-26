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
    /// <summary>Xenon-fed ion engine - Dawn.</summary>
    public sealed class IonEngineArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Engines";
        /// <inheritdoc />
        public override string Family => "0140-Xenon";
        /// <inheritdoc />
        public override string DisplayName => "Ion engine";
        /// <inheritdoc />
        public override string Description => "Electric ion thruster running on xenon.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Xs;

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
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, PartSizeRegistry.Xs)
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
                TrySeedScalar(source, StockFieldNames.EngineMaxThrust + ".XenonEC", v => mode.maxThrust = v);
                SeedEngineModeIsp(mode, source, "XenonEC");
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
