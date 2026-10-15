using Ksp2UnityTools.Editor.Modding;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The Patch Manager patch of a <see cref="TechTreePatchAsset" /> in a mod, written into the mod when it builds.
    /// </summary>
    /// <remarks>
    /// A sidecar, as a body's orbit is, since the patch asset is a tech tree asset and cannot also be a generator. The
    /// bake makes it beside the patch. In a project without a mod the bake writes the patch as a Lua file instead.
    /// </remarks>
    public class TechTreePatchScript : TextAssetGenerator
    {
        /// <summary>
        /// The patch this writes.
        /// </summary>
        public TechTreePatchAsset Patch;

        /// <summary>
        /// The hash of the Lua last exported for the patch, which tells an author's edits to the file apart from what
        /// the bake wrote, or null when the patch is not exported.
        /// </summary>
        [HideInInspector] public string ExportedHash;

        /// <inheritdoc />
        public override bool ShouldGenerate => Patch != null && Patch.ShouldBake;

        /// <inheritdoc />
        public override string PathInMod => $"patches/{TechTreePatchBake.GetPatchFileName(Patch)}";

        /// <inheritdoc />
        public override string Generate() => TechTreePatchBake.BuildLua(Patch, TechTreeBake.GetOwnerId(Patch));
    }
}
