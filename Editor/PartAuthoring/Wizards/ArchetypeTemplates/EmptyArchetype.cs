using System;
using System.Collections.Generic;
using KSP;
using KSP.OAB;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>
    /// Bare-bones archetype - creates a CorePartData with no modules, no attach nodes,
    /// and no transforms. The fallback for parts that do not fit any other archetype.
    /// </summary>
    public sealed class EmptyArchetype : PartArchetypeBase
    {
        /// <inheritdoc />
        public override string Category => "Empty";
        /// <inheritdoc />
        public override string Family => string.Empty;
        /// <inheritdoc />
        public override string DisplayName => "Empty";
        /// <inheritdoc />
        public override string Description => "Bare prefab. No modules, no attach nodes, no transforms.";
        /// <inheritdoc />
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        /// <inheritdoc />
        public override IReadOnlyList<Type> DefaultModules => Array.Empty<Type>();
        /// <inheritdoc />
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => Array.Empty<AttachNodeTemplate>();

        /// <inheritdoc />
        public override void SeedDefaults(CorePartData part, BucketResolution bucket)
        {
            // Empty archetype carries no defaults.
        }
    }
}
