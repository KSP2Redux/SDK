using System.Linq;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.IO;
using Ksp2UnityTools.Editor.Modding;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

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

        /// <summary>Label the game loads part JSON by.</summary>
        public const string PartsDataLabel = "parts_data";

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
        /// Returns the canonical addressables key for a part's JSON sidecar.
        /// </summary>
        /// <param name="partName">The part's name.</param>
        /// <returns>The address <c>{partName}.json</c>.</returns>
        public static string GetJsonAddress(string partName) => $"{partName}.json";

        /// <summary>
        /// Returns the canonical addressables key for a part's baked icon.
        /// </summary>
        /// <param name="partName">The part's name.</param>
        /// <returns>The address <c>{partName}_icon.png</c>.</returns>
        public static string GetIconAddress(string partName) => $"{partName}_icon.png";

        /// <summary>
        /// Finds the mod that owns a part.
        /// </summary>
        /// <remarks>
        /// Looks the mod up through the prefab asset rather than the component. In Prefab Mode the
        /// component lives in the stage's scene, which has no asset path, so a lookup through it
        /// finds no mod.
        /// </remarks>
        /// <param name="target">The part whose mod is wanted.</param>
        /// <returns>The owning mod, or null if the part is not inside one.</returns>
        public static Mod FindOwningMod(CorePartData target)
        {
            if (target == null)
                return null;
            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            return string.IsNullOrEmpty(prefabPath)
                ? null
                : KSP2UnityTools.FindParentMod(AssetDatabase.LoadMainAssetAtPath(prefabPath));
        }

        /// <summary>
        /// Checks whether a group ships in the build that carries a part.
        /// </summary>
        /// <param name="group">The group holding one of the part's assets.</param>
        /// <param name="owner">The mod that owns the part, or null for a part outside any mod.</param>
        /// <returns>True if the group's contents reach the build, false otherwise.</returns>
        public static bool IsShippedGroup(AddressableAssetGroup group, Mod owner)
        {
            // A mod build switches off every group the mod does not own
            if (owner != null)
                return owner.AllGroups.Contains(group);
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            return schema != null && schema.IncludeInBuild;
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
            if (FindOwningMod(target) is { } mod && mod.partsGroup != null)
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
            if (FindOwningMod(target) is { } mod && mod.partsGroup != null)
            {
                return mod.partsGroup;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings?.groups.FirstOrDefault(g => g != null && g.Name == PartIconsGroupName);
        }
    }
}
