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
    /// <summary>Landing wheel with brakes, steering, and suspension.</summary>
    public sealed class LandingWheelArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Ground";
        /// <inheritdoc />
        public override string Family => "0440-Wheel";
        /// <inheritdoc />
        public override string DisplayName => "Landing wheel";
        /// <inheritdoc />
        public override string Description => "Steerable landing wheel with brakes and suspension.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_WheelBase),
            typeof(Module_WheelBrakes),
            typeof(Module_WheelSteering),
            typeof(Module_WheelSuspension),
            typeof(Module_Deployable),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, PartSizeRegistry.Xs)
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

            Data_WheelBrakes brakes = FindModuleData<Data_WheelBrakes>(part);
            if (brakes != null)
            {
                TrySeedScalar(source, StockFieldNames.WheelMaxBrakeTorque, v => brakes.MaxBrakeTorque = v);
            }

            Data_WheelSuspension suspension = FindModuleData<Data_WheelSuspension>(part);
            if (suspension != null)
            {
                TrySeedScalar(source, StockFieldNames.WheelSuspensionDistance, v => suspension.suspensionDistance = v);
            }
        }
    }
}
