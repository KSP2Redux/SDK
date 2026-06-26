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
    /// <summary>Hydrogen fuel tank for hydrogen-burning engines.</summary>
    public sealed class HydrogenTankArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Fuel Tank";
        /// <inheritdoc />
        public override string Family => "0080-Hydrogen";
        /// <inheritdoc />
        public override string DisplayName => "Hydrogen tank";
        /// <inheritdoc />
        public override string Description => "Hydrogen fuel tank.";
        /// <inheritdoc />
        public override string DefaultSizeKey => PartSizeRegistry.Sm;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => new[]
        {
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
        public override void SeedDefaults(CorePartData part, BucketResolution bucket) =>
            SeedTankDefaults(part, bucket, "Hydrogen", defaultCapacity: 400f);
    }
}
