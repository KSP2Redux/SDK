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
    /// <summary>Battery for electric charge storage.</summary>
    public sealed class BatteryArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Electrical";
        /// <inheritdoc />
        public override string Family => "0470-Battery";
        /// <inheritdoc />
        public override string DisplayName => "Battery";
        /// <inheritdoc />
        public override string Description => "Stores electric charge.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Xs;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceCapacities),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, PartSizeRegistry.Xs),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down, PartSizeRegistry.Xs)
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

            AddResourceContainer(data, source, "ElectricCharge", defaultCapacity: 100f,
                capacityFieldName: StockFieldNames.TankCapacity + ".ElectricCharge");
        }
    }
}
