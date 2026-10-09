using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Redux.Packs;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Ksp2UnityTools.Editor.Widgets
{
    /// <summary>
    /// Enumerates the layers the campaign packs in the project select, for the autocomplete of a layer field.
    /// </summary>
    public static class CampaignPackLayerCatalog
    {
        /// <summary>
        /// Returns the default layer and every layer of one kind that a campaign pack in the project selects,
        /// sorted alphabetically without duplicates.
        /// </summary>
        /// <param name="layersOf">Picks the layers of the wanted kind from a campaign pack.</param>
        /// <returns>The known layers.</returns>
        public static IReadOnlyList<string> GetKnownLayers(Func<CampaignPack, IEnumerable<string>> layersOf)
        {
            var layers = new HashSet<string>(StringComparer.Ordinal) { CampaignPack.DEFAULT_LAYER };
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                ScanCampaignPacks(settings, layersOf, layers);
            }

            return layers.OrderBy(layer => layer, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ScanCampaignPacks(
            AddressableAssetSettings settings,
            Func<CampaignPack, IEnumerable<string>> layersOf,
            HashSet<string> sink
        )
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
                        if (campaignPack != null)
                        {
                            sink.UnionWith(layersOf(campaignPack) ?? Enumerable.Empty<string>());
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }
            }
        }
    }
}
