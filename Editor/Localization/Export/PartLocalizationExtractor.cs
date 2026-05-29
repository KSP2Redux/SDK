using System.Collections.Generic;
using System.Linq;
using KSP;
using UnityEditor;
using VSwift.Modules.Data;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a part asset: the four convention keys derived from partName,
    /// plus per-variant-set and per-variant keys discovered on attached Data_PartSwitch modules.
    /// </summary>
    public static class PartLocalizationExtractor
    {
        public static List<LocalizationKeyEntry> Extract(CorePartData corePart)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (corePart == null || corePart.Data == null) return entries;

            var data = corePart.Data;
            var partName = data.partName;
            if (string.IsNullOrEmpty(partName)) return entries;

            var assetPath = AssetDatabase.GetAssetPath(corePart);
            var sourceHint = $"CorePartData: {partName} ({assetPath})";

            entries.Add(new LocalizationKeyEntry(
                "Parts/Title/" + partName, partName,
                $"Display name shown in PAM and Parts Picker for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Subtitle/" + partName, string.Empty,
                $"Subtitle / variant name shown beneath the title for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Manufacturer/" + partName, string.Empty,
                $"Manufacturer line shown in the part tooltip for {partName}", sourceHint));
            entries.Add(new LocalizationKeyEntry(
                "Parts/Description/" + partName, string.Empty,
                $"Multi-line part description shown in PAM tooltip for {partName}", sourceHint));

            if (corePart.Core?.modules != null)
            {
                foreach (var dataSwitch in corePart.Core.modules.OfType<Data_PartSwitch>())
                {
                    if (dataSwitch.VariantSets == null) continue;
                    foreach (var variantSet in dataSwitch.VariantSets)
                    {
                        if (variantSet == null) continue;
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
                        if (variantSet.Variants == null) continue;
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
            return entries;
        }
    }
}
