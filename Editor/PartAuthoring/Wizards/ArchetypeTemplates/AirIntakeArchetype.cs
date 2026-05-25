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
    /// <summary>Standalone air intake for atmospheric engines.</summary>
    public sealed class AirIntakeArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Aerodynamics";
        /// <inheritdoc />
        public override string Family => "0370-Intake";
        /// <inheritdoc />
        public override string DisplayName => "Air intake";
        /// <inheritdoc />
        public override string Description => "Provides intake air for jet engines.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ResourceIntake),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, MetaAssemblySizeFilterType.XS)
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

            Data_ResourceIntake intake = FindModuleData<Data_ResourceIntake>(part);
            if (intake != null)
            {
                TrySeedScalar(source, StockFieldNames.IntakeArea, v => intake.area = v);
                TrySeedScalar(source, StockFieldNames.IntakeSpeed, v => intake.intakeSpeed = v);
            }
        }
    }
}
