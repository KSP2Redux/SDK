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
    /// <summary>Stack decoupler - TR-18A / TR-2C etc.</summary>
    public sealed class InlineDecouplerArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Coupling";
        /// <inheritdoc />
        public override string Family => "0280-Stack Decoupler";
        /// <inheritdoc />
        public override string DisplayName => "Inline decoupler";
        /// <inheritdoc />
        public override string Description => "Stack-mounted decoupler with ejection force.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Decouple),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.S),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down, MetaAssemblySizeFilterType.S)
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

            Data_Decouple decouple = FindModuleData<Data_Decouple>(part);
            if (decouple != null)
            {
                TrySeedScalar(source, StockFieldNames.DecoupleEjectionForce, v => decouple.ejectionForce = v);
            }
        }
    }
}
