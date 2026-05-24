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
    /// <summary>Monopropellant fuel tank - Stratus / Oscar series.</summary>
    public sealed class MonopropTankArchetype : PartArchetypeBase
    {
        public override string Category => "Fuel Tank";
        public override string Family => "0060-Monopropellant";
        public override string DisplayName => "Monoprop tank";
        public override string Description => "Monopropellant tank.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.XS),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down, MetaAssemblySizeFilterType.XS)
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

            AddResourceContainer(data, source, "MonoPropellant", defaultCapacity: 100f,
                capacityFieldName: StockFieldNames.TankCapacity + ".MonoPropellant");
        }
    }
}
