using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>Strut connector - structural reinforcement between two points.</summary>
    public sealed class StrutConnectorArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Structural";
        /// <inheritdoc />
        public override string Family => "0160-Strut";
        /// <inheritdoc />
        public override string DisplayName => "Strut connector";
        /// <inheritdoc />
        public override string Description => "Two-point strut for structural reinforcement.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Strut),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => Array.Empty<AttachNodeTemplate>();

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
            TrySeedScalar(source, StockFieldNames.BreakingForce, v => data.breakingForce = v);
            TrySeedScalar(source, StockFieldNames.BreakingTorque, v => data.breakingTorque = v);
            TrySeedScalar(source, StockFieldNames.MaxTemp, v => data.maxTemp = v);
        }
    }
}
