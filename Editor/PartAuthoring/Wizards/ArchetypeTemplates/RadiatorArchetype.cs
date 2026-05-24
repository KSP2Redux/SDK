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
    /// <summary>Active radiator for heat rejection.</summary>
    public sealed class RadiatorArchetype : PartArchetypeBase
    {
        public override string Category => "Thermal";
        public override string Family => "0460-Radiator";
        public override string DisplayName => "Radiator";
        public override string Description => "Deployable radiator for active heat rejection.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ActiveRadiator),
            typeof(Module_Deployable),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, MetaAssemblySizeFilterType.XS)
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

            Data_ActiveRadiator rad = FindModuleData<Data_ActiveRadiator>(part);
            if (rad != null)
            {
                TrySeedScalar(source, StockFieldNames.RadiatorFluxPerAreaUnit, v => rad.ProceduralRadiatorFluxPerAreaUnit = v);
            }
        }
    }
}
