using System.Collections.Generic;
using Ksp2UnityTools.Editor.CampaignPacks;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a campaign pack asset: player-facing name and description.
    /// </summary>
    public static class CampaignPackLocalizationExtractor
    {
        /// <summary>
        /// Extracts the player-facing localization keys for the given campaign pack asset.
        /// </summary>
        /// <param name="pack">The campaign pack asset to scan.</param>
        /// <returns>The collected localization key entries, or an empty list when no keys are authored.</returns>
        public static List<LocalizationKeyEntry> Extract(CampaignPack pack)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (pack == null) return entries;

            var packId = string.IsNullOrEmpty(pack.id) ? pack.name : pack.id;
            var assetPath = AssetDatabase.GetAssetPath(pack);
            var sourceHint = LocalizationSourceHint.Format(nameof(CampaignPack), packId, assetPath);

            if (!string.IsNullOrEmpty(pack.nameLocKey))
            {
                entries.Add(new LocalizationKeyEntry(
                    pack.nameLocKey,
                    packId,
                    $"Campaign pack display name for {packId}",
                    sourceHint));
            }

            if (!string.IsNullOrEmpty(pack.descriptionLocKey))
            {
                entries.Add(new LocalizationKeyEntry(
                    pack.descriptionLocKey,
                    string.Empty,
                    $"Campaign pack description for {packId}",
                    sourceHint));
            }

            return entries;
        }
    }
}
