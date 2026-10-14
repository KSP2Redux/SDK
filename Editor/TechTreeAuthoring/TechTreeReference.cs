using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Loads the nodes of a tree that an authored node is placed against, which the editor shows but never changes.
    /// </summary>
    /// <remarks>
    /// The source is a tech tree asset, a folder of tech node JSON such as another mod's baked tree, or every node the
    /// game's catalogs and the project's Addressables hold. English text comes from the project's localization CSVs,
    /// so nodes whose text the project lacks show by ID.
    /// </remarks>
    public static class TechTreeReference
    {
        private const string NODE_LABEL = "techNodeData";

        // Redux's own nodes, the stock tree as Redux patches it
        private const string REDUX_NODES_LABEL = "feature:Ksp2Redux:Nodes";

        /// <summary>
        /// Loads a node asset's reference tree.
        /// </summary>
        /// <param name="asset">The node asset.</param>
        /// <returns>The reference nodes, empty when the asset names no usable reference.</returns>
        public static List<TechTreeNode> Load(TechNodeAsset asset) => Load(asset, asset.ReferenceSource, asset.Reference);

        /// <summary>
        /// Loads the tree a patch changes.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>The reference nodes, empty when the patch names no usable reference.</returns>
        public static List<TechTreeNode> Load(TechTreePatchAsset patch) => Load(patch, patch.ReferenceSource, patch.Reference);

        // The asset's own baked files are left out of the game's nodes, and an asset is never its own reference
        private static List<TechTreeNode> Load(TechTreeAsset self, TechNodeReferenceSource source, UnityEngine.Object reference)
        {
            if (source == TechNodeReferenceSource.GameNodes)
                return LoadGameNodes(self.BakedFiles);

            if (reference is TechTreeAsset tree && tree != self)
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
            var json = new List<string>();
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
            {
                json.AddRange(settings.groups
                    .Where(group => group != null)
                    .SelectMany(group => group.entries)
                    .Where(entry => entry.labels.Contains(NODE_LABEL) && !excludedPaths.Contains(entry.AssetPath))
                    .Select(entry => AssetDatabase.LoadAssetAtPath<TextAsset>(entry.AssetPath)?.text)
                    .Where(text => text != null));
            }

            json.AddRange(LoadCatalogText(REDUX_NODES_LABEL));
            json.AddRange(LoadCatalogText(NODE_LABEL));
            return FromJson(json);
        }

        private static List<string> LoadCatalogText(string label)
        {
            if (!Addressables.ResourceLocators.Any(locator => locator.Locate(label, typeof(TextAsset), out _)))
                return new List<string>();

            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(label, null);
                IList<TextAsset> assets = handle.WaitForCompletion();
                List<string> text = (assets ?? Array.Empty<TextAsset>()).Where(asset => asset != null).Select(asset => asset.text).ToList();
                Addressables.Release(handle);
                return text;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TechTreeReference] Could not load {label} from Addressables. {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Reads tech nodes from their JSON, with English from the project's localization CSVs.
        /// </summary>
        /// <param name="json">Each node's JSON. Text that is not a tech node is skipped.</param>
        /// <returns>The nodes, the first of two with one ID kept, as the game keeps it.</returns>
        public static List<TechTreeNode> FromJson(IEnumerable<string> json)
        {
            Dictionary<string, string> english = TechTreeImport.ReadProjectEnglish();
            var nodes = new Dictionary<string, TechTreeNode>(StringComparer.Ordinal);
            foreach (string text in json)
            {
                if (!TechTreeJson.TryFromJson(text, out TechNodeData data) || nodes.ContainsKey(data.ID))
                    continue;

                nodes[data.ID] = new TechTreeNode
                {
                    Data = data,
                    EnglishName = English(english, data.NameLocKey),
                    EnglishDescription = English(english, data.DescriptionLocKey),
                };
            }

            return nodes.Values.ToList();
        }

        private static string English(Dictionary<string, string> english, string key) =>
            key != null && english.TryGetValue(key, out string text) ? text : null;
    }
}
