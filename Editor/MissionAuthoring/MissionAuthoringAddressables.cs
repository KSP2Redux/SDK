using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Constant names and resolution helpers for mission-authoring addressables groups.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>PartAuthoringAddressables</c> and <c>PlanetAuthoringAddressables</c>. The
    /// project-level <see cref="MissionsGroupName" /> group is the fallback when the asset is
    /// not owned by a Mod with its own per-mod missions group.
    /// </remarks>
    internal static class MissionAuthoringAddressables
    {
        /// <summary>
        /// Name of the project-level missions addressables group.
        /// </summary>
        public const string MissionsGroupName = "Missions";

        /// <summary>
        /// Addressable label applied to every mission JSON. Matches the runtime load path.
        /// </summary>
        public const string MissionsLabel = "missions";

        /// <summary>
        /// Resolves the addressables group for a Mission asset.
        /// </summary>
        /// <remarks>
        /// Prefers the owning mod's per-mod missions group, falls back to the project-level
        /// <see cref="MissionsGroupName" /> group, returns null if neither exists.
        /// </remarks>
        /// <param name="target">The mission asset whose addressables group should be resolved.</param>
        /// <returns>The resolved addressables group, or null if neither a per-mod nor project-level group exists.</returns>
        public static AddressableAssetGroup ResolveGroup(Mission target)
        {
            if (KSP2UnityTools.FindParentMod(target) is { } mod && mod.missionsGroup != null)
            {
                return mod.missionsGroup;
            }
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            return settings?.groups.FirstOrDefault(g => g != null && g.Name == MissionsGroupName);
        }
    }
}
