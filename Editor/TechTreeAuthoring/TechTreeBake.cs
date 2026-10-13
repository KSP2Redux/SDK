using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Modding;
using Redux.TechTree;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// One file a tech tree bake writes.
    /// </summary>
    public readonly struct TechTreeBakeOutput
    {
        /// <summary>
        /// Gets the file name, which is also the Addressables address.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Gets the file's JSON.
        /// </summary>
        public string Json { get; }

        /// <summary>
        /// Gets the Addressables label the game loads the file by.
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// Creates a bake output.
        /// </summary>
        /// <param name="fileName">The file name and address.</param>
        /// <param name="json">The file's JSON.</param>
        /// <param name="label">The Addressables label.</param>
        public TechTreeBakeOutput(string fileName, string json, string label)
        {
            FileName = fileName;
            Json = json;
            Label = label;
        }
    }

    /// <summary>
    /// Works out what a tech tree bakes to, without touching the project.
    /// </summary>
    /// <remarks>
    /// Every node is written with keys derived from its owning mod and its ID, into a file named by its ID, the way
    /// Redux's own nodes are. Two enabled nodes with one ID are an error in game, so a tree that reuses a stock ID
    /// belongs to a feature that turns the stock tree off.
    /// </remarks>
    public static class TechTreeBake
    {
        /// <summary>
        /// The Addressables label the game loads tech nodes by.
        /// </summary>
        public const string NODE_LABEL = "techNodeData";

        /// <summary>
        /// Gets the folder a tree bakes into, beside the tree asset.
        /// </summary>
        /// <param name="assetPath">The tree asset's path.</param>
        /// <returns>The folder's asset path.</returns>
        public static string GetOutputFolder(string assetPath) =>
            $"{Path.GetDirectoryName(assetPath)?.Replace('\\', '/')}/{Path.GetFileNameWithoutExtension(assetPath)}_Nodes";

        /// <summary>
        /// Gets the id of the mod a tree belongs to.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The id of the mod the tree's asset sits in, or Ksp2Redux's outside any mod.</returns>
        public static string GetOwnerId(TechTreeAsset tree)
        {
            Mod mod = KSP2UnityTools.FindParentMod(tree);
            return mod != null ? mod.id : FeatureCatalog.REDUX_MOD_ID;
        }

        /// <summary>
        /// Finds what stops a tree from baking.
        /// </summary>
        /// <param name="tree">The tree to check.</param>
        /// <returns>One message per problem, empty when the tree can bake.</returns>
        public static List<string> FindBlockingProblems(TechTreeAsset tree)
        {
            var problems = new List<string>();
            int unnamed = tree.Nodes.Count(node => string.IsNullOrEmpty(node.Data?.ID));
            if (unnamed > 0)
            {
                problems.Add($"{unnamed} node(s) have no ID.");
            }

            foreach (IGrouping<string, TechTreeNode> duplicate in tree.Nodes
                         .Where(node => !string.IsNullOrEmpty(node.Data?.ID))
                         .GroupBy(node => node.Data.ID)
                         .Where(group => group.Count() > 1))
            {
                problems.Add($"{duplicate.Key} is used by {duplicate.Count()} nodes.");
            }

            return problems;
        }

        /// <summary>
        /// Builds every file a tree bakes to: one per node, and the tree data when the tree has tiers.
        /// </summary>
        /// <param name="tree">The tree to bake.</param>
        /// <param name="ownerId">The id of the mod the tree belongs to, which scopes its keys.</param>
        /// <returns>The files to write.</returns>
        public static List<TechTreeBakeOutput> BuildOutputs(TechTreeAsset tree, string ownerId)
        {
            var outputs = new List<TechTreeBakeOutput>();
            foreach (TechTreeNode node in tree.Nodes)
            {
                // A copy, so the bake never changes the asset
                if (!TechTreeJson.TryFromJson(TechTreeJson.ToJson(node.Data), out TechNodeData data))
                    continue;

                data.NameLocKey = TechTreeKeys.GetNameKey(ownerId, data.ID);
                data.DescriptionLocKey = TechTreeKeys.GetDescriptionKey(ownerId, data.ID);
                outputs.Add(new TechTreeBakeOutput($"{data.ID}.json", TechTreeJson.ToJson(data), NODE_LABEL));
            }

            if (tree.Tiers.Count == 0)
                return outputs;

            // A tier without a name gets an empty key, which the game shows by the tier's number
            var treeData = new TechTreeData
            {
                TierNameKeys = tree.Tiers
                    .Select((tier, index) => string.IsNullOrEmpty(tier.EnglishName)
                        ? string.Empty
                        : TechTreeKeys.GetTierNameKey(ownerId, index + 1))
                    .ToList(),
            };
            outputs.Add(
                new TechTreeBakeOutput($"{tree.name}_TechTree.json", JsonUtility.ToJson(treeData, true), TechTreeData.LABEL)
            );
            return outputs;
        }
    }
}
