using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using Redux.Features;
using SpaceWarp2.API.Features;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.Modding
{
    /// <summary>
    /// Reads and writes the <c>feature:</c> and <c>nofeature:</c> Addressables labels that gate an asset by feature.
    /// </summary>
    /// <remarks>
    /// The game loads a mod's asset only while the mod's own feature is on, every feature its labels require is on
    /// and every one they exclude is off. The mod's own feature is never written as a label, since it always holds.
    /// </remarks>
    public static class AssetFeatureLabels
    {
        /// <summary>
        /// Reads the features an asset's labels require and exclude.
        /// </summary>
        /// <param name="labels">The asset's labels.</param>
        /// <param name="ownerId">The owning mod's id, whose features are given bare.</param>
        /// <returns>The required and the excluded features, each sorted.</returns>
        public static (List<string> features, List<string> noFeatures) Read(IEnumerable<string> labels, string ownerId)
        {
            var labelList = labels.ToList();
            return (
                IdsWithPrefix(labelList, FeatureSelector.FEATURE_TAG_PREFIX, ownerId),
                IdsWithPrefix(labelList, FeatureSelector.NO_FEATURE_TAG_PREFIX, ownerId)
            );
        }

        /// <summary>
        /// Reads the features a set of assets' labels require and exclude between them.
        /// </summary>
        /// <param name="labelSets">Each asset's labels.</param>
        /// <param name="ownerId">The owning mod's id, whose features are given bare.</param>
        /// <returns>Every feature any of the assets requires, and every one any excludes, each sorted.</returns>
        public static (List<string> features, List<string> noFeatures) ReadAll(
            IEnumerable<IEnumerable<string>> labelSets,
            string ownerId
        ) => Read(labelSets.SelectMany(labels => labels).Distinct(StringComparer.Ordinal), ownerId);

        /// <summary>
        /// Checks whether the game loads an asset by one of the content labels the feature gate filters. Only those
        /// need feature labels: anything else loads because gated content names it.
        /// </summary>
        /// <param name="labels">The asset's labels.</param>
        /// <returns>True if the asset carries a content label.</returns>
        public static bool IsGatedContent(IEnumerable<string> labels) =>
            labels.Any(FeatureAssetIndex.StockFeatureByLabel.ContainsKey);

        /// <summary>
        /// Works out an asset's labels with its feature labels replaced, keeping every other label.
        /// </summary>
        /// <param name="labels">The asset's labels now.</param>
        /// <param name="ownerId">The owning mod's id, which bare ids belong to.</param>
        /// <param name="features">The features to require.</param>
        /// <param name="noFeatures">The features to exclude.</param>
        /// <returns>The new labels.</returns>
        public static HashSet<string> WithFeatures(
            IEnumerable<string> labels,
            string ownerId,
            IEnumerable<string> features,
            IEnumerable<string> noFeatures
        )
        {
            var result = new HashSet<string>(labels.Where(label => !IsFeatureLabel(label)), StringComparer.Ordinal);
            string ownFeature = FeatureRegistry.DefaultFor(ownerId);
            foreach (string feature in Qualified(features, ownerId).Where(id => !Same(id, ownFeature)))
            {
                result.Add(FeatureSelector.FEATURE_TAG_PREFIX + feature);
            }

            foreach (string feature in Qualified(noFeatures, ownerId))
            {
                result.Add(FeatureSelector.NO_FEATURE_TAG_PREFIX + feature);
            }

            return result;
        }

        /// <summary>
        /// Picks the Redux category label a new Redux asset takes from the content labels it was registered with,
        /// since the game treats an untagged Redux asset as an error.
        /// </summary>
        /// <param name="labels">The asset's labels.</param>
        /// <returns>The category's feature label, or null when the asset has a feature label already or no content
        /// label names a category.</returns>
        public static string ReduxCategoryLabelFor(IEnumerable<string> labels)
        {
            var labelList = labels.ToList();
            if (labelList.Any(IsFeatureLabel))
                return null;

            foreach (string label in labelList)
            {
                if (FeatureAssetIndex.StockFeatureByLabel.TryGetValue(label, out string category))
                    return FeatureSelector.FEATURE_TAG_PREFIX + category;
            }

            return null;
        }

        /// <summary>
        /// Finds the Addressables entry of an asset.
        /// </summary>
        /// <param name="assetPath">The asset's project path.</param>
        /// <returns>The entry, or null when the asset is not addressable.</returns>
        public static AddressableAssetEntry EntryOf(string assetPath)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            return settings == null || string.IsNullOrEmpty(guid) ? null : settings.FindAssetEntry(guid);
        }

        /// <summary>
        /// Replaces the feature labels of every addressable asset among some paths.
        /// </summary>
        /// <param name="assetPaths">The assets' project paths. Ones that are not addressable are skipped.</param>
        /// <param name="ownerId">The owning mod's id, which bare ids belong to.</param>
        /// <param name="features">The features to require.</param>
        /// <param name="noFeatures">The features to exclude.</param>
        public static void Apply(
            IEnumerable<string> assetPaths,
            string ownerId,
            IReadOnlyCollection<string> features,
            IReadOnlyCollection<string> noFeatures
        )
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return;

            foreach (AddressableAssetEntry entry in assetPaths.Select(EntryOf).Where(entry => entry != null))
            {
                HashSet<string> labels = WithFeatures(entry.labels, ownerId, features, noFeatures);
                foreach (string removed in entry.labels.Where(label => !labels.Contains(label)).ToList())
                {
                    entry.SetLabel(removed, false, true, false);
                }

                foreach (string added in labels.Where(label => !entry.labels.Contains(label)).ToList())
                {
                    AddressablesTools.EnsureLabelIsDefined(settings, added);
                    entry.SetLabel(added, true, true, false);
                }

                settings.SetDirty(AddressableAssetSettings.ModificationEvent.LabelAdded, entry, true);
            }

            RemoveUnusedFeatureLabels(settings);
        }

        // An edit that is abandoned or replaced leaves its label declared, so declared feature labels no entry
        // carries are dropped
        private static void RemoveUnusedFeatureLabels(AddressableAssetSettings settings)
        {
            var used = new HashSet<string>(
                settings.groups
                    .Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .SelectMany(entry => entry.labels)
                    .Where(IsFeatureLabel),
                StringComparer.Ordinal
            );
            foreach (string label in settings.GetLabels().Where(label => IsFeatureLabel(label) && !used.Contains(label)).ToList())
            {
                settings.RemoveLabel(label);
            }
        }

        private static bool IsFeatureLabel(string label) =>
            label.StartsWith(FeatureSelector.FEATURE_TAG_PREFIX, StringComparison.Ordinal)
            || label.StartsWith(FeatureSelector.NO_FEATURE_TAG_PREFIX, StringComparison.Ordinal);

        private static List<string> IdsWithPrefix(IEnumerable<string> labels, string prefix, string ownerId) =>
            labels
                .Where(label => label.StartsWith(prefix, StringComparison.Ordinal))
                .Select(label => Bare(label.Substring(prefix.Length), ownerId))
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // A feature of the owning mod reads bare, as authors write it
        private static string Bare(string featureId, string ownerId) =>
            Same(FeatureRegistry.NamespaceOf(featureId), ownerId)
                ? featureId.Substring(featureId.IndexOf(FeatureRegistry.SEPARATOR) + 1)
                : featureId;

        private static IEnumerable<string> Qualified(IEnumerable<string> ids, string ownerId) =>
            (ids ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => FeatureRegistry.Qualify(ownerId, id.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        private static bool Same(string first, string second) =>
            string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }
}
