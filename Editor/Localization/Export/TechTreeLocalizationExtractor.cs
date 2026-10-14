using System.Collections.Generic;
using Ksp2UnityTools.Editor.TechTreeAuthoring;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a tech tree: each node's name and description, and the name of every named tier.
    /// </summary>
    /// <remarks>
    /// A patch exports only the text it changes from the reference, since the rest keeps the reference's keys.
    /// </remarks>
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

            string ownerId = TechTreeBake.GetOwnerId(tree);
            string assetPath = AssetDatabase.GetAssetPath(tree);
            var patch = tree as TechTreePatchAsset;
            foreach (TechTreeNode node in tree.Nodes)
            {
                string id = node.Data?.ID;
                if (string.IsNullOrEmpty(id))
                    continue;

                // A patch's override keeps the reference's text, and its keys, unless the patch changes it
                TechTreeNode baseline = patch?.FindBaseline(id);
                List<string> changed = baseline != null ? TechTreePatchBake.GetChangedFields(baseline, node) : null;
                string sourceHint = LocalizationSourceHint.Format("TechNode", id, assetPath);
                if (changed == null || changed.Contains(TechTreePatchBake.NAME_FIELD))
                {
                    entries.Add(new LocalizationKeyEntry(
                        TechTreeKeys.GetNameKey(ownerId, id),
                        node.EnglishName ?? string.Empty,
                        $"Tech node name for {id}",
                        sourceHint));
                }

                if (changed == null || changed.Contains(TechTreePatchBake.DESCRIPTION_FIELD))
                {
                    entries.Add(new LocalizationKeyEntry(
                        TechTreeKeys.GetDescriptionKey(ownerId, id),
                        node.EnglishDescription ?? string.Empty,
                        $"Tech node description for {id}",
                        sourceHint));
                }
            }

            // An unnamed tier is shown by its number, so it has no key to export. A patch keeps the reference's keys
            // for the tiers it leaves alone.
            HashSet<int> changedTiers = patch != null ? new HashSet<int>(TechTreePatchBake.GetChangedTiers(patch)) : null;
            for (int i = 0; i < tree.Tiers.Count; i++)
            {
                if (string.IsNullOrEmpty(tree.Tiers[i].EnglishName) || (changedTiers != null && !changedTiers.Contains(i + 1)))
                    continue;

                entries.Add(new LocalizationKeyEntry(
                    TechTreeKeys.GetTierNameKey(ownerId, i + 1),
                    tree.Tiers[i].EnglishName,
                    $"Name of tier {i + 1} in the R&D Center",
                    LocalizationSourceHint.Format("TechTreeTier", (i + 1).ToString(), assetPath)));
            }

            return entries;
        }
    }
}
