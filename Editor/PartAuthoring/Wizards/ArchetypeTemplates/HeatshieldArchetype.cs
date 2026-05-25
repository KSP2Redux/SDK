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
    /// <summary>Ablative heat shield for atmospheric reentry.</summary>
    public sealed class HeatshieldArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Thermal";
        /// <inheritdoc />
        public override string Family => "0450-Heat Shield";
        /// <inheritdoc />
        public override string DisplayName => "Heat shield";
        /// <inheritdoc />
        public override string Description => "Ablative shield that protects against reentry heat.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Heatshield),
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

            Data_Heatshield shield = FindModuleData<Data_Heatshield>(part);
            if (shield != null)
            {
                TrySeedScalar(source, StockFieldNames.HeatshieldAblationTempThreshold, v => shield.AblationTempThreshold = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldAblationMaxOverThreshold, v => shield.AblationMaximumOverThreshold = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldPyrolysisLossFactor, v => shield.PyrolysisLossFactor = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldShieldingScale, v => shield.ShieldingScale = v);
            }

            // Heat shields carry an Ablator resource container; seed capacity from bake if present.
            AddResourceContainer(data, source, "Ablator", defaultCapacity: 200f,
                capacityFieldName: StockFieldNames.TankCapacity + ".Ablator");
        }
    }
}
