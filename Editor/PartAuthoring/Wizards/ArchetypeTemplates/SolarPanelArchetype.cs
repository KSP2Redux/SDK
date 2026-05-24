using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>Solar panel - deployable EC generator.</summary>
    public sealed class SolarPanelArchetype : PartArchetypeBase
    {
        public override string Category => "Electrical";
        public override string Family => "0480-Solar Array";
        public override string DisplayName => "Solar panel";
        public override string Description => "Deployable solar array that generates electric charge.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_SolarPanel),
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

            // No solar.rate bake field yet; peak output is derived. Author tunes Rate in inspector.
        }
    }
}
