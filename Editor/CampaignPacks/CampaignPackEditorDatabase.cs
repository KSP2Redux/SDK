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
        private const string DefaultGalaxyDefinitionKey = "GalaxyDefinition_Default";
        private const string DefinitionsRoot = "Assets/ReduxAssets/Definitions";
        private const string TechNodesFolder = DefinitionsRoot + "/TechNodes";
        private const string MissionsFolder = DefinitionsRoot + "/Missions";
        private const string ExperimentsFolder = DefinitionsRoot + "/Experiments";
        private const string CelestialBodiesFolder = DefinitionsRoot + "/CelestialBodies";

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
            Add(catalog.GalaxyKeys, DefaultGalaxyDefinitionKey);
            foreach (var pack in FindCampaignPacks()) Add(catalog.PackIds, pack.id);
            foreach (var set in FindTechTreeSets()) Add(catalog.TechTreeSetIds, set.id);
            foreach (var set in FindMissionSets()) Add(catalog.MissionSetIds, set.id);
            foreach (var set in FindScienceSets()) Add(catalog.ScienceSetIds, set.id);

            foreach (var path in AssetDatabase.FindAssets("t:TextAsset")
                         .Select(AssetDatabase.GUIDToAssetPath))
            {
                ReadTextAssetCatalogEntry(path, catalog);
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

        private static void ReadTextAssetCatalogEntry(string path, CampaignPackCatalog catalog)
        {
            if (Path.GetFileNameWithoutExtension(path).StartsWith("GalaxyDefinition_", StringComparison.Ordinal))
            {
                Add(catalog.GalaxyKeys, Path.GetFileNameWithoutExtension(path));
            }

            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return;
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

            if (IsUnderFolder(path, TechNodesFolder))
            {
                Add(catalog.TechNodeIds, ReadId(root));
                return;
            }

            if (IsUnderFolder(path, MissionsFolder))
            {
                Add(catalog.MissionIds, ReadId(root));
                return;
            }

            if (IsUnderFolder(path, ExperimentsFolder))
            {
                Add(catalog.ExperimentIds, ReadExperimentId(root));
                return;
            }

            if (IsUnderFolder(path, CelestialBodiesFolder))
            {
                ReadScienceCatalogEntry(root, catalog);
            }
        }

        private static void ReadScienceCatalogEntry(JObject root, CampaignPackCatalog catalog)
        {
            foreach (var region in root["Regions"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
            {
                Add(catalog.ScienceRegionIds, ReadId(region));
            }

            foreach (var discoverable in root["Discoverables"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
            {
                Add(catalog.DiscoverableIds, ReadId(discoverable));
            }
        }

        private static string? ReadId(JObject root)
        {
            return root.Value<string>("ID") ?? root.Value<string>("Id");
        }

        private static string? ReadExperimentId(JObject root)
        {
            return root.Value<string>("ExperimentID") ??
                root.SelectToken("data.ExperimentID")?.Value<string>() ??
                ReadId(root);
        }

        private static bool IsUnderFolder(string path, string folder)
        {
            return path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase);
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
