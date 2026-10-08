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
    /// <summary>Aerodynamic control surface for atmospheric flight - elevons, rudders.</summary>
    public sealed class ControlSurfaceArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Aerodynamics";
        /// <inheritdoc />
        public override string Family => "0400-Control Surface";
        /// <inheritdoc />
        public override string DisplayName => "Control surface";
        /// <inheritdoc />
        public override string Description => "Hinged aerodynamic surface driven by command inputs.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_ControlSurface),
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

            Data_ControlSurface cs = FindModuleData<Data_ControlSurface>(part);
            if (cs != null)
            {
                TrySeedScalar(source, StockFieldNames.ControlSurfaceRange, v => cs.CtrlSurfaceRange = v);
                TrySeedScalar(source, StockFieldNames.ControlSurfaceArea, v => cs.CtrlSurfaceArea = v);
                TrySeedScalar(source, StockFieldNames.ControlSurfaceActuatorSpeed, v => cs.ActuatorSpeedNormalScale = v);
            }
        }
    }
}
