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
    /// <summary>Uncrewed probe core - Stayputnik, OKTO, RoveMate, etc.</summary>
    public sealed class ProbeCoreArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Pods";
        /// <inheritdoc />
        public override string Family => "0010-Probe";
        /// <inheritdoc />
        public override string DisplayName => "Probe core";
        /// <inheritdoc />
        public override string Description => "Uncrewed core with reaction wheel, EC, and built-in transmitter.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Xs;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Command),
            typeof(Module_ReactionWheel),
            typeof(Module_ResourceCapacities),
            typeof(Module_DataTransmitter),
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

            Data_ReactionWheel wheel = FindModuleData<Data_ReactionWheel>(part);
            if (wheel != null)
            {
                TrySeedScalar(source, StockFieldNames.ReactionWheelPitchTorque, v => wheel.PitchTorque = v);
                TrySeedScalar(source, StockFieldNames.ReactionWheelYawTorque, v => wheel.YawTorque = v);
                TrySeedScalar(source, StockFieldNames.ReactionWheelRollTorque, v => wheel.RollTorque = v);
                wheel.RequiredResources ??= new List<PartModuleResourceSetting>();
                TrySeedScalar(source, StockFieldNames.ReactionWheelEcRate,
                    v => UpsertElectricChargeRate(wheel.RequiredResources, v));
            }

            Data_Command cmd = FindModuleData<Data_Command>(part);
            if (cmd != null)
            {
                cmd.requiredResources ??= new List<PartModuleResourceSetting>();
                TrySeedScalar(source, StockFieldNames.CommandEcRate,
                    v => UpsertElectricChargeRate(cmd.requiredResources, v));
            }
        }
    }
}
