using System.Collections.Generic;
using Ksp2UnityTools.Editor.Widgets;

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
        public static IReadOnlyList<string> GetKnownLayers() =>
            CampaignPackLayerCatalog.GetKnownLayers(campaignPack => campaignPack.MissionLayers);
    }
}
