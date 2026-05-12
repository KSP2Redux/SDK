using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Bidirectional cache between addressable prefab assets and their primary keys.
    /// </summary>
    /// <remarks>
    /// Used by the surface-prefab inspector, placement tool, and preview sync. All three need to
    /// resolve a prefab to its addressable key (for storage on <see cref="PrefabSpawner.prefabName" />)
    /// or back from a key to the prefab (for inspector display and scene preview). Caches both
    /// directions per session and is invalidated by <see cref="Authoring.AuthoringSidecarBootstrap" />
    /// on any asset postprocess pass.
    /// </remarks>
    internal static class AddressableKeyLookup
    {
        private static readonly Dictionary<GameObject, string> KeyByPrefab = new();
        private static readonly Dictionary<string, GameObject> PrefabByKey = new();

        /// <summary>
        /// Drops every cached lookup. Called by the AssetPostprocessor on any asset movement.
        /// </summary>
        public static void InvalidateCaches()
        {
            KeyByPrefab.Clear();
            PrefabByKey.Clear();
        }

        /// <summary>
        /// Returns the addressable primary key for <paramref name="prefab" />, or null when the prefab
        /// has no entry in any addressables group.
        /// </summary>
        /// <param name="prefab">The prefab asset to look up.</param>
        /// <returns>The primary key string, or null when not addressable.</returns>
        public static string GetKey(GameObject prefab)
        {
            if (prefab == null) return null;
            if (KeyByPrefab.TryGetValue(prefab, out var cached)) return cached;
            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path)) return null;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) return null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings != null ? settings.FindAssetEntry(guid) : null;
            var key = entry?.address;
            KeyByPrefab[prefab] = key;
            if (!string.IsNullOrEmpty(key))
            {
                PrefabByKey[key] = prefab;
            }
            return key;
        }

        /// <summary>
        /// Returns the prefab asset whose addressable primary key equals <paramref name="key" />, or
        /// null when no entry has that address.
        /// </summary>
        /// <param name="key">The addressable primary key as stored on <see cref="PrefabSpawner.prefabName" />.</param>
        /// <returns>The matching prefab asset, or null.</returns>
        public static GameObject GetPrefab(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (PrefabByKey.TryGetValue(key, out var cached) && cached != null) return cached;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;
                    if (entry.address != key) continue;
                    var path = AssetDatabase.GUIDToAssetPath(entry.guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null)
                    {
                        PrefabByKey[key] = prefab;
                        KeyByPrefab[prefab] = key;
                    }
                    return prefab;
                }
            }
            return null;
        }
    }
}
