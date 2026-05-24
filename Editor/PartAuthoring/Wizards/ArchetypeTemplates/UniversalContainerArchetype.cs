using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Redux.Modules;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>Universal resource container - holds any single resource selected at flight time.</summary>
    public sealed class UniversalContainerArchetype : PartArchetypeBase
    {
        public override string Category => "Utility";
        public override string Family => "UniversalContainer";
        public override string DisplayName => "Universal container";
        public override string Description => "Generic container that can store any single resource.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_UniversalContainer),
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => Array.Empty<AttachNodeTemplate>();

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
        }
    }
}
