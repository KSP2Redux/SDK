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
        public override string Category => "Empty";
        public override string Family => string.Empty;
        public override string DisplayName => "Empty";
        public override string Description => "Bare prefab. No modules, no attach nodes, no transforms.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.S;

        public override IReadOnlyList<Type> DefaultModules => Array.Empty<Type>();
        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => Array.Empty<AttachNodeTemplate>();

        public override void SeedDefaults(CorePartData part, BucketResolution bucket)
        {
            // Empty archetype carries no defaults.
        }
    }
}
