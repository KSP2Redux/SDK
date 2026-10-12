using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Redux.Packs;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Enumerates known mission layers for the Layer field's autocomplete: the default layer, and every mission layer
    /// a campaign pack in the project selects.
    /// </summary>
    public static class MissionLayerCatalog
    {
        /// <summary>
        /// Returns the alphabetically-sorted, deduplicated list of known mission layers.
        /// </summary>
        /// <returns>The known mission layers.</returns>
        public static IReadOnlyList<string> GetKnownLayers()
        {
            var layers = new HashSet<string>(StringComparer.Ordinal) { CampaignPack.DEFAULT_LAYER };
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                ScanCampaignPacks(settings, layers);
            }

            return layers.OrderBy(layer => layer, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ScanCampaignPacks(AddressableAssetSettings settings, HashSet<string> sink)
        {
            foreach (AddressableAssetGroup group in settings.groups)
            {
                if (group == null)
                    continue;

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    if (!entry.labels.Contains(CampaignPackManager.CAMPAIGN_PACK_LABEL) || !File.Exists(entry.AssetPath))
                        continue;

                    // A pack that fails to parse only loses its suggestions
                    try
                    {
                        var campaignPack = JsonConvert.DeserializeObject<CampaignPack>(File.ReadAllText(entry.AssetPath));
                        sink.UnionWith(campaignPack?.MissionLayers ?? new List<string>());
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
        }
    }
}
