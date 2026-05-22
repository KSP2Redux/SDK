using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets
{
    /// <summary>
    /// Enumerates known science experiment IDs by loading addressable TextAssets labeled
    /// <c>"scienceExperiment"</c> and parsing each one's JSON for <c>data.ExperimentID</c>.
    /// </summary>
    /// <remarks>
    /// Uses the runtime <see cref="Addressables" /> API rather than
    /// <c>AddressableAssetSettingsDefaultObject.Settings</c> because the SDK runs in contexts
    /// where KSP2 is imported via ThunderKit. Same reasoning as <see cref="ResourceNameCatalog" />.
    /// </remarks>
    public static class ExperimentNameCatalog
    {
        private const string SCIENCE_EXPERIMENT_LABEL = "scienceExperiment";

        private static List<string> _cached;

        /// <summary>
        /// Returns the alphabetically-sorted list of known experiment IDs.
        /// </summary>
        public static IReadOnlyList<string> GetKnownExperiments()
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
            var ids = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(SCIENCE_EXPERIMENT_LABEL, null);
                var assets = handle.WaitForCompletion();
                if (assets != null)
                {
                    foreach (var asset in assets)
                    {
                        if (asset == null)
                        {
                            continue;
                        }
                        var id = TryExtractId(asset.text);
                        if (!string.IsNullOrEmpty(id))
                        {
                            ids.Add(id);
                        }
                    }
                }
                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ExperimentNameCatalog] Failed to load experiments via addressables: {ex.Message}");
            }

            return ids.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string TryExtractId(string json)
        {
            try
            {
                return JObject.Parse(json)["data"]?["ExperimentID"]?.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
