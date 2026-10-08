using System;
using System.Collections.Generic;
using System.IO;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Science
{
    /// <summary>
    /// Resolves Science Region authoring assets from a body name.
    /// </summary>
    /// <remarks>
    /// The runtime loads science data purely from addressables by body name, so the editor mirrors
    /// that key. Bake artifact paths are derived from the asset's folder and the body's lowercased
    /// name via the canonical suffixes in <see cref="ScienceRegionBaker" /> (no per-asset
    /// customization), so callers can find the bake output without any external state.
    /// </remarks>
    internal static class ScienceRegionAssetLocator
    {
        // Body-name -> data cache. AssetDatabase.FindAssets is too slow to run per validator tick
        // (we have ~9 validators that hit FindForBody every 500ms), so cache resolved entries and
        // invalidate via AssetPostprocessor when ScienceRegionData assets are imported or removed.
        private static readonly Dictionary<string, ScienceRegionData> Cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Drops every cached lookup.
        /// </summary>
        /// <remarks>Called by the AssetPostprocessor.</remarks>
        public static void InvalidateCache() => Cache.Clear();

        /// <summary>
        /// Finds the project's <see cref="ScienceRegionData" /> asset for the given body name.
        /// </summary>
        /// <remarks>Body name match is case-insensitive.</remarks>
        /// <param name="bodyName">The body name to look up.</param>
        /// <returns>The matching asset, or null if no asset matches.</returns>
        public static ScienceRegionData FindForBody(string bodyName)
        {
            if (string.IsNullOrWhiteSpace(bodyName)) return null;
            if (Cache.TryGetValue(bodyName, out var cached) &&
                cached != null && cached.information != null &&
                string.Equals(cached.information.BodyName, bodyName, StringComparison.OrdinalIgnoreCase))
            {
                return cached;
            }

            var guids = AssetDatabase.FindAssets($"t:{nameof(ScienceRegionData)}");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var data = AssetDatabase.LoadAssetAtPath<ScienceRegionData>(path);
                if (data == null || data.information == null) continue;
                if (string.Equals(data.information.BodyName, bodyName, StringComparison.OrdinalIgnoreCase))
                {
                    Cache[bodyName] = data;
                    return data;
                }
            }
            Cache.Remove(bodyName);
            return null;
        }

        /// <summary>
        /// Finds the baked region map asset associated with the given <see cref="ScienceRegionData" />.
        /// </summary>
        /// <remarks>
        /// The bake step writes the asset next to the source as
        /// <c>{folder}/{lower-body-name}{ScienceRegionBaker.BakedMapSuffix}.asset</c>.
        /// </remarks>
        /// <param name="data">The Science Region asset whose bake output is resolved.</param>
        /// <returns>The baked region map asset, or null if it has not been baked or cannot be found.</returns>
        public static CelestialBodyBakedScienceRegionMap FindBakedMap(ScienceRegionData data)
        {
            if (data == null || data.information == null || string.IsNullOrEmpty(data.information.BodyName)) return null;
            var sourcePath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(sourcePath)) return null;
            var folder = string.IsNullOrEmpty(Path.GetDirectoryName(sourcePath)) ? "Assets" : Path.GetDirectoryName(sourcePath);
            var fileStem = ScienceRegionBaker.ComposeFileStem(data.information.BodyName, ScienceRegionBaker.BakedMapSuffix);
            var assetPath = Path.Combine(folder, fileStem + ".asset").Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<CelestialBodyBakedScienceRegionMap>(assetPath);
        }

        /// <summary>
        /// Reports whether the source <see cref="ScienceRegionData.scienceRegionMap" /> has been modified more recently than the baked region asset.
        /// </summary>
        /// <remarks>True means the artist should re-bake before shipping.</remarks>
        /// <param name="data">The Science Region asset whose source map timestamp is checked.</param>
        /// <param name="bakedMap">The baked region map asset to compare against.</param>
        /// <returns>True if the source has been modified since the bake or the bake does not exist, false otherwise.</returns>
        public static bool IsBakeStale(ScienceRegionData data, CelestialBodyBakedScienceRegionMap bakedMap)
        {
            if (data == null || data.scienceRegionMap == null) return false;
            if (bakedMap == null) return true;
            var sourcePath = AssetDatabase.GetAssetPath(data.scienceRegionMap);
            var bakedPath = AssetDatabase.GetAssetPath(bakedMap);
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(bakedPath)) return false;
            var sourceFull = Path.GetFullPath(sourcePath);
            var bakedFull = Path.GetFullPath(bakedPath);
            if (!File.Exists(sourceFull) || !File.Exists(bakedFull)) return false;
            return File.GetLastWriteTimeUtc(sourceFull) > File.GetLastWriteTimeUtc(bakedFull);
        }
    }
}
