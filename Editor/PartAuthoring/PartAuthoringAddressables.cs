using System.Linq;
using KSP;
using Ksp2UnityTools.Editor.API;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    /// <summary>
    /// Constant names and resolution helpers for part-authoring addressables groups.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PlanetAuthoringAddressables</c>. The project-level <see cref="PartsGroupName" />
    /// group is the fallback when the asset is not owned by a <see cref="Modding.Mod" /> with its
    /// own per-mod parts group.
    /// </remarks>
    internal static class PartAuthoringAddressables
    {
        /// <summary>Name of the project-level parts addressables group.</summary>
        public const string PartsGroupName = "Parts Data";

        /// <summary>Name of the project-level part icon addressables group.</summary>
        public const string PartIconsGroupName = "Parts Icons";

        /// <summary>
        /// Returns the canonical addressables key for a part prefab.
        /// </summary>
        /// <remarks>
        /// KSP2 loads part prefab assets by their asset-style key, including the prefab extension.
        /// The JSON sidecar uses <c>{partName}.json</c>, so the prefab side mirrors that convention
        /// as <c>{partName}.prefab</c>.
        /// </remarks>
        public static string GetPrefabAddress(string partName)
        {
            return string.IsNullOrWhiteSpace(partName) ? "part.prefab" : $"{partName}.prefab";
        }

        /// <summary>
        /// Resolves the addressables group for a part-related asset.
        /// </summary>
        /// <remarks>
        /// Prefers the owning mod's per-mod parts group, falls back to the project-level
        /// <see cref="PartsGroupName" /> group, returns null if neither exists.
        /// </remarks>
        /// <param name="target">The part whose addressables group is being resolved.</param>
        /// <returns>The resolved group, or null if no per-mod and no project-level group is available.</returns>
        public static AddressableAssetGroup ResolveGroup(CorePartData target)
        {
            if (KSP2UnityTools.FindParentMod(target) is { } mod && mod.partsGroup != null)
            {
                return mod.partsGroup;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings?.groups.FirstOrDefault(g => g != null && g.Name == PartsGroupName);
        }

        /// <summary>
        /// Resolves the addressables group for a part icon asset.
        /// </summary>
        /// <remarks>
        /// Mod-authored icons travel with the mod's parts group. Project-authored icons use the
        /// dedicated stock-style <see cref="PartIconsGroupName" /> group.
        /// </remarks>
        /// <param name="target">The part whose icon addressables group is being resolved.</param>
        /// <returns>The resolved group, or null if no per-mod and no project-level icon group is available.</returns>
        public static AddressableAssetGroup ResolveIconGroup(CorePartData target)
        {
            if (KSP2UnityTools.FindParentMod(target) is { } mod && mod.partsGroup != null)
            {
                return mod.partsGroup;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings?.groups.FirstOrDefault(g => g != null && g.Name == PartIconsGroupName);
        }
    }
}
