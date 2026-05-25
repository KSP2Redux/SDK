using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Shared loader for catalogs that enumerate string identifiers from addressable <see cref="TextAsset" /> entries with a given label.
    /// </summary>
    /// <remarks>
    /// Loads each TextAsset, runs the caller-supplied extractor against its JSON, deduplicates, and returns the alphabetically-sorted list. Uses the runtime <see cref="Addressables" /> API rather than <c>AddressableAssetSettingsDefaultObject.Settings</c> because the SDK runs in contexts where KSP2 is imported via ThunderKit. Stock entries only appear in the runtime catalog, not in the editor-side asset settings.
    /// </remarks>
    internal static class AddressablesJsonCatalog
    {
        /// <summary>
        /// Loads every addressable TextAsset with the given label and returns the deduplicated, alphabetically-sorted list of non-empty extractor results.
        /// </summary>
        /// <param name="label">The Addressables label to query.</param>
        /// <param name="logPrefix">Prefix used when logging load failures.</param>
        /// <param name="tryExtract">Callback that extracts the identifier string from a TextAsset's JSON, returning null or empty to skip the entry.</param>
        /// <returns>The deduplicated, alphabetically-sorted list of extracted identifiers.</returns>
        public static List<string> Build(string label, string logPrefix, Func<string, string> tryExtract)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(label, null);
                var assets = handle.WaitForCompletion();
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset == null) continue;
                        var name = tryExtract(asset.text);
                        if (!string.IsNullOrEmpty(name)) names.Add(name);
                    }
                }
                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[{logPrefix}] Failed to load via addressables. {ex.Message}");
            }

            return names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
