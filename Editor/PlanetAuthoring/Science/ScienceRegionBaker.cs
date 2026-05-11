using System.IO;
using KSP.Game.Science;
using KSP.IO;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Science
{
    /// <summary>
    /// Encapsulates the Science Region bake step.
    /// </summary>
    /// <remarks>
    /// Writes three artifacts (regions JSON, discoverables JSON, and the baked region map asset)
    /// next to the source <see cref="ScienceRegionData" /> and registers them with addressables.
    /// Filenames are derived from the body name via canonical suffixes (no per-asset customization),
    /// matching the convention CoreCelestialBodyData and part data use: the artifact path is always
    /// "{asset folder}/{body name}_{suffix}". Addressables registration is delegated to
    /// <see cref="PlanetAuthoringAddressables.ResolveCelestialBodiesGroup" />.
    /// </remarks>
    internal static class ScienceRegionBaker
    {
        /// <summary>
        /// Filename suffix appended to the body name for the regions JSON artifact.
        /// </summary>
        public const string RegionsJsonSuffix = "_science_regions";

        /// <summary>
        /// Filename suffix appended to the body name for the discoverables JSON artifact.
        /// </summary>
        public const string DiscoverablesJsonSuffix = "_science_regions_discoverables";

        /// <summary>
        /// Filename suffix appended to the body name for the baked region map asset.
        /// </summary>
        public const string BakedMapSuffix = "_baked_science_regions";

        /// <summary>
        /// Composes the canonical filename stem (no extension) for one of the three bake artifacts.
        /// </summary>
        /// <param name="bodyName">The body name to lowercase and prefix.</param>
        /// <param name="suffix">The artifact suffix from this class.</param>
        /// <returns>The lowercased body name concatenated with the suffix.</returns>
        public static string ComposeFileStem(string bodyName, string suffix)
        {
            var lower = (bodyName ?? string.Empty).ToLowerInvariant();
            return lower + suffix;
        }

        /// <summary>
        /// Runs the full bake against <paramref name="data" />.
        /// </summary>
        /// <param name="data">The Science Region asset to bake.</param>
        /// <returns>The asset path of the baked map on success, or null on failure (missing source map, missing body name, etc.).</returns>
        public static string Bake(ScienceRegionData data)
        {
            if (data == null || data.information == null) return null;
            if (string.IsNullOrWhiteSpace(data.information.BodyName))
            {
                Debug.LogWarning("[ScienceRegionBaker] BodyName is empty. Cannot resolve filenames.");
                return null;
            }
            if (data.scienceRegionMap == null)
            {
                Debug.LogWarning("[ScienceRegionBaker] scienceRegionMap is null. Nothing to bake.");
                return null;
            }
            if (!data.scienceRegionMap.isReadable)
            {
                Debug.LogWarning($"[ScienceRegionBaker] Source texture '{data.scienceRegionMap.name}' is not Read/Write enabled. Bake skipped.");
                return null;
            }

            var regionsName = ComposeFileStem(data.information.BodyName, RegionsJsonSuffix);
            var discoverablesName = ComposeFileStem(data.information.BodyName, DiscoverablesJsonSuffix);
            var bakedMapName = ComposeFileStem(data.information.BodyName, BakedMapSuffix);

            var folder = ResolveFolder(data);
            var group = PlanetAuthoringAddressables.ResolveCelestialBodiesGroup(data);

            WriteRegionsJson(data, folder, regionsName, group);
            WriteDiscoverablesJson(data, folder, discoverablesName, group);
            var bakedAssetPath = WriteBakedMapAsset(data, folder, bakedMapName, group);

            // Stash the bake fingerprint so the SR_BAKED_DRIFT validator can detect when any input
            // changes after this bake. Sidecar is editor-only; runtime never reads it.
            var sidecar = PlanetAuthoringRegistry.Instance.GetOrCreateScienceRegion(data);
            if (sidecar != null)
            {
                sidecar.LastBakeFingerprint = ComputeFingerprint(data);
                EditorUtility.SetDirty(sidecar);
                AssetDatabase.SaveAssetIfDirty(sidecar);
            }

            AssetDatabase.Refresh();
            return bakedAssetPath;
        }

        /// <summary>
        /// Hashes the bake's input surface so the validator can detect drift without parsing or re-running the bake.
        /// </summary>
        /// <remarks>
        /// Combines the source map asset GUID, importer timestamp, and every region row's (Id, MapId, color)
        /// into a <see cref="Hash128" /> hex form so equality is fixed-size.
        /// </remarks>
        /// <param name="data">The Science Region asset whose inputs are hashed.</param>
        /// <returns>The hex form of the <see cref="Hash128" /> over the bake inputs.</returns>
        public static string ComputeFingerprint(ScienceRegionData data)
        {
            var hash = new Hash128();
            if (data.scienceRegionMap != null)
            {
                var mapPath = AssetDatabase.GetAssetPath(data.scienceRegionMap);
                hash.Append(AssetDatabase.AssetPathToGUID(mapPath));
                var importer = !string.IsNullOrEmpty(mapPath) ? AssetImporter.GetAtPath(mapPath) : null;
                if (importer != null)
                {
                    hash.Append((float)importer.assetTimeStamp);
                }
            }
            else
            {
                hash.Append("map:none");
            }
            var defs = data.information?.ScienceRegionDefinitions;
            if (defs != null)
            {
                foreach (var d in defs)
                {
                    if (d == null) continue;
                    hash.Append(d.Id ?? string.Empty);
                    hash.Append(d.MapId);
                    hash.Append(d.RegionColor.r);
                    hash.Append(d.RegionColor.g);
                    hash.Append(d.RegionColor.b);
                    hash.Append(d.RegionColor.a);
                }
            }
            return hash.ToString();
        }

        private static string ResolveFolder(ScienceRegionData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return "Assets";
            if (!string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            }
            return path;
        }

        private static void WriteRegionsJson(ScienceRegionData data, string folder, string fileStem, AddressableAssetGroup group)
        {
            var assetPath = $"{folder}/{fileStem}.json";
            var jsonData = JObject.Parse(IOProvider.ToJson(data.information)).ToString(Formatting.Indented);
            File.WriteAllText(assetPath, jsonData);
            AssetDatabase.ImportAsset(assetPath);
            if (group != null)
            {
                AddressablesTools.MakeAddressable(group, assetPath, $"{fileStem}.json", "science_region");
            }
        }

        private static void WriteDiscoverablesJson(ScienceRegionData data, string folder, string fileStem, AddressableAssetGroup group)
        {
            var payload = new CelestialBodyBakedDiscoverables
            {
                BodyName = data.information.BodyName,
                Version = data.information.Version,
                Discoverables = data.discoverables.ToArray(),
            };
            var assetPath = $"{folder}/{fileStem}.json";
            var jsonData = JObject.Parse(IOProvider.ToJson(payload)).ToString(Formatting.Indented);
            File.WriteAllText(assetPath, jsonData);
            AssetDatabase.ImportAsset(assetPath);
            if (group != null)
            {
                AddressablesTools.MakeAddressable(group, assetPath, $"{fileStem}.json", "science_region_discoverables");
            }
        }

        private static string WriteBakedMapAsset(ScienceRegionData data, string folder, string fileStem, AddressableAssetGroup group)
        {
            var regionMap = ScriptableObject.CreateInstance<CelestialBodyBakedScienceRegionMap>();
            regionMap.Width = data.scienceRegionMap.width;
            regionMap.Height = data.scienceRegionMap.height;
            regionMap.MapData = data.GetIndices();
            regionMap.BodyName = data.information.BodyName;

            var assetPath = $"{folder}/{fileStem}.asset";
            if (File.Exists(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.CreateAsset(regionMap, assetPath);
            if (group != null)
            {
                AddressablesTools.MakeAddressable(group, assetPath, fileStem, "science_region_map");
            }
            return assetPath;
        }
    }
}
