using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>
    /// Generic basic part - Drag and Color modules only. Author picks category and family
    /// in the Identity step.
    /// </summary>
    public sealed class GenericArchetype : PartArchetypeBase
    {
        public override string Category => "Empty";
        public override string Family => string.Empty;
        public override string DisplayName => "Generic";
        public override string Description => "Basic part with drag and color modules. Pick your own category and family.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
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
