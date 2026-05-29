using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a celestial body asset: bodyDisplayName, bodyDescription, plus
    /// Science/Regions/{Id} for each region in the matching ScienceRegionData (resolved via
    /// <see cref="ScienceRegionAssetLocator.FindForBody" /> to share the runtime's body-name key).
    /// </summary>
    public static class CelestialBodyLocalizationExtractor
    {
        /// <summary>
        /// Extracts the display, description, and science region localization keys for the given celestial body asset.
        /// </summary>
        /// <param name="coreBody">The celestial body data asset to scan.</param>
        /// <returns>The collected localization key entries, or an empty list when the asset is missing data or a body name.</returns>
        public static List<LocalizationKeyEntry> Extract(CoreCelestialBodyData coreBody)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (coreBody?.Data == null) return entries;

            var body = coreBody.Data;
            var bodyName = body.bodyName;
            if (string.IsNullOrEmpty(bodyName)) return entries;

            var assetPath = AssetDatabase.GetAssetPath(coreBody);
            var sourceHint = LocalizationSourceHint.Format(nameof(CoreCelestialBodyData), bodyName, assetPath);

            var displayKey = string.IsNullOrEmpty(body.bodyDisplayName)
                ? "CelestialBody/" + bodyName
                : body.bodyDisplayName;
            entries.Add(new LocalizationKeyEntry(
                displayKey, bodyName,
                $"Display name for celestial body {bodyName}", sourceHint));

            if (!string.IsNullOrEmpty(body.bodyDescription))
            {
                entries.Add(new LocalizationKeyEntry(
                    body.bodyDescription, string.Empty,
                    $"Long-form description for celestial body {bodyName}", sourceHint));
            }

            var regionData = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (regionData?.information?.ScienceRegionDefinitions != null)
            {
                foreach (var regionDef in regionData.information.ScienceRegionDefinitions)
                {
                    if (regionDef == null || string.IsNullOrEmpty(regionDef.Id)) continue;
                    entries.Add(new LocalizationKeyEntry(
                        "Science/Regions/" + regionDef.Id, regionDef.Id,
                        $"Science region '{regionDef.Id}' on body {bodyName}", sourceHint));
                }
            }
            return entries;
        }
    }
}
