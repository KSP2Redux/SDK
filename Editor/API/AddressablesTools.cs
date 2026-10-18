using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
            // The group's own settings, so a project whose DefaultObject lost track of its settings still registers
            AddressableAssetSettings settings = group.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"Cannot make '{assetPath}' addressable: group '{group.Name}' has no Addressables settings and the project has none. Create them in Window > Asset Management > Addressables > Groups."
                );
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group);
            foreach (string label in labels)
            {
                EnsureLabelIsDefined(settings, label);
                entry.SetLabel(label, true, true, false);
            }

#if REDUX
            // The game treats an untagged Redux asset as an error, so new Redux content takes its kind's category
            string categoryLabel = Modding.AssetFeatureLabels.ReduxCategoryLabelFor(entry.labels);
            if (categoryLabel != null)
            {
                EnsureLabelIsDefined(settings, categoryLabel);
                entry.SetLabel(categoryLabel, true, true, false);
            }
#endif

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
            if (settings == null)
                return;

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                return;

            settings.RemoveAssetEntry(guid);
        }

        /// <summary>
        /// Lists the project's own Addressables entries that carry a label.
        /// </summary>
        /// <remarks>
        /// These are the entries of the project's Addressables settings. A built catalog, such as the game's, is not
        /// searched.
        /// </remarks>
        /// <param name="label">The label.</param>
        /// <returns>The entries, empty when the project has no Addressables settings.</returns>
        public static IEnumerable<AddressableAssetEntry> GetProjectEntries(string label)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return Enumerable.Empty<AddressableAssetEntry>();

            return settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
                .Where(entry => entry.labels.Contains(label));
        }

        /// <summary>
        /// Reads the text of the text assets that carry a label in the loaded runtime catalogs, such as the game's.
        /// </summary>
        /// <remarks>
        /// The project's own entries are not in a catalog until its Addressables are built, so they are read with
        /// <see cref="GetProjectText" /> instead. A label no catalog knows gives nothing rather than a failed load.
        /// </remarks>
        /// <param name="label">The label.</param>
        /// <returns>Each asset's text, empty when no catalog has the label or the load fails.</returns>
        public static List<string> GetCatalogText(string label)
        {
            if (!Addressables.ResourceLocators.Any(locator => locator.Locate(label, typeof(TextAsset), out _)))
                return new List<string>();

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(label, null);
                IList<TextAsset> assets = handle.WaitForCompletion();
                List<string> text = (assets ?? Array.Empty<TextAsset>())
                    .Where(asset => asset != null)
                    .Select(asset => asset.text)
                    .ToList();
                Addressables.Release(handle);
                return text;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AddressablesTools] Could not load {label} from Addressables. {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Reads the text of the project's own Addressables text assets that carry a label.
        /// </summary>
        /// <param name="label">The label.</param>
        /// <param name="excludedPaths">Asset paths to leave out, or null for none.</param>
        /// <returns>Each asset's text, in entry order.</returns>
        public static List<string> GetProjectText(string label, ICollection<string> excludedPaths = null)
        {
            var text = new List<string>();
            foreach (AddressableAssetEntry entry in GetProjectEntries(label))
            {
                if (excludedPaths != null && excludedPaths.Contains(entry.AssetPath))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(entry.AssetPath);
                if (asset != null)
                {
                    text.Add(asset.text);
                }
            }

            return text;
        }
    }
}