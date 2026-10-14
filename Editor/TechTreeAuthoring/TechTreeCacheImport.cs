using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PatchManager.Core.Cache;
using PatchManager.Core.Cache.Json;
using Redux.TechTree;
using ThunderKit.Core.Data;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// One feature set Patch Manager has patched for and cached, whose tech nodes the tech tree wizard can start from.
    /// </summary>
    public readonly struct TechTreeCacheSet
    {
        /// <summary>
        /// Creates a set.
        /// </summary>
        /// <param name="root">The cache folder the set is in.</param>
        /// <param name="setId">The set's id, its folder name.</param>
        /// <param name="features">The set's features, as its summary lists them.</param>
        /// <param name="lastWritten">When the set's inventory was last written.</param>
        /// <param name="archivePath">The path of the archive holding the set's patched tech nodes.</param>
        /// <param name="nodeNames">The names of the tech nodes in the archive.</param>
        /// <param name="campaign">The campaign that last saved with the set, or null when none is known.</param>
        public TechTreeCacheSet(
            string root,
            string setId,
            string features,
            DateTime lastWritten,
            string archivePath,
            IReadOnlyList<string> nodeNames,
            string campaign = null
        )
        {
            Root = root;
            SetId = setId;
            Features = features;
            LastWritten = lastWritten;
            ArchivePath = archivePath;
            NodeNames = nodeNames;
            Campaign = campaign;
        }

        /// <summary>
        /// Gets the cache folder the set is in.
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Gets the set's id.
        /// </summary>
        public string SetId { get; }

        /// <summary>
        /// Gets the set's features, comma separated, or empty when its summary is missing.
        /// </summary>
        public string Features { get; }

        /// <summary>
        /// Gets when the set's inventory was last written.
        /// </summary>
        public DateTime LastWritten { get; }

        /// <summary>
        /// Gets the path of the archive holding the set's patched tech nodes.
        /// </summary>
        public string ArchivePath { get; }

        /// <summary>
        /// Gets the names of the tech nodes in the archive.
        /// </summary>
        public IReadOnlyList<string> NodeNames { get; }

        /// <summary>
        /// Gets the campaign that last saved with the set, or null when none is known.
        /// </summary>
        public string Campaign { get; }

        /// <summary>
        /// Gets the set with the campaign that last saved with it.
        /// </summary>
        /// <param name="campaign">The campaign's name.</param>
        /// <returns>The set, naming the campaign.</returns>
        public TechTreeCacheSet WithCampaign(string campaign) =>
            new(Root, SetId, Features, LastWritten, ArchivePath, NodeNames, campaign);
    }

    /// <summary>
    /// Finds the feature sets in Patch Manager caches and reads their patched tech nodes, as the game loads them after
    /// every patch has run.
    /// </summary>
    /// <remarks>
    /// The cache is read with Patch Manager's own inventory and archive types. Its folder and file names mirror the
    /// private constants of Patch Manager's CacheManager and its summary log.
    /// </remarks>
    public static class TechTreeCacheImport
    {
        private const string CACHE_FOLDER = "pm_cache";

        private const string SETS_FOLDER = "sets";

        private const string ARCHIVES_FOLDER = "archives";

        private const string INVENTORY_FILENAME = "inventory.json";

        private const string SUMMARY_FILENAME = "summary.log";

        private const string FEATURES_PREFIX = "Features: ";

        private const string SAVE_METADATA_PATTERN = "*.meta";

        private const string GAME_COMPANY = "Intercept Games";

        private const string GAME_PRODUCT = "Kerbal Space Program 2";

        // The field of SerializedSaveGameMetadata a save records its set id in
        private const string FEATURE_SET_ID_FIELD = "FeatureSetId";

        /// <summary>
        /// Gets the caches worth looking in: the project's own, written when it plays, and the game install's.
        /// </summary>
        /// <returns>The cache folders that exist.</returns>
        public static List<string> GetCacheRoots()
        {
            var roots = new List<string> { Path.GetFullPath(CACHE_FOLDER) };
            string gamePath = ThunderKitSetting.GetOrCreateSettings<ThunderKitSettings>()?.GamePath;
            if (!string.IsNullOrEmpty(gamePath))
            {
                roots.Add(Path.GetFullPath(Path.Combine(gamePath, CACHE_FOLDER)));
            }

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).Where(Directory.Exists).ToList();
        }

        /// <summary>
        /// Finds every cached set with patched tech nodes in some caches.
        /// </summary>
        /// <param name="roots">The cache folders.</param>
        /// <returns>The sets, the most recently written first.</returns>
        public static List<TechTreeCacheSet> FindSets(IEnumerable<string> roots)
        {
            var sets = new List<TechTreeCacheSet>();
            foreach (string root in roots)
            {
                string setsFolder = Path.Combine(root, SETS_FOLDER);
                if (!Directory.Exists(setsFolder))
                    continue;

                foreach (string setFolder in Directory.GetDirectories(setsFolder))
                {
                    if (TryReadSet(root, setFolder, out TechTreeCacheSet set))
                    {
                        sets.Add(set);
                    }
                }
            }

            return sets.OrderByDescending(set => set.LastWritten).ToList();
        }

        /// <summary>
        /// Gets the folder the game keeps its single-player campaigns in.
        /// </summary>
        /// <remarks>
        /// The persistent data path is the platform's folder, then the company, then the product. A mod project names
        /// the last two after itself, so the game's are put in their place. That holds on Windows, on Linux and under
        /// Proton alike.
        /// </remarks>
        /// <returns>The folder, which may not exist.</returns>
        public static string GetSavesRoot()
        {
            string platformRoot = Path.GetDirectoryName(Path.GetDirectoryName(Application.persistentDataPath)) ?? string.Empty;
            return Path.Combine(platformRoot, GAME_COMPANY, GAME_PRODUCT, "Saves", "SinglePlayer");
        }

        /// <summary>
        /// Finds the cached sets campaigns play with, each named after the campaign that saved with it last.
        /// </summary>
        /// <remarks>
        /// A campaign plays the set its newest save recorded. A save from before set ids names none, so its campaign
        /// is left out, as is a set no campaign plays.
        /// </remarks>
        /// <param name="roots">The cache folders.</param>
        /// <param name="savesRoot">The folder of campaign folders.</param>
        /// <returns>One entry per set, the most recently saved campaign's first.</returns>
        public static List<TechTreeCacheSet> FindCampaignSets(IEnumerable<string> roots, string savesRoot)
        {
            Dictionary<string, (string Campaign, DateTime Saved)> campaigns = FindCampaignsBySet(savesRoot);

            // A set cached in both the project and the game install is listed once, as last written
            return FindSets(roots)
                .Where(set => campaigns.ContainsKey(set.SetId))
                .GroupBy(set => set.SetId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First().WithCampaign(campaigns[group.Key].Campaign))
                .OrderByDescending(set => campaigns[set.SetId].Saved)
                .ToList();
        }

        /// <summary>
        /// Checks whether a set's tech nodes are exactly some others, as when no patch in the set changes the tree.
        /// </summary>
        /// <param name="set">The set.</param>
        /// <param name="nodes">The nodes to compare with.</param>
        /// <returns>True if the set has the same nodes, none of them changed, false otherwise.</returns>
        public static bool HasNodes(TechTreeCacheSet set, IReadOnlyCollection<TechTreeNode> nodes)
        {
            List<TechTreeNode> cached = TechTreeReference.FromJson(ReadNodes(set).Values);
            if (cached.Count != nodes.Count)
                return false;

            Dictionary<string, TechTreeNode> byId = nodes.ToDictionary(node => node.Data.ID, StringComparer.Ordinal);
            return cached.All(node =>
                byId.TryGetValue(node.Data.ID, out TechTreeNode other) && TechTreePatchBake.GetChangedFields(other, node).Count == 0
            );
        }

        // Each set id a campaign last saved with, by the campaign that did so most recently
        private static Dictionary<string, (string Campaign, DateTime Saved)> FindCampaignsBySet(string savesRoot)
        {
            var bySet = new Dictionary<string, (string Campaign, DateTime Saved)>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(savesRoot))
                return bySet;

            foreach (string campaignFolder in Directory.GetDirectories(savesRoot))
            {
                string metaPath = Directory.EnumerateFiles(campaignFolder, SAVE_METADATA_PATTERN)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
                if (metaPath == null)
                    continue;

                string setId = ReadFeatureSetId(metaPath);
                if (string.IsNullOrEmpty(setId))
                    continue;

                DateTime saved = File.GetLastWriteTimeUtc(metaPath);
                if (!bySet.TryGetValue(setId, out (string Campaign, DateTime Saved) existing) || saved > existing.Saved)
                {
                    bySet[setId] = (Path.GetFileName(campaignFolder), saved);
                }
            }

            return bySet;
        }

        private static string ReadFeatureSetId(string metaPath)
        {
            try
            {
                return JObject.Parse(File.ReadAllText(metaPath))[FEATURE_SET_ID_FIELD]?.Value<string>();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Debug.LogWarning($"[TechTreeCacheImport] Skipping {metaPath}, which could not be read. {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads a set's patched tech nodes.
        /// </summary>
        /// <param name="set">The set.</param>
        /// <returns>Each node's JSON, by node name.</returns>
        public static Dictionary<string, string> ReadNodes(TechTreeCacheSet set)
        {
            var nodes = new Dictionary<string, string>(StringComparer.Ordinal);
            using var archive = new Archive(set.ArchivePath);
            foreach (string name in set.NodeNames)
            {
                nodes[name] = archive.ReadFile(name);
            }

            return nodes;
        }

        /// <summary>
        /// Reads a set's patched tree data.
        /// </summary>
        /// <remarks>
        /// The cache only holds the labels a patch in the set changed, so a set whose tree data no patch touched has
        /// none here, though the game still loads one.
        /// </remarks>
        /// <param name="set">The set.</param>
        /// <returns>The tree data's JSON, or null when the cache holds none for the set.</returns>
        public static string ReadTreeData(TechTreeCacheSet set)
        {
            string inventoryPath = Path.Combine(set.Root, SETS_FOLDER, set.SetId, INVENTORY_FILENAME);
            Inventory inventory;
            try
            {
                inventory = JsonConvert.DeserializeObject<Inventory>(File.ReadAllText(inventoryPath));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Debug.LogWarning($"[TechTreeCacheImport] Could not read {inventoryPath}. {ex.Message}");
                return null;
            }

            if (inventory?.CacheEntries == null
                || !inventory.CacheEntries.TryGetValue(TechTreeData.LABEL, out CacheEntry entry)
                || entry.Assets == null
                || entry.Assets.Count == 0)
            {
                return null;
            }

            string archivePath = Path.Combine(set.Root, ARCHIVES_FOLDER, entry.ArchiveFilename);
            if (!File.Exists(archivePath))
                return null;

            using var archive = new Archive(archivePath);
            return archive.ReadFile(entry.Assets[0]);
        }

        private static bool TryReadSet(string root, string setFolder, out TechTreeCacheSet set)
        {
            set = default;
            string inventoryPath = Path.Combine(setFolder, INVENTORY_FILENAME);
            if (!File.Exists(inventoryPath))
                return false;

            Inventory inventory;
            try
            {
                inventory = JsonConvert.DeserializeObject<Inventory>(File.ReadAllText(inventoryPath));
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                Debug.LogWarning($"[TechTreeCacheImport] Skipping {inventoryPath}, which could not be read. {ex.Message}");
                return false;
            }

            if (inventory?.CacheEntries == null || !inventory.CacheEntries.TryGetValue(TechTreeBake.NODE_LABEL, out CacheEntry entry))
                return false;

            string archivePath = Path.Combine(root, ARCHIVES_FOLDER, entry.ArchiveFilename);
            if (!File.Exists(archivePath))
                return false;

            set = new TechTreeCacheSet(
                root,
                Path.GetFileName(setFolder),
                ReadFeatures(Path.Combine(setFolder, SUMMARY_FILENAME)),
                File.GetLastWriteTime(inventoryPath),
                archivePath,
                entry.Assets ?? new List<string>()
            );
            return true;
        }

        // The summary's first line lists the set's features
        private static string ReadFeatures(string summaryPath)
        {
            if (!File.Exists(summaryPath))
                return string.Empty;

            string first = File.ReadLines(summaryPath).FirstOrDefault() ?? string.Empty;
            return first.StartsWith(FEATURES_PREFIX, StringComparison.Ordinal) ? first.Substring(FEATURES_PREFIX.Length) : string.Empty;
        }
    }
}
