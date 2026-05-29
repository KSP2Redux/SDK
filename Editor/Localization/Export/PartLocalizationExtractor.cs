using System.Collections.Generic;
using System.Linq;
using KSP;
using KSP.Sim.Definitions;
using UnityEditor;
using VSwift.Modules.Data;
using VSwift.Modules.Variants;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a part asset: the four convention keys derived from partName,
    /// plus per-variant-set and per-variant keys discovered on attached Data_PartSwitch modules.
    /// </summary>
    public static class PartLocalizationExtractor
    {
        /// <summary>
        /// Extracts the convention and variant localization keys for the given part asset.
        /// </summary>
        /// <param name="corePart">The part data asset to scan.</param>
        /// <returns>The collected localization key entries, or an empty list when the asset is missing data or a part name.</returns>
        public static List<LocalizationKeyEntry> Extract(CorePartData corePart)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (corePart?.Data == null) return entries;

            var partName = corePart.Data.partName;
            if (string.IsNullOrEmpty(partName)) return entries;

            var assetPath = AssetDatabase.GetAssetPath(corePart);
            var sourceHint = LocalizationSourceHint.Format(nameof(CorePartData), partName, assetPath);

            AddPartConventionKeys(entries, partName, sourceHint);
            AddVariantKeys(entries, corePart.Core?.modules, partName, sourceHint);
            return entries;
        }

        private static void AddPartConventionKeys(List<LocalizationKeyEntry> entries, string partName, string sourceHint)
        {
            entries.Add(new LocalizationKeyEntry(
                "Parts/Title/" + partName, partName,
                $"Display name shown in PAM and Parts Picker for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Subtitle/" + partName, string.Empty,
                $"Subtitle shown beneath the title for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Manufacturer/" + partName, string.Empty,
                $"Manufacturer line shown in the part tooltip for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Description/" + partName, string.Empty,
                $"Multi-line part description shown in PAM tooltip for {partName}", sourceHint));
        }

        private static void AddVariantKeys(List<LocalizationKeyEntry> entries, List<ModuleData> modules, string partName, string sourceHint)
        {
            if (modules == null) return;
            foreach (var dataSwitch in modules.OfType<Data_PartSwitch>())
            {
                if (dataSwitch.VariantSets == null) continue;
                foreach (var variantSet in dataSwitch.VariantSets)
                {
                    if (variantSet == null) continue;
                    AddVariantSetKeys(entries, variantSet, partName, sourceHint);
                }
            }
        }

        private static void AddVariantSetKeys(List<LocalizationKeyEntry> entries, VariantSet variantSet, string partName, string sourceHint)
        {
            var setKey = string.IsNullOrEmpty(variantSet.VariantSetLocalizationKey)
                ? variantSet.VariantSetId
                : variantSet.VariantSetLocalizationKey;
            if (!string.IsNullOrEmpty(setKey))
            {
                entries.Add(new LocalizationKeyEntry(
                    setKey, variantSet.VariantSetId,
                    $"Display name for variant set '{variantSet.VariantSetId}' on part {partName}",
                    sourceHint));
            }
            if (variantSet.Variants == null) return;
            foreach (var variant in variantSet.Variants)
            {
                if (variant == null) continue;
                var variantKey = string.IsNullOrEmpty(variant.VariantLocalizationKey)
                    ? variant.VariantId
                    : variant.VariantLocalizationKey;
                if (string.IsNullOrEmpty(variantKey)) continue;
                entries.Add(new LocalizationKeyEntry(
                    variantKey, variant.VariantId,
                    $"Display name for variant '{variant.VariantId}' in set '{variantSet.VariantSetId}' on part {partName}",
                    sourceHint));
            }
        }
    }
}
