using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
using Redux.Packs;
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
    /// Every node is written into the tree's layer with keys derived from its ID. Files are named
    /// <c>{Layer}_{ID}</c> because Patch Manager keys a label's assets by file name and keeps only one of two with
    /// the same name, so a tree that reuses a stock ID must never ship a file called by that ID alone.
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
        /// Gets the tree's layer, an empty layer counting as the default one.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The layer the tree bakes into.</returns>
        public static string GetLayer(TechTreeAsset tree) =>
            string.IsNullOrEmpty(tree.Layer) ? CampaignPack.DEFAULT_LAYER : tree.Layer;

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
        /// <returns>The files to write.</returns>
        public static List<TechTreeBakeOutput> BuildOutputs(TechTreeAsset tree)
        {
            string layer = GetLayer(tree);
            var outputs = new List<TechTreeBakeOutput>();
            foreach (TechTreeNode node in tree.Nodes)
            {
                // A copy, so the bake never changes the asset
                if (!TechTreeJson.TryFromJson(TechTreeJson.ToJson(node.Data), out TechNodeData data))
                    continue;

                data.Layer = layer;
                data.NameLocKey = TechTreeKeys.GetNameKey(layer, data.ID);
                data.DescriptionLocKey = TechTreeKeys.GetDescriptionKey(layer, data.ID);
                outputs.Add(new TechTreeBakeOutput($"{layer}_{data.ID}.json", TechTreeJson.ToJson(data), NODE_LABEL));
            }

            if (tree.Tiers.Count == 0)
                return outputs;

            // A tier without a name gets an empty key, which the game shows by the tier's number
            var treeData = new TechTreeData
            {
                Layer = layer,
                TierNameKeys = tree.Tiers
                    .Select((tier, index) => string.IsNullOrEmpty(tier.EnglishName)
                        ? string.Empty
                        : TechTreeKeys.GetTierNameKey(layer, index + 1))
                    .ToList(),
            };
            outputs.Add(new TechTreeBakeOutput($"{layer}_TechTree.json", JsonUtility.ToJson(treeData, true), TechTreeData.LABEL));
            return outputs;
        }
    }
}
