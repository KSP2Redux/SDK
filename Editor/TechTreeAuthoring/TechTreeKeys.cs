using System;
using Ksp2UnityTools.Editor.Modding;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// The localization keys a tech tree bakes for its nodes and tiers.
    /// </summary>
    /// <remarks>
    /// Localization keys are global, so a mod's tree scopes its node keys by the mod's id. Otherwise a mod tree that
    /// reuses a stock ID would overwrite the stock name wherever the stock node is shown. Redux's own tree keeps the
    /// stock key shape, so its nodes keep the keys they already have.
    /// </remarks>
    public static class TechTreeKeys
    {
        /// <summary>
        /// Gets the key of a node's name.
        /// </summary>
        /// <param name="ownerId">The id of the mod the tree belongs to.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The localization key.</returns>
        public static string GetNameKey(string ownerId, string nodeId) =>
            IsRedux(ownerId) ? $"Science/TechNodes/Names/{nodeId}" : $"Science/TechNodes/{ownerId}/Names/{nodeId}";

        /// <summary>
        /// Gets the key of a node's description.
        /// </summary>
        /// <param name="ownerId">The id of the mod the tree belongs to.</param>
        /// <param name="nodeId">The node's ID.</param>
        /// <returns>The localization key.</returns>
        public static string GetDescriptionKey(string ownerId, string nodeId) =>
            IsRedux(ownerId)
                ? $"Science/TechNodes/Descriptions/{nodeId}"
                : $"Science/TechNodes/{ownerId}/Descriptions/{nodeId}";

        /// <summary>
        /// Gets the key of a tier's name.
        /// </summary>
        /// <param name="ownerId">The id of the mod the tree belongs to.</param>
        /// <param name="tier">The tier, counted from 1.</param>
        /// <returns>The localization key.</returns>
        public static string GetTierNameKey(string ownerId, int tier) => $"Science/TechTree/{ownerId}/Tiers/{tier}";

        private static bool IsRedux(string ownerId) =>
            string.Equals(ownerId, FeatureCatalog.REDUX_MOD_ID, StringComparison.Ordinal);
    }
}
