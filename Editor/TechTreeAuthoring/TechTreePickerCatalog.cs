using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The part names, part variants, mission IDs and icon addresses the tech tree editor's pickers suggest.
    /// </summary>
    /// <remarks>
    /// Each list joins the game's own catalog, registered in the editor for the lookup, with the project's own
    /// Addressables entries. Trees use other mods' parts, so the pickers suggest and never restrict.
    /// Each list is built on first use and rebuilt after any change to the project's Addressables.
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

        private static List<TechUnlockedVariant> _variants;

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
        /// Gets every part variant the project knows, from the variant sets of each part's part switch.
        /// </summary>
        /// <returns>The variants, sorted by part, then set, then variant.</returns>
        public static IReadOnlyList<TechUnlockedVariant> GetVariants()
        {
            if (_variants != null)
                return _variants;

            // The game's parts are only in the editor once its catalog is registered
            bool hasStockCatalog = EditorPqsBootstrap.EnsureStockCatalog();
            var variants = new Dictionary<(string, string, string), TechUnlockedVariant>();
            foreach (string json in AddressablesTools.GetCatalogText(PARTS_LABEL).Concat(AddressablesTools.GetProjectText(PARTS_LABEL)))
            {
                foreach (TechUnlockedVariant variant in ReadVariants(json))
                {
                    variants.TryAdd((variant.PartName, variant.VariantSetId, variant.VariantId), variant);
                }
            }

            List<TechUnlockedVariant> sorted = variants.Values
                .OrderBy(variant => variant.PartName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(variant => variant.VariantSetId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(variant => variant.VariantId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (hasStockCatalog)
            {
                _variants = sorted;
            }

            return sorted;
        }

        /// <summary>
        /// Reads the variants of one part's JSON.
        /// </summary>
        /// <param name="json">The part's JSON.</param>
        /// <returns>Each variant in the part's variant sets, none when the JSON is not a part or has none.</returns>
        internal static IEnumerable<TechUnlockedVariant> ReadVariants(string json)
        {
            JObject part;
            try
            {
                part = JObject.Parse(json);
            }
            catch (JsonException)
            {
                yield break;
            }

            string partName = part.SelectToken("data.partName")?.ToString();
            if (string.IsNullOrEmpty(partName))
                yield break;

            foreach (JToken set in part.SelectTokens("$..VariantSets[*]"))
            {
                string setId = set[nameof(TechUnlockedVariant.VariantSetId)]?.ToString();
                foreach (JToken variant in set["Variants"] ?? new JArray())
                {
                    string variantId = variant[nameof(TechUnlockedVariant.VariantId)]?.ToString();
                    if (!string.IsNullOrEmpty(setId) && !string.IsNullOrEmpty(variantId))
                    {
                        yield return new TechUnlockedVariant { PartName = partName, VariantSetId = setId, VariantId = variantId };
                    }
                }
            }
        }

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

            foreach (AddressableAssetEntry entry in AddressablesTools.GetProjectEntries(ICON_LABEL))
            {
                addresses.Add(entry.address);
            }

            List<string> sorted = addresses
                .Where(address => !string.IsNullOrEmpty(address))
                .OrderBy(address => address, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (hasStockCatalog)
            {
                _iconAddresses = sorted;
            }

            return sorted;
        }

        // A part, mission or icon added to the project shows in the pickers without waiting for a domain reload
        [InitializeOnLoadMethod]
        private static void ClearOnAddressablesChange() =>
            AddressableAssetSettings.OnModificationGlobal += (_, _, _) =>
            {
                _partNames = null;
                _missionIds = null;
                _iconAddresses = null;
                _variants = null;
            };

        private static List<string> ReadIds(string label, Regex idPattern)
        {
            EditorPqsBootstrap.EnsureStockCatalog();
            return AddressablesJsonCatalog.Build(label, json =>
            {
                Match match = json != null ? idPattern.Match(json) : Match.Empty;
                return match.Success ? match.Groups[1].Value : null;
            });
        }
    }
}
