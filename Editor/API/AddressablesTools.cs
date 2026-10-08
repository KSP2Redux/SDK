using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.API
{
    /// <summary>
    /// Helpers for managing Unity Addressables entries from editor code.
    /// </summary>
    /// <remarks>
    /// Wraps the AddressableAssetSettings API with the entry-modification idioms the SDK needs for
    /// bake outputs and authoring exports.
    /// </remarks>
    [PublicAPI]
    public static class AddressablesTools
    {
        /// <summary>
        /// Registers <paramref name="assetPath" /> in <paramref name="group" /> with the given address
        /// and labels, creating or moving the entry as needed.
        /// </summary>
        /// <remarks>
        /// Labels are applied via <c>entry.SetLabel</c> so they persist to disk. Direct mutation of
        /// the runtime HashSet on the entry does not survive serialization.
        /// </remarks>
        /// <param name="group">The target Addressables group.</param>
        /// <param name="assetPath">Project-relative path of the asset to register.</param>
        /// <param name="name">Address to assign to the entry.</param>
        /// <param name="labels">Labels to attach to the entry. Labels not yet declared in settings are added.</param>
        public static void MakeAddressable(
            AddressableAssetGroup group,
            string assetPath,
            string name,
            params string[] labels
        )
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            foreach (string label in labels)
            {
                EnsureLabelIsDefined(settings, label);
                entry.SetLabel(label, true, true, false);
            }

            entry.address = name;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        /// <summary>
        /// Ensures <paramref name="label" /> is declared in <paramref name="settings" />, adding it if absent.
        /// </summary>
        /// <param name="settings">The Addressables settings to update.</param>
        /// <param name="label">The label to ensure exists.</param>
        public static void EnsureLabelIsDefined(AddressableAssetSettings settings, string label)
        {
            if (!settings.GetLabels().Contains(label))
            {
                settings.AddLabel(label);
            }
        }

        /// <summary>
        /// Removes the Addressables entry for <paramref name="assetPath" />, if one exists.
        /// </summary>
        /// <param name="assetPath">Project-relative path of the asset to unregister.</param>
        public static void RemoveAddressable(string assetPath)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;
            settings.RemoveAssetEntry(guid);
        }
    }
}