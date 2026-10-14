using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The part names, mission IDs and icon addresses the tech tree editor's pickers suggest.
    /// </summary>
    /// <remarks>
    /// Each list joins the game's own catalog, registered in the editor for the lookup, with the project's own
    /// Addressables entries. Trees use other mods' parts, so the pickers suggest and never restrict.
    /// Each list is built on first use and kept until the domain reloads.
    /// </remarks>
    public static class TechTreePickerCatalog
    {
        private const string PARTS_LABEL = "parts_data";

        private const string MISSIONS_LABEL = "missions";

        private const string ICON_LABEL = "techNodeIcon";

        private static readonly Regex PartNameRegex = new("\"partName\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static readonly Regex MissionIdRegex = new("\"ID\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        private static List<string> _partNames;

        private static List<string> _missionIds;

        private static List<string> _iconAddresses;

        /// <summary>
        /// Gets every part name the project knows.
        /// </summary>
        /// <returns>The part names, sorted.</returns>
        public static IReadOnlyList<string> GetPartNames() => _partNames ??= ReadIds(PARTS_LABEL, PartNameRegex);

        /// <summary>
        /// Gets every mission ID the project knows.
        /// </summary>
        /// <returns>The mission IDs, sorted.</returns>
        public static IReadOnlyList<string> GetMissionIds() => _missionIds ??= ReadIds(MISSIONS_LABEL, MissionIdRegex);

        /// <summary>
        /// Gets the address of every tech node icon the project knows.
        /// </summary>
        /// <returns>The icon addresses, sorted.</returns>
        public static IReadOnlyList<string> GetIconAddresses()
        {
            if (_iconAddresses != null)
                return _iconAddresses;

            // The game's icons are only in the editor once its catalog is registered
            bool hasStockCatalog = EditorPqsBootstrap.EnsureStockCatalog();
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (!locator.Locate(ICON_LABEL, typeof(Sprite), out var locations))
                    continue;

                foreach (var location in locations)
                {
                    addresses.Add(location.PrimaryKey);
                }
            }

            foreach (AddressableAssetEntry entry in ProjectEntries(ICON_LABEL))
            {
                addresses.Add(entry.address);
            }

            List<string> sorted = Sorted(addresses);
            if (hasStockCatalog)
            {
                _iconAddresses = sorted;
            }

            return sorted;
        }

        // The runtime catalogs hold the game's assets, the project's own are read off its Addressables settings
        private static List<string> ReadIds(string label, Regex idPattern)
        {
            EditorPqsBootstrap.EnsureStockCatalog();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            void AddFrom(string json)
            {
                Match match = json != null ? idPattern.Match(json) : Match.Empty;
                if (match.Success)
                {
                    ids.Add(match.Groups[1].Value);
                }
            }

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(label, null);
                IList<TextAsset> assets = handle.WaitForCompletion();
                foreach (TextAsset asset in assets ?? Array.Empty<TextAsset>())
                {
                    AddFrom(asset != null ? asset.text : null);
                }

                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TechTreePickerCatalog] Could not load {label} from Addressables. {ex.Message}");
            }

            foreach (AddressableAssetEntry entry in ProjectEntries(label))
            {
                AddFrom(AssetDatabase.LoadAssetAtPath<TextAsset>(entry.AssetPath)?.text);
            }

            return Sorted(ids);
        }

        private static IEnumerable<AddressableAssetEntry> ProjectEntries(string label)
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
                return Enumerable.Empty<AddressableAssetEntry>();

            return settings.groups
                .Where(group => group != null)
                .SelectMany(group => group.entries)
                .Where(entry => entry.labels.Contains(label));
        }

        private static List<string> Sorted(IEnumerable<string> values) =>
            values.Where(value => !string.IsNullOrEmpty(value)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
