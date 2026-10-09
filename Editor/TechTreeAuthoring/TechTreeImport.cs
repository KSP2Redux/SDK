using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Localization.CsvIO;
using Redux.Packs;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Builds a <see cref="TechTreeAsset" /> from tech node JSON, for bringing an existing tree under the editor once.
    /// </summary>
    public static class TechTreeImport
    {
        private const string KEY_COLUMN = "Key";

        private const string ENGLISH_COLUMN = "English";

        /// <summary>
        /// Builds a tree from the JSON of its nodes.
        /// </summary>
        /// <remarks>
        /// Text that is not a tech node is skipped. The tree takes the layer most of its nodes share, and each node's
        /// English text is looked up by the keys it already has.
        /// </remarks>
        /// <param name="nodeJson">The JSON of each node.</param>
        /// <param name="englishByKey">Gets the English text for a localization key, or null when it has none.</param>
        /// <returns>The new tree, not yet saved as an asset.</returns>
        public static TechTreeAsset FromJson(IEnumerable<string> nodeJson, Func<string, string> englishByKey)
        {
            var tree = ScriptableObject.CreateInstance<TechTreeAsset>();
            foreach (string json in nodeJson)
            {
                if (!TechTreeJson.TryFromJson(json, out TechNodeData data))
                    continue;

                tree.Nodes.Add(new TechTreeNode
                {
                    Data = data,
                    EnglishName = englishByKey(data.NameLocKey),
                    EnglishDescription = englishByKey(data.DescriptionLocKey),
                });
            }

            tree.Layer = tree.Nodes
                .GroupBy(node => string.IsNullOrEmpty(node.Data.Layer) ? CampaignPack.DEFAULT_LAYER : node.Data.Layer)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Key)
                .FirstOrDefault() ?? CampaignPack.DEFAULT_LAYER;
            return tree;
        }

        /// <summary>
        /// Reads the English text of every localization CSV in the project, keyed by localization key.
        /// </summary>
        /// <remarks>
        /// Only CSVs with a Key column and an English column count. When two files hold the same key, the first read
        /// wins.
        /// </remarks>
        /// <returns>The English text by key.</returns>
        public static Dictionary<string, string> ReadProjectEnglish()
        {
            var english = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
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

            return english;
        }
    }
}
