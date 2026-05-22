using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Enumerates known resource definition names by loading addressable TextAssets labeled
    /// <c>"resources"</c> and parsing each one's JSON for its resource name.
    /// </summary>
    /// <remarks>
    /// Uses the runtime <see cref="Addressables" /> API rather than
    /// <c>AddressableAssetSettingsDefaultObject.Settings</c> because the SDK runs in contexts
    /// where KSP2 is imported via ThunderKit - stock resources only appear in the runtime catalog,
    /// not in the editor-side asset settings. The runtime API returns assets from both
    /// project-local entries and ThunderKit-imported catalogs, matching the convention that
    /// <c>PopulateResourceDefinitionDatabaseFlowAction</c> uses at game load time.
    ///
    /// The cache builds lazily on first query. Call <see cref="Invalidate" /> after operations that
    /// change the addressable resource set.
    /// </remarks>
    public static class ResourceNameCatalog
    {
        private const string RESOURCES_LABEL = "resources";

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted list of known resource names.
        /// </summary>
        public static IReadOnlyList<string> GetKnownResources()
        {
            return _cached ??= Build();
        }

        /// <summary>
        /// Forces a rebuild on the next query.
        /// </summary>
        public static void Invalidate()
        {
            _cached = null;
        }

        private static List<string> Build()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(RESOURCES_LABEL, null);
                var assets = handle.WaitForCompletion();
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset == null)
                        {
                            continue;
                        }
                        var name = TryExtractName(asset.text);
                        if (!string.IsNullOrEmpty(name))
                        {
                            names.Add(name);
                        }
                    }
                }
                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ResourceNameCatalog] Failed to load resources via addressables: {ex.Message}");
            }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string TryExtractName(string json)
        {
            try
            {
                var parsed = JObject.Parse(json);
                var isRecipe = parsed["isRecipe"]?.Value<bool>() ?? false;
                var dataKey = isRecipe ? "recipeData" : "data";
                return parsed[dataKey]?["name"]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
