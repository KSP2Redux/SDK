using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.MissionAuthoring;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// AssetDatabase-backed lookup service for campaign pack authoring UI, autocomplete, and validation.
    /// </summary>
    public static class CampaignPackEditorDatabase
    {
        /// <summary>
        /// Finds all campaign pack assets in the Unity project.
        /// </summary>
        /// <returns>Loaded campaign pack assets.</returns>
        public static List<CampaignPack> FindCampaignPacks() => FindAssets<CampaignPack>();

        /// <summary>
        /// Finds all tech tree set assets in the Unity project.
        /// </summary>
        /// <returns>Loaded tech tree set assets.</returns>
        public static List<TechTreeSet> FindTechTreeSets() => FindAssets<TechTreeSet>();

        /// <summary>
        /// Finds all mission set assets in the Unity project.
        /// </summary>
        /// <returns>Loaded mission set assets.</returns>
        public static List<MissionSet> FindMissionSets() => FindAssets<MissionSet>();

        /// <summary>
        /// Finds all science set assets in the Unity project.
        /// </summary>
        /// <returns>Loaded science set assets.</returns>
        public static List<ScienceSet> FindScienceSets() => FindAssets<ScienceSet>();

        /// <summary>
        /// Finds all campaign pack extension assets in the Unity project.
        /// </summary>
        /// <returns>Loaded campaign pack extension assets.</returns>
        public static List<CampaignPackExtension> FindExtensions() => FindAssets<CampaignPackExtension>();

        /// <summary>
        /// Builds a catalog of known authoring and data identifiers for validation and autocomplete.
        /// </summary>
        /// <returns>Catalog populated from campaign pack assets, JSON text assets, and authored mission assets.</returns>
        public static CampaignPackCatalog BuildCatalog()
        {
            var catalog = new CampaignPackCatalog();
            foreach (var pack in FindCampaignPacks()) Add(catalog.PackIds, pack.id);
            foreach (var set in FindTechTreeSets()) Add(catalog.TechTreeSetIds, set.id);
            foreach (var set in FindMissionSets()) Add(catalog.MissionSetIds, set.id);
            foreach (var set in FindScienceSets()) Add(catalog.ScienceSetIds, set.id);

            foreach (var path in AssetDatabase.FindAssets("t:TextAsset")
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                ReadJsonCatalogEntry(path, catalog);
            }

            foreach (var mission in FindAssets<Mission>())
            {
                Add(catalog.MissionIds, mission.missionData?.ID);
            }

            return catalog;
        }

        /// <summary>
        /// Pings the first Unity asset found by searching for the supplied identifier.
        /// </summary>
        /// <param name="id">Identifier or search term to locate in the AssetDatabase.</param>
        public static void PingAssetForId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            foreach (var path in AssetDatabase.FindAssets(id)
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null) continue;
                EditorGUIUtility.PingObject(asset);
                Selection.activeObject = asset;
                return;
            }
        }

        private static List<T> FindAssets<T>() where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToList();
        }

        private static void ReadJsonCatalogEntry(string path, CampaignPackCatalog catalog)
        {
            if (Path.GetFileNameWithoutExtension(path).StartsWith("GalaxyDefinition_", StringComparison.Ordinal))
            {
                Add(catalog.GalaxyKeys, Path.GetFileNameWithoutExtension(path));
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(path));
            }
            catch
            {
                return;
            }

            var id = root.Value<string>("ID");
            if (string.IsNullOrWhiteSpace(id)) id = root.Value<string>("Id");

            if (path.IndexOf("TechNodes", StringComparison.OrdinalIgnoreCase) >= 0 ||
                root["RequiredSciencePoints"] != null ||
                root["UnlockedPartsIDs"] != null)
            {
                Add(catalog.TechNodeIds, id);
            }

            if (path.IndexOf("Missions", StringComparison.OrdinalIgnoreCase) >= 0 ||
                root["missionStages"] != null ||
                root["GameModeFeatureId"] != null)
            {
                Add(catalog.MissionIds, id);
            }

            var experimentId = root.Value<string>("ExperimentID") ??
                root.SelectToken("data.ExperimentID")?.Value<string>() ??
                id;
            if (path.IndexOf("Experiments", StringComparison.OrdinalIgnoreCase) >= 0 ||
                root["ExperimentID"] != null ||
                root.SelectToken("data.ExperimentID") != null)
            {
                Add(catalog.ExperimentIds, experimentId);
            }

            if (path.IndexOf("ScienceRegion", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("ScienceRegions", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Add(catalog.ScienceRegionIds, id ?? Path.GetFileNameWithoutExtension(path));
            }

            if (path.IndexOf("Discoverable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf("Discoverables", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Add(catalog.DiscoverableIds, id ?? Path.GetFileNameWithoutExtension(path));
            }
        }

        private static void Add(HashSet<string> values, string? value)
        {
            var normalized = value ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                values.Add(normalized);
            }
        }
    }
}
