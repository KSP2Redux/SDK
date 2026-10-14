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
        public static List<TechTreeNode> Load(TechNodeAsset asset)
        {
            if (asset.ReferenceSource == TechNodeReferenceSource.GameNodes)
                return LoadGameNodes(asset.BakedFiles);

            if (asset.Reference is TechTreeAsset tree && tree != asset)
                return tree.Nodes.Where(node => !string.IsNullOrEmpty(node.Data?.ID)).ToList();

            string folder = asset.Reference != null ? AssetDatabase.GetAssetPath(asset.Reference) : null;
            return !string.IsNullOrEmpty(folder) && AssetDatabase.IsValidFolder(folder)
                ? FromJson(Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText))
                : new List<TechTreeNode>();
        }

        // The first copy of an ID wins, so the project's own nodes come first, then Redux's, which replace the game's
        // own under the same IDs, then the rest. The asset's own baked file is left out.
        private static List<TechTreeNode> LoadGameNodes(ICollection<string> excludedPaths)
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

        // Of two nodes with one ID the first is kept, as the game keeps it
        private static List<TechTreeNode> FromJson(IEnumerable<string> json)
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
