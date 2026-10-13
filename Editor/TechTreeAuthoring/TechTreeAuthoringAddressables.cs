using System.Linq;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Resolves the Addressables group a tech tree bakes into.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>MissionAuthoringAddressables</c>. A tree inside a mod uses the mod's tech node group. Anywhere else,
    /// such as Redux itself, it uses the project's <see cref="TECH_NODES_GROUP_NAME" /> group.
    /// </remarks>
    internal static class TechTreeAuthoringAddressables
    {
        /// <summary>
        /// The name of the project-level tech node group.
        /// </summary>
        public const string TECH_NODES_GROUP_NAME = "Tech Nodes";

        /// <summary>
        /// Resolves the group for a tech tree.
        /// </summary>
        /// <param name="tree">The tree whose group to resolve.</param>
        /// <returns>The group, or null when neither a mod group nor a project group exists.</returns>
        public static AddressableAssetGroup ResolveGroup(TechTreeAsset tree)
        {
            Mod mod = KSP2UnityTools.FindParentMod(tree);
            if (mod != null && mod.techNodeDataGroup != null)
                return mod.techNodeDataGroup;

            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings != null ? settings.groups.FirstOrDefault(group => group != null && group.Name == TECH_NODES_GROUP_NAME) : null;
        }
    }
}
