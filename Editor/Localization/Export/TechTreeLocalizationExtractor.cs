using System.Collections.Generic;
using Ksp2UnityTools.Editor.TechTreeAuthoring;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a tech tree: each node's name and description, and the name of every named tier.
    /// </summary>
    public static class TechTreeLocalizationExtractor
    {
        /// <summary>
        /// Extracts the localization keys a tech tree bakes, with the English text the tree holds for each.
        /// </summary>
        /// <param name="tree">The tree to scan.</param>
        /// <returns>The collected localization key entries, empty when the tree is missing.</returns>
        public static List<LocalizationKeyEntry> Extract(TechTreeAsset tree)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (tree == null)
                return entries;

            string layer = TechTreeBake.GetLayer(tree);
            string assetPath = AssetDatabase.GetAssetPath(tree);
            foreach (TechTreeNode node in tree.Nodes)
            {
                string id = node.Data?.ID;
                if (string.IsNullOrEmpty(id))
                    continue;

                string sourceHint = LocalizationSourceHint.Format("TechNode", id, assetPath);
                entries.Add(new LocalizationKeyEntry(
                    TechTreeKeys.GetNameKey(layer, id),
                    node.EnglishName ?? string.Empty,
                    $"Tech node name for {id}",
                    sourceHint));
                entries.Add(new LocalizationKeyEntry(
                    TechTreeKeys.GetDescriptionKey(layer, id),
                    node.EnglishDescription ?? string.Empty,
                    $"Tech node description for {id}",
                    sourceHint));
            }

            // An unnamed tier is shown by its number, so it has no key to export
            for (int i = 0; i < tree.Tiers.Count; i++)
            {
                if (string.IsNullOrEmpty(tree.Tiers[i].EnglishName))
                    continue;

                entries.Add(new LocalizationKeyEntry(
                    TechTreeKeys.GetTierNameKey(layer, i + 1),
                    tree.Tiers[i].EnglishName,
                    $"Name of tier {i + 1} in the R&D Center",
                    LocalizationSourceHint.Format("TechTreeTier", (i + 1).ToString(), assetPath)));
            }

            return entries;
        }
    }
}
