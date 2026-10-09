using Redux.Packs;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The localization keys a tech tree bakes for its nodes and tiers.
    /// </summary>
    /// <remarks>
    /// Localization keys are global, so a layer other than the default one scopes its node keys by layer. Otherwise
    /// a mod tree that reuses a stock ID would overwrite the stock name in every default campaign. The default layer
    /// keeps the stock key shape, so the Redux tree keeps the keys it already has.
    /// </remarks>
    public static class TechTreeKeys
    {
        /// <summary>
        /// Gets the key of a node's name.
        /// </summary>
        /// <param name="layer">The tree's layer.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The localization key.</returns>
        public static string GetNameKey(string layer, string nodeId) =>
            CampaignPack.IsDefaultLayer(layer)
                ? $"Science/TechNodes/Names/{nodeId}"
                : $"Science/TechNodes/{layer}/Names/{nodeId}";

        /// <summary>
        /// Gets the key of a node's description.
        /// </summary>
        /// <param name="layer">The tree's layer.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The localization key.</returns>
        public static string GetDescriptionKey(string layer, string nodeId) =>
            CampaignPack.IsDefaultLayer(layer)
                ? $"Science/TechNodes/Descriptions/{nodeId}"
                : $"Science/TechNodes/{layer}/Descriptions/{nodeId}";

        /// <summary>
        /// Gets the key of a tier's name.
        /// </summary>
        /// <param name="layer">The tree's layer.</param>
        /// <param name="tier">The tier, counted from 1.</param>
        /// <returns>The localization key.</returns>
        public static string GetTierNameKey(string layer, int tier) =>
            $"Science/TechTree/{(CampaignPack.IsDefaultLayer(layer) ? CampaignPack.DEFAULT_LAYER : layer)}/Tiers/{tier}";
    }
}
