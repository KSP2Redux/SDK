using System;
using System.Collections.Generic;
using System.IO;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Single editor-only registry for every authoring sidecar (decal templates, PQSData, decal
    /// controller bake-state, science region). Stored at <see cref="AssetPath" /> with each entry
    /// as a hidden sub-asset.
    /// </summary>
    /// <remarks>
    /// One authoring asset on disk for every sidecar type. Entries land in a single
    /// <c>List&lt;ScriptableObject&gt;</c> and are dispatched by runtime type with the
    /// type-specific GetOrCreate / Find methods. Keys (DecalID for templates, asset GUID for
    /// PQSData and PQSDecalData) are unique within their type.
    /// </remarks>
    public class PlanetAuthoringRegistry : ScriptableObject
    {
        // Lives under the SDK's project-local data folder (alongside KSP2UTData/JsonPaths.asset) so it does not ship with the SDK package itself - this is per-project authoring state.
        private const string AssetPath =
            "Assets/KSP2UTData/PlanetAuthoringRegistry.asset";

        [SerializeField] private List<ScriptableObject> _entries = new();

        private static PlanetAuthoringRegistry _instance;

        /// <summary>
        /// Gets the singleton registry asset, creating it on disk under the project's authoring data folder the first time it is requested.
        /// </summary>
        public static PlanetAuthoringRegistry Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = AssetDatabase.LoadAssetAtPath<PlanetAuthoringRegistry>(AssetPath);
                if (_instance != null) return _instance;
                var dir = Path.GetDirectoryName(AssetPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                _instance = CreateInstance<PlanetAuthoringRegistry>();
                AssetDatabase.CreateAsset(_instance, AssetPath);
                AssetDatabase.SaveAssets();
                return _instance;
            }
        }

        // --- Decal template sidecars (keyed by PQSDecal.DecalID) ---

        /// <summary>
        /// Returns the template sidecar for <paramref name="decalId" />, creating one if absent.
        /// </summary>
        /// <param name="decalId">The decal identifier from <see cref="PQSDecal.DecalID" />.</param>
        /// <returns>The existing or newly created template sidecar, or null if <paramref name="decalId" /> is null or empty.</returns>
        public PQSDecalTemplateAuthoring GetOrCreateDecalTemplate(string decalId) =>
            GetOrCreateSidecar<PQSDecalTemplateAuthoring>(decalId, t => t.DecalID, (t, k) => t.DecalID = k, "DecalTemplateAuthoring_");

        /// <summary>
        /// Looks up the template sidecar for <paramref name="decalId" />.
        /// </summary>
        /// <param name="decalId">The decal identifier from <see cref="PQSDecal.DecalID" />.</param>
        /// <returns>The template sidecar, or null if no entry is registered for <paramref name="decalId" />.</returns>
        public PQSDecalTemplateAuthoring FindDecalTemplate(string decalId) =>
            FindSidecar<PQSDecalTemplateAuthoring>(decalId, t => t.DecalID);

        // --- PQSData sidecars (keyed by PQSData asset GUID) ---

        /// <summary>
        /// Returns the PQSData sidecar for <paramref name="pqsData" />, creating one if absent.
        /// </summary>
        /// <remarks>
        /// Returns null if the asset has no GUID yet (unsaved).
        /// </remarks>
        /// <param name="pqsData">The PQSData asset to look up or create a sidecar for.</param>
        /// <returns>The existing or newly created sidecar, or null if <paramref name="pqsData" /> is null or has no asset GUID.</returns>
        public PQSDataAuthoring GetOrCreatePQSData(PQSData pqsData) =>
            GetOrCreateSidecar<PQSDataAuthoring>(TryGetGuid(pqsData), d => d.PQSDataGuid, (d, k) => d.PQSDataGuid = k, "PQSDataAuthoring_");

        /// <summary>
        /// Looks up the PQSData sidecar for <paramref name="pqsData" />.
        /// </summary>
        /// <param name="pqsData">The PQSData asset to look up.</param>
        /// <returns>The PQSData sidecar, or null if no entry is registered for <paramref name="pqsData" />.</returns>
        public PQSDataAuthoring FindPQSData(PQSData pqsData) =>
            FindSidecar<PQSDataAuthoring>(TryGetGuid(pqsData), d => d.PQSDataGuid);

        // --- Decal controller bake-state sidecars (keyed by PQSDecalData asset GUID) ---

        /// <summary>
        /// Returns the controller bake-state sidecar for <paramref name="pqsDecalData" />, creating one if absent.
        /// </summary>
        /// <remarks>
        /// Returns null if the asset has no GUID yet (unsaved).
        /// </remarks>
        /// <param name="pqsDecalData">The PQSDecalData asset that identifies the owning controller.</param>
        /// <returns>The existing or newly created bake-state sidecar, or null if <paramref name="pqsDecalData" /> is null or has no asset GUID.</returns>
        public PQSDecalControllerAuthoring GetOrCreateDecalController(PQSDecalData pqsDecalData) =>
            GetOrCreateSidecar<PQSDecalControllerAuthoring>(TryGetGuid(pqsDecalData), c => c.PqsDecalDataGuid, (c, k) => c.PqsDecalDataGuid = k, "DecalControllerAuthoring_");

        /// <summary>
        /// Looks up the controller bake-state sidecar for <paramref name="pqsDecalData" />.
        /// </summary>
        /// <param name="pqsDecalData">The PQSDecalData asset that identifies the owning controller.</param>
        /// <returns>The bake-state sidecar, or null if no entry is registered for <paramref name="pqsDecalData" />.</returns>
        public PQSDecalControllerAuthoring FindDecalController(PQSDecalData pqsDecalData) =>
            FindSidecar<PQSDecalControllerAuthoring>(TryGetGuid(pqsDecalData), c => c.PqsDecalDataGuid);

        // --- ScienceRegion sidecars (keyed by ScienceRegionData asset GUID) ---

        /// <summary>
        /// Returns the ScienceRegion sidecar for <paramref name="scienceRegionData" />, creating one if absent.
        /// </summary>
        /// <remarks>
        /// Returns null if the asset has no GUID yet (unsaved).
        /// </remarks>
        /// <param name="scienceRegionData">The ScienceRegionData asset to look up or create a sidecar for.</param>
        /// <returns>The existing or newly created sidecar, or null if <paramref name="scienceRegionData" /> is null or has no asset GUID.</returns>
        public ScienceRegionAuthoring GetOrCreateScienceRegion(ScienceRegionData scienceRegionData) =>
            GetOrCreateSidecar<ScienceRegionAuthoring>(TryGetGuid(scienceRegionData), s => s.ScienceRegionGuid, (s, k) => s.ScienceRegionGuid = k, "ScienceRegionAuthoring_");

        /// <summary>
        /// Looks up the ScienceRegion sidecar for <paramref name="scienceRegionData" />.
        /// </summary>
        /// <param name="scienceRegionData">The ScienceRegionData asset to look up.</param>
        /// <returns>The ScienceRegion sidecar, or null if no entry is registered for <paramref name="scienceRegionData" />.</returns>
        public ScienceRegionAuthoring FindScienceRegion(ScienceRegionData scienceRegionData) =>
            FindSidecar<ScienceRegionAuthoring>(TryGetGuid(scienceRegionData), s => s.ScienceRegionGuid);

        // --- Bulk operations ---

        /// <summary>
        /// Enumerates every <see cref="PQSDataAuthoring" /> sidecar currently registered.
        /// </summary>
        /// <remarks>
        /// Used by cross-body refresh paths that need to walk every body's sidecar to find ones
        /// referencing a shared asset (for example, <c>SmallLayerMaterialPostProcessor</c>
        /// recompiles bodies whose <see cref="SmallLayerMaterial" /> changed).
        /// </remarks>
        public IEnumerable<PQSDataAuthoring> EnumeratePQSDataAuthorings()
        {
            foreach (var entry in _entries)
            {
                if (entry is PQSDataAuthoring d)
                    yield return d;
            }
        }

        /// <summary>
        /// Removes every sidecar whose stored GUID no longer maps to an asset on disk. Called by
        /// <see cref="AuthoringSidecarBootstrap" /> after a postprocess pass that includes deletes.
        /// </summary>
        public void RemoveOrphanedSidecars()
        {
            PruneNulls();
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var guid = TryGetEntryGuid(_entries[i]);
                if (guid == null) continue; // DecalTemplateAuthoring keys by DecalID, not a GUID, so leave for explicit cleanup.
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    AssetDatabase.RemoveObjectFromAsset(_entries[i]);
                    DestroyImmediate(_entries[i], allowDestroyingAssets: true);
                    _entries.RemoveAt(i);
                }
            }
            EditorUtility.SetDirty(this);
        }

        // --- internals ---

        private T GetOrCreateSidecar<T>(string key, Func<T, string> getKey, Action<T, string> setKey, string namePrefix)
            where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(key)) return null;
            var existing = FindSidecar<T>(key, getKey);
            if (existing != null) return existing;
            var entry = CreateInstance<T>();
            entry.name = namePrefix + key;
            setKey(entry, key);
            AddSubAsset(entry);
            return entry;
        }

        private T FindSidecar<T>(string key, Func<T, string> getKey) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(key)) return null;
            foreach (var e in _entries)
            {
                if (e is T t && getKey(t) == key) return t;
            }
            return null;
        }

        // Pulls the GUID from whichever sidecar shape carries one. DecalTemplateAuthoring uses a
        // DecalID instead of a GUID and is excluded so RemoveOrphanedSidecars leaves it alone.
        private static string TryGetEntryGuid(ScriptableObject entry) => entry switch
        {
            PQSDataAuthoring d => d.PQSDataGuid,
            PQSDecalControllerAuthoring c => c.PqsDecalDataGuid,
            ScienceRegionAuthoring s => s.ScienceRegionGuid,
            _ => null,
        };

        // Adds the sub-asset and prunes any null entries that accumulated from external deletes.
        // Does NOT save the asset. Bootstrap batches one SaveAssets at the end of its postprocess
        // pass, and interactive callers can save on their own action paths.
        private void AddSubAsset(ScriptableObject entry)
        {
            PruneNulls();
            entry.hideFlags = HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(entry, this);
            _entries.Add(entry);
            EditorUtility.SetDirty(this);
        }

        private void PruneNulls()
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                if (_entries[i] == null)
                {
                    _entries.RemoveAt(i);
                }
            }
        }

        private static string TryGetGuid(UnityEngine.Object asset)
        {
            if (asset == null) return null;
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetDatabase.AssetPathToGUID(path);
        }
    }
}
