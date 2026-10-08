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
    /// <summary>Cargo bay - storage with deployable doors.</summary>
    public sealed class CargoBayArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Utility";
        /// <inheritdoc />
        public override string Family => "0330-Cargo Bay";
        /// <inheritdoc />
        public override string DisplayName => "Cargo bay";
        /// <inheritdoc />
        public override string Description => "Internal storage with deployable doors.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_CargoBay),
            typeof(Module_Deployable),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, PartSizeRegistry.Sm),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down, PartSizeRegistry.Sm)
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

            Data_CargoBay bay = FindModuleData<Data_CargoBay>(part);
            if (bay != null)
            {
                TrySeedScalar(source, StockFieldNames.CargoBayInternalLength, v => bay.BayInternalLength = v);
                TrySeedScalar(source, StockFieldNames.CargoBayLookUpRadius, v => bay.lookUpRadius = v);
            }
        }
    }
}
