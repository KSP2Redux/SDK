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
    /// <summary>RCS thruster block for fine attitude control.</summary>
    public sealed class RCSThrusterBlockArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Utility";
        /// <inheritdoc />
        public override string Family => "0520-RCS";
        /// <inheritdoc />
        public override string DisplayName => "RCS thruster block";
        /// <inheritdoc />
        public override string Description => "Reaction control thruster running on monopropellant.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Xs;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_RCS),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, PartSizeRegistry.Xs)
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

            // RCS bake keys use lowercase-p "Monopropellant" (stock data inconsistency vs engines).
            Data_RCS rcs = FindModuleData<Data_RCS>(part);
            if (rcs != null)
            {
                TrySeedScalar(source, StockFieldNames.RcsThrust + ".Monopropellant", v => rcs.maxThrust = v);
            }
        }
    }
}
