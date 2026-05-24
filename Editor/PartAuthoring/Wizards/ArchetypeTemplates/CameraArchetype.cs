using System;
using System.Collections.Generic;
using KSP;
using KSP.Modules;
using KSP.OAB;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Redux.Modules;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Wizards.ArchetypeTemplates
{
    /// <summary>Camera part. Uses Redux Module_Camera.</summary>
    public sealed class CameraArchetype : PartArchetypeBase
    {
        public override string Category => "Utility";
        public override string Family => "Camera";
        public override string DisplayName => "Camera";
        public override string Description => "Mountable camera with adjustable field of view.";
        public override MetaAssemblySizeFilterType DefaultSize => MetaAssemblySizeFilterType.XS;

        public override IReadOnlyList<Type> DefaultModules => new[]
        {
            typeof(Module_Camera),
            typeof(Module_Drag),
            typeof(Module_Color)
        };

        public override IReadOnlyList<AttachNodeTemplate> DefaultAttachNodes => new[]
        {
            new AttachNodeTemplate("surface", new Vector3(0f, 0f, 0.1f), Vector3.forward, MetaAssemblySizeFilterType.XS)
        };

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
        }
    }
}
