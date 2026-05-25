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
    /// <summary>Aerodynamic wing surface for atmospheric flight.</summary>
    public sealed class WingSurfaceArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Aerodynamics";
        /// <inheritdoc />
        public override string Family => "0380-Wing";
        /// <inheritdoc />
        public override string DisplayName => "Wing surface";
        /// <inheritdoc />
        public override string Description => "Fixed wing that generates lift in atmosphere.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_LiftingSurface),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, MetaAssemblySizeFilterType.S)
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

            Data_LiftingSurface lift = FindModuleData<Data_LiftingSurface>(part);
            if (lift != null)
            {
                TrySeedScalar(source, StockFieldNames.LiftSurfaceDeflectionCoeff, v => lift.deflectionLiftCoeff = v);
            }
        }
    }
}
