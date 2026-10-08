using System.Collections.Generic;
using KSP.Rendering.Planets;
using Redux.CelestialBody;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Editor-side preview spawner that mirrors every legacy <see cref="PrefabSpawner" /> and every
    /// <see cref="ReduxSurfaceSpawner" /> on the active planet preview's PQS with a hidden,
    /// read-only instance of its assigned prefab.
    /// </summary>
    /// <remarks>
    /// At edit time the artist would otherwise see only an empty GameObject for each spawner
    /// because the runtime instantiates the prefab on altitude crossing or proximity gate. Each
    /// preview is parented to its spawner with HideAndDontSave so it never lands in scene
    /// serialization, and the set is rebuilt on hierarchy change and session change. Cleared when
    /// no body preview is active.
    /// </remarks>
    [InitializeOnLoad]
    internal static class SurfacePrefabPreviewSync
    {
        private static readonly Dictionary<EntityId, PreviewEntry> Previews = new();
        private static PQS _trackedPqs;

        private struct PreviewEntry
        {
            public string Key;
            public GameObject Instance;
        }

        static SurfacePrefabPreviewSync()
        {
            PlanetPreviewState.ActiveChanged += OnSessionChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        private static void OnSessionChanged()
        {
            ClearAll();
            _trackedPqs = PlanetAuthoringSession.Active?.Pqs;
            if (_trackedPqs != null) RefreshAll();
        }

        private static void OnHierarchyChanged()
        {
            if (_trackedPqs == null) return;
            RefreshAll();
        }

        /// <summary>
        /// Forces a full sync against the active session's spawners.
        /// </summary>
        /// <remarks>
        /// Called by <see cref="Inspectors.PrefabSpawnerEditor" /> when <c>prefabName</c> changes,
        /// since field edits don't trigger <see cref="EditorApplication.hierarchyChanged" />.
        /// </remarks>
        public static void Refresh()
        {
            if (_trackedPqs == null) return;
            RefreshAll();
        }

        private static void RefreshAll()
        {
            var seen = new HashSet<EntityId>();
            foreach (var spawner in _trackedPqs.GetComponentsInChildren<PrefabSpawner>(true))
            {
                if (spawner == null) continue;
                var id = spawner.GetEntityId();
                seen.Add(id);
                EnsurePreview(id, spawner.transform, spawner.prefabName);
            }
            foreach (var spawner in _trackedPqs.GetComponentsInChildren<ReduxSurfaceSpawner>(true))
            {
                if (spawner == null) continue;
                var id = spawner.GetEntityId();
                seen.Add(id);
                EnsurePreview(id, spawner.transform, spawner.AddressableKey);
            }
            // Drop previews for spawners that have gone away.
            var stale = new List<EntityId>();
            foreach (var id in Previews.Keys)
            {
                if (!seen.Contains(id)) stale.Add(id);
            }
            foreach (var id in stale)
            {
                ReleasePreview(id);
            }
        }

        private static void EnsurePreview(EntityId id, Transform parent, string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                ReleasePreview(id);
                return;
            }
            if (Previews.TryGetValue(id, out var entry) && entry.Key == key && entry.Instance != null)
            {
                return;
            }
            ReleasePreview(id);
            var prefab = AddressableKeyLookup.GetPrefab(key);
            if (prefab == null)
            {
                // Asset isn't editor-loadable (e.g. only present in a built bundle, or the key
                // is unresolved). Skip rather than error-spam.
                return;
            }
            var instance = Object.Instantiate(prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            SetHideFlagsRecursive(instance.transform, HideFlags.HideAndDontSave);
            Previews[id] = new PreviewEntry { Key = key, Instance = instance };
        }

        private static void ReleasePreview(EntityId id)
        {
            if (!Previews.TryGetValue(id, out var entry)) return;
            if (entry.Instance != null)
            {
                Object.DestroyImmediate(entry.Instance);
            }
            Previews.Remove(id);
        }

        private static void ClearAll()
        {
            foreach (var entry in Previews.Values)
            {
                if (entry.Instance != null)
                {
                    Object.DestroyImmediate(entry.Instance);
                }
            }
            Previews.Clear();
        }

        private static void SetHideFlagsRecursive(Transform t, HideFlags flags)
        {
            t.gameObject.hideFlags = flags;
            for (var i = 0; i < t.childCount; i++)
            {
                SetHideFlagsRecursive(t.GetChild(i), flags);
            }
        }
    }
}
