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
    /// <summary>Standalone EC generator - RTG or fuel cell.</summary>
    public sealed class GeneratorArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Electrical";
        /// <inheritdoc />
        public override string Family => "0490-Generator";
        /// <inheritdoc />
        public override string DisplayName => "Generator";
        /// <inheritdoc />
        public override string Description => "Stand-alone electric charge producer.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Xs;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Generator),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, PartSizeRegistry.Xs)
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

            Data_ModuleGenerator gen = FindModuleData<Data_ModuleGenerator>(part);
            SeedAlternatorOutput(gen, source);
        }
    }
}
