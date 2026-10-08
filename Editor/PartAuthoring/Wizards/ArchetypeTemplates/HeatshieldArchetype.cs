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
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Heatshield),
            typeof(Module_ResourceCapacities),
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

            PartData data = part.Data;
            data.angularDrag = 2f;
            data.coLiftOffset = new Vector3(0f, -0.255f, 0f);
            data.coPressureOffset = new Vector3(0f, 1.05f, 0f);
            data.crashTolerance = 20f;
            data.maxTemp = 1700f;
            TrySeedScalar(source, StockFieldNames.Mass, v => data.mass = v);
            TrySeedScalarInt(source, StockFieldNames.Cost, v => data.cost = v);

            Data_Heatshield shield = FindModuleData<Data_Heatshield>(part);
            if (shield != null)
            {
                shield.requiredResources ??= new List<PartModuleResourceSetting>();
                shield.requiredResources.Clear();
                shield.requiredResources.Add(new PartModuleResourceSetting
                {
                    Rate = 0.5f,
                    ResourceName = "Ablator",
                    AcceptanceThreshold = 0.001
                });
                shield.AblationTempThreshold = 1000.0;
                shield.AblationMaximumOverThreshold = 50.0;
                shield.PyrolysisLossFactor = 0.00000016;
                shield.ShieldingScale = 1f;
                shield.ShieldingDirection = Vector3.down;
                shield.DisabledWhenRetracted = true;
                shield.UseChar = true;
                shield.CharMaterialName = data.partName;
                shield.CharMin = 0f;
                shield.CharMax = 1f;
                shield.AblatorMaxValue = 1.0;

                TrySeedScalar(source, StockFieldNames.HeatshieldAblationTempThreshold, v => shield.AblationTempThreshold = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldAblationMaxOverThreshold, v => shield.AblationMaximumOverThreshold = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldPyrolysisLossFactor, v => shield.PyrolysisLossFactor = v);
                TrySeedScalar(source, StockFieldNames.HeatshieldShieldingScale, v => shield.ShieldingScale = v);
            }

            // Heat shields carry an Ablator resource container; seed capacity from bake if present.
            AddResourceContainer(data, source, "Ablator", defaultCapacity: 0.2f,
                capacityFieldName: StockFieldNames.TankCapacity + ".Ablator");
            for (int i = 0; i < data.resourceContainers.Count; i++)
            {
                var resource = data.resourceContainers[i];
                if (resource?.name == "Ablator")
                {
                    resource.initialUnits = resource.capacityUnits;
                    break;
                }
            }
        }
    }
}
