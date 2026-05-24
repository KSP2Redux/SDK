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
    /// <summary>Radial decoupler for separating side-mounted boosters and payloads.</summary>
    public sealed class RadialDecouplerArchetype : PartArchetypeBase
    {
        public override string Category => "Coupling";
        public override string Family => "0300-Radial Decoupler";
        public override string DisplayName => "Radial decoupler";
        public override string Description => "Surface-mounted decoupler with ejection force.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Decouple),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, MetaAssemblySizeFilterType.S)
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

            Data_Decouple decouple = FindModuleData<Data_Decouple>(part);
            if (decouple != null)
            {
                TrySeedScalar(source, StockFieldNames.DecoupleEjectionForce, v => decouple.ejectionForce = v);
            }
        }
    }
}
