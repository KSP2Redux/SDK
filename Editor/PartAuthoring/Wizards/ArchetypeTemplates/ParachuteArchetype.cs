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
    /// <summary>Atmospheric descent parachute.</summary>
    public sealed class ParachuteArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Utility";
        /// <inheritdoc />
        public override string Family => "0510-Parachute";
        /// <inheritdoc />
        public override string DisplayName => "Parachute";
        /// <inheritdoc />
        public override string Description => "Deployable parachute for atmospheric descent.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Parachute),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, PartSizeRegistry.Sm)
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

            Data_Parachute chute = FindModuleData<Data_Parachute>(part);
            if (chute != null)
            {
                TrySeedScalar(source, StockFieldNames.ParachuteDeployAltitude, v => chute.defaultDeployAltitude = v);
                TrySeedScalar(source, StockFieldNames.ParachuteMinPressureToOpen, v => chute.defaultMinAirPressureToOpen = v);
                TrySeedScalar(source, StockFieldNames.ParachuteAreaDeployed, v => chute.areaDeployed = v);
                TrySeedScalar(source, StockFieldNames.ParachuteMaxTemp, v => chute.chuteMaxTemp = v);
            }
        }
    }
}
