using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
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

        // The asset's own baked file is in the project's entries too, so it is left out
        private static List<TechTreeNode> LoadGameNodes(ICollection<string> excludedPaths)
        {
            var json = new List<string>();
            try
            {
                var handle = Addressables.LoadAssetsAsync<TextAsset>(NODE_LABEL, null);
                IList<TextAsset> assets = handle.WaitForCompletion();
                json.AddRange((assets ?? Array.Empty<TextAsset>()).Where(asset => asset != null).Select(asset => asset.text));
                Addressables.Release(handle);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TechTreeReference] Could not load {NODE_LABEL} from Addressables. {ex.Message}");
            }

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

            return FromJson(json);
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
