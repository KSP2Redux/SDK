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
    /// <summary>Monopropellant fuel tank - Stratus / Oscar series.</summary>
    public sealed class MonopropTankArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Fuel Tank";
        /// <inheritdoc />
        public override string Family => "0060-Monopropellant";
        /// <inheritdoc />
        public override string DisplayName => "Monoprop tank";
        /// <inheritdoc />
        public override string Description => "Monopropellant tank.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

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
            new AttachNodeTemplate("top", new Vector3(0f, 0.5f, 0f), Vector3.up, MetaAssemblySizeFilterType.XS),
            new AttachNodeTemplate("bottom", new Vector3(0f, -0.5f, 0f), Vector3.down, MetaAssemblySizeFilterType.XS)
        };

        /// <inheritdoc />
        public override void SeedDefaults(CorePartData part, BucketResolution bucket) =>
            SeedTankDefaults(part, bucket, "MonoPropellant", defaultCapacity: 100f);
    }
}
