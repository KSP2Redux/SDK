using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Localization.CsvIO;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using Redux.TechTree;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Loads the nodes and tree data of a tree that an authored node is placed against, which the editor shows but
    /// never changes.
    /// </summary>
    /// <remarks>
    /// The source is a tech tree asset, a folder of tech node JSON such as another mod's baked tree, or every node the
    /// game's catalogs and the project's Addressables hold. English text comes from the project's localization CSVs,
    /// so nodes whose text the project lacks show by ID.
    /// </remarks>
    public static class TechTreeReference
    {
        // Redux's own nodes, the stock tree as Redux patches it
        private const string REDUX_NODES_LABEL = "feature:Ksp2Redux:Nodes";

        private const string KEY_COLUMN = "Key";

        private const string ENGLISH_COLUMN = "English";

        private const string CSV_EXTENSION = ".csv";

        private static Dictionary<string, string> _projectEnglish;

        /// <summary>
        /// Loads the tree an asset is authored against: the tree a node is placed in, or the tree a patch changes.
        /// </summary>
        /// <remarks>
        /// The asset's own baked files are left out of the game's nodes, and an asset is never its own reference.
        /// </remarks>
        /// <param name="asset">The asset.</param>
        /// <returns>The reference nodes, empty when the asset names no usable reference.</returns>
        public static List<TechTreeNode> Load(BaseReferencedTechTreeAsset asset)
        {
            if (asset.ReferenceSource == TechNodeReferenceSource.GameNodes)
                return LoadGameNodes(asset.BakedFiles);

            UnityEngine.Object reference = asset.Reference;
            if (reference != null && reference is TechTreeAsset tree && tree != asset)
                return tree.Nodes.Where(node => !string.IsNullOrEmpty(node.Data?.ID)).ToList();

            string folder = reference != null ? AssetDatabase.GetAssetPath(reference) : null;
            return !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder) ? LoadFolder(folder) : new List<TechTreeNode>();
        }

        /// <summary>
        /// Loads the nodes of a folder of tech node JSON, such as another mod's baked tree.
        /// </summary>
        /// <param name="folder">The folder, searched with its subfolders.</param>
        /// <returns>The nodes, the first of two with one ID kept.</returns>
        public static List<TechTreeNode> LoadFolder(string folder) =>
            FromJson(Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));

        /// <summary>
        /// Loads every tech node the project's own Addressables and the loaded catalogs hold.
        /// </summary>
        /// <remarks>
        /// The first copy of an ID wins, so the project's own nodes come first, then Redux's, which replace the game's
        /// own under the same IDs, then the rest.
        /// </remarks>
        /// <param name="excludedPaths">Project assets to leave out, such as an asset's own baked files.</param>
        /// <returns>The nodes.</returns>
        public static List<TechTreeNode> LoadGameNodes(ICollection<string> excludedPaths)
        {
            EditorPqsBootstrap.EnsureStockCatalog();
            List<string> json = AddressablesTools.GetProjectText(TechTreeBake.NODE_LABEL, excludedPaths);
            json.AddRange(AddressablesTools.GetCatalogText(REDUX_NODES_LABEL));
            json.AddRange(AddressablesTools.GetCatalogText(TechTreeBake.NODE_LABEL));
            return FromJson(json);
        }

        /// <summary>
        /// Loads the tree data of the tree an asset is authored against: its tiers and its start node.
        /// </summary>
        /// <remarks>
        /// A tech tree asset gives its own fields, a folder its tree data JSON, and the game's nodes the tree data the
        /// project's Addressables and the loaded catalogs hold. The asset's own baked files are left out.
        /// </remarks>
        /// <param name="asset">The asset.</param>
        /// <returns>The reference's tree data, or null when the reference has none.</returns>
        public static ReferenceTreeData LoadTreeData(BaseReferencedTechTreeAsset asset)
        {
            if (asset.ReferenceSource == TechNodeReferenceSource.GameNodes)
                return LoadGameTreeData(asset.BakedFiles);

            UnityEngine.Object reference = asset.Reference;
            if (reference != null && reference is TechTreeAsset tree && tree != asset)
                return ReferenceTreeData.FromTree(tree);

            string folder = reference != null ? AssetDatabase.GetAssetPath(reference) : null;
            return !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder) ? LoadTreeDataFolder(folder) : null;
        }

        /// <summary>
        /// Loads the tree data of a folder of tech tree JSON, such as another mod's baked tree.
        /// </summary>
        /// <param name="folder">The folder, searched with its subfolders for files named as a bake names tree data.</param>
        /// <returns>The first tree data found, or null when the folder has none.</returns>
        public static ReferenceTreeData LoadTreeDataFolder(string folder) =>
            TreeDataFromJson(Directory.GetFiles(folder, $"*{TechTreeBake.TREE_DATA_FILE_SUFFIX}", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));

        /// <summary>
        /// Loads the tree data the project's own Addressables and the loaded catalogs hold.
        /// </summary>
        /// <remarks>
        /// A feature set keeps one tree data, so the first found wins, the project's own first.
        /// </remarks>
        /// <param name="excludedPaths">Project assets to leave out, such as an asset's own baked files.</param>
        /// <returns>The tree data, or null when there is none.</returns>
        public static ReferenceTreeData LoadGameTreeData(ICollection<string> excludedPaths) =>
            TreeDataFromJson(LoadGameTreeDataJson(excludedPaths));

        /// <summary>
        /// Loads the JSON of every tree data the project's own Addressables and the loaded catalogs hold, keys as they
        /// are, the project's own first.
        /// </summary>
        /// <param name="excludedPaths">Project assets to leave out, such as an asset's own baked files.</param>
        /// <returns>Each tree data's JSON, the one a set loads first.</returns>
        public static List<string> LoadGameTreeDataJson(ICollection<string> excludedPaths)
        {
            EditorPqsBootstrap.EnsureStockCatalog();
            List<string> json = AddressablesTools.GetProjectText(TechTreeData.LABEL, excludedPaths);
            json.AddRange(AddressablesTools.GetCatalogText(TechTreeData.LABEL));
            return json;
        }

        /// <summary>
        /// Reads tree data from its JSON, with tier names in English from the project's localization CSVs.
        /// </summary>
        /// <remarks>
        /// A tier whose key the project has no English for shows its key, so it still reads as a named tier.
        /// </remarks>
        /// <param name="json">The JSON of each tree data. Text that is not JSON is skipped.</param>
        /// <returns>The first tree data read, or null when there is none.</returns>
        public static ReferenceTreeData TreeDataFromJson(IEnumerable<string> json)
        {
            foreach (string text in json)
            {
                TechTreeData data;
                try
                {
                    data = JsonUtility.FromJson<TechTreeData>(text);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (data == null)
                    continue;

                IReadOnlyDictionary<string, string> english = ReadProjectEnglish();
                return new ReferenceTreeData
                {
                    Tiers = (data.TierNameKeys ?? new List<string>())
                        .Select(key => new TechTreeTier { EnglishName = TierName(english, key) })
                        .ToList(),
                    StartNodeId = data.StartNodeId,
                };
            }

            return null;
        }

        private static string TierName(IReadOnlyDictionary<string, string> english, string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            return english.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text) ? text : key;
        }

        /// <summary>
        /// Reads tech nodes from their JSON, with English from the project's localization CSVs.
        /// </summary>
        /// <param name="json">Each node's JSON. Text that is not a tech node is skipped.</param>
        /// <returns>The nodes, the first of two with one ID kept, as the game keeps it.</returns>
        public static List<TechTreeNode> FromJson(IEnumerable<string> json)
        {
            IReadOnlyDictionary<string, string> english = ReadProjectEnglish();
            return FromJson(json, key => key != null && english.TryGetValue(key, out string text) ? text : null);
        }

        /// <summary>
        /// Reads tech nodes from their JSON, with English from a lookup.
        /// </summary>
        /// <param name="json">Each node's JSON. Text that is not a tech node is skipped.</param>
        /// <param name="englishByKey">Gets the English text for a localization key, or null when it has none.</param>
        /// <returns>The nodes, the first of two with one ID kept, as the game keeps it.</returns>
        internal static List<TechTreeNode> FromJson(IEnumerable<string> json, Func<string, string> englishByKey)
        {
            var nodes = new Dictionary<string, TechTreeNode>(StringComparer.Ordinal);
            foreach (string text in json)
            {
                if (!TechTreeJson.TryFromJson(text, out TechNodeData data) || nodes.ContainsKey(data.ID))
                    continue;

                nodes[data.ID] = new TechTreeNode
                {
                    Data = data,
                    EnglishName = englishByKey(data.NameLocKey),
                    EnglishDescription = englishByKey(data.DescriptionLocKey),
                };
            }

            return nodes.Values.ToList();
        }

        /// <summary>
        /// Reads the English text of every localization CSV in the project, keyed by localization key.
        /// </summary>
        /// <remarks>
        /// Only CSVs with a Key column and an English column count. When two files hold the same key, the first read
        /// wins. The result is kept until a CSV in the project is imported, moved or deleted.
        /// </remarks>
        /// <returns>The English text by key.</returns>
        public static IReadOnlyDictionary<string, string> ReadProjectEnglish()
        {
            if (_projectEnglish != null)
                return _projectEnglish;

            var english = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(CSV_EXTENSION, StringComparison.OrdinalIgnoreCase))
                    continue;

                LocCsvParseResult csv = LocCsvReader.Parse(File.ReadAllText(path), path);
                if (!csv.Columns.Any(column => column.Id == ENGLISH_COLUMN))
                    continue;

                foreach (var row in csv.Rows)
                {
                    string key = row.Get(KEY_COLUMN);
                    if (!string.IsNullOrEmpty(key) && !english.ContainsKey(key))
                    {
                        english[key] = row.Get(ENGLISH_COLUMN);
                    }
                }
            }

            _projectEnglish = english;
            return english;
        }

        // Forgets the English read so far whenever a localization CSV changes
        private class LocalizationCsvWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths
            )
            {
                if (importedAssets.Concat(deletedAssets).Concat(movedAssets).Concat(movedFromAssetPaths).Any(IsCsv))
                {
                    _projectEnglish = null;
                }
            }

            private static bool IsCsv(string path) => path.EndsWith(CSV_EXTENSION, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The tree data of a reference tree as the editor shows it: its tiers by English name and its start node.
    /// </summary>
    /// <remarks>
    /// A patch keeps one as the baseline its own tiers and start node are compared with, as it keeps a baseline of
    /// each node it changes.
    /// </remarks>
    [Serializable]
    public class ReferenceTreeData
    {
        /// <summary>
        /// The tiers, the first tier first. A tier without an English name is shown by its number.
        /// </summary>
        public List<TechTreeTier> Tiers = new();

        /// <summary>
        /// The ID of the node the R&amp;D Center focuses on when it first opens, or empty for the game's own.
        /// </summary>
        public string StartNodeId;

        /// <summary>
        /// Copies the tree data, so the copy shares nothing with the original.
        /// </summary>
        /// <returns>The copy.</returns>
        public ReferenceTreeData Copy() => new() { Tiers = CopyTiers(Tiers), StartNodeId = StartNodeId };

        /// <summary>
        /// Copies a list of tiers, so the copy shares nothing with the original.
        /// </summary>
        /// <param name="tiers">The tiers to copy.</param>
        /// <returns>The copy.</returns>
        public static List<TechTreeTier> CopyTiers(IEnumerable<TechTreeTier> tiers) =>
            tiers.Select(tier => new TechTreeTier { EnglishName = tier?.EnglishName ?? string.Empty }).ToList();

        /// <summary>
        /// Gets a tree asset's tiers and start node as tree data, sharing nothing with the asset.
        /// </summary>
        /// <param name="tree">The tree.</param>
        /// <returns>The tree data, or null when the tree has no tiers and no start node, so bakes none.</returns>
        public static ReferenceTreeData FromTree(TechTreeAsset tree) =>
            tree.Tiers.Count == 0 && string.IsNullOrEmpty(tree.StartNodeId)
                ? null
                : new ReferenceTreeData { Tiers = CopyTiers(tree.Tiers), StartNodeId = tree.StartNodeId };
    }
}
