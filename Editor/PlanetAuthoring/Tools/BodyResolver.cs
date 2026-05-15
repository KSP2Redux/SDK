using System.IO;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Locates the celestial body (CoreCelestialBodyData) and PQS for a given component in the
    /// sibling-scene-root authoring layout, where Scaled and Local prefabs are sibling scene roots
    /// rather than parent / child.
    /// </summary>
    /// <remarks>
    /// Authoring scenes hold exactly one celestial body, so the first body / PQS found in the scene
    /// is unambiguous.
    /// </remarks>
    public static class BodyResolver
    {
        /// <summary>
        /// Returns the body for the authoring scene that <paramref name="hint" /> belongs to.
        /// </summary>
        /// <param name="hint">Any component or scene object in the authoring scene.</param>
        /// <returns>The body, or null if none was found.</returns>
        public static CoreCelestialBodyData FindBody(Component hint)
        {
            if (hint == null) return null;
            // GetComponentInParent includes the hint itself, so this also covers hint-IS-body.
            var body = hint.GetComponentInParent<CoreCelestialBodyData>();
            return body != null ? body : FindBodyInScene(hint.gameObject);
        }

        /// <summary>
        /// Returns the PQS for the authoring scene that <paramref name="body" /> belongs to.
        /// </summary>
        /// <param name="body">The body whose scene to search.</param>
        /// <returns>The PQS, or null if none was found.</returns>
        public static PQS FindPqs(CoreCelestialBodyData body)
        {
            if (body == null) return null;
            var pqs = body.GetComponentInChildren<PQS>(true);
            return pqs != null ? pqs : FindPqsInScene(body.gameObject);
        }

        /// <summary>
        /// Returns the PQS for <paramref name="body" />, searching the scene first and falling back
        /// to loading the Local prefab from disk via <c>body.Data.assetKeySimulation</c>. Useful
        /// when the body is selected as a prefab asset (no scene open) or for editor operations
        /// that should work without an active authoring scene.
        /// </summary>
        /// <param name="body">The body to resolve a PQS for.</param>
        /// <returns>The PQS, or null if none was found.</returns>
        public static PQS FindPqsIncludingAsset(CoreCelestialBodyData body)
        {
            if (body == null) return null;
            var pqs = FindPqs(body);
            if (pqs != null) return pqs;
            var bodyAssetPath = AssetDatabase.GetAssetPath(body);
            if (string.IsNullOrEmpty(bodyAssetPath)) return null;
            var folder = Path.GetDirectoryName(bodyAssetPath)?.Replace('\\', '/');
            var simKey = body.Data?.assetKeySimulation;
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(simKey)) return null;
            var localPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{simKey}");
            return localPrefab != null ? localPrefab.GetComponentInChildren<PQS>(true) : null;
        }

        /// <summary>
        /// Returns the body for <paramref name="pqs" />, searching the scene first and falling back
        /// to loading the sibling Scaled prefab from disk by naming convention. Symmetric to
        /// <see cref="FindPqsIncludingAsset" />.
        /// </summary>
        /// <param name="pqs">The PQS to resolve a body for.</param>
        /// <returns>The body, or null if none was found.</returns>
        public static CoreCelestialBodyData FindBodyIncludingAsset(PQS pqs)
        {
            if (pqs == null) return null;
            var body = FindBody(pqs);
            if (body != null) return body;
            var pqsAssetPath = AssetDatabase.GetAssetPath(pqs);
            if (string.IsNullOrEmpty(pqsAssetPath)) return null;
            if (!pqsAssetPath.EndsWith(PlanetAuthoringNaming.LocalPrefabSuffix)) return null;
            var scaledPath = pqsAssetPath.Substring(0, pqsAssetPath.Length - PlanetAuthoringNaming.LocalPrefabSuffix.Length)
                + PlanetAuthoringNaming.ScaledPrefabSuffix;
            var scaledPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scaledPath);
            return scaledPrefab != null ? scaledPrefab.GetComponentInChildren<CoreCelestialBodyData>(true) : null;
        }

        /// <summary>
        /// Returns the body in the same scene as <paramref name="hint" />, scanning every scene root.
        /// </summary>
        /// <param name="hint">Any object in the scene to search.</param>
        /// <returns>The body, or null if none was found.</returns>
        public static CoreCelestialBodyData FindBodyInScene(GameObject hint) => FindInSceneRoots<CoreCelestialBodyData>(hint);

        /// <summary>
        /// Returns the PQS in the same scene as <paramref name="hint" />, scanning every scene root.
        /// </summary>
        /// <param name="hint">Any object in the scene to search.</param>
        /// <returns>The PQS, or null if none was found.</returns>
        public static PQS FindPqsInScene(GameObject hint) => FindInSceneRoots<PQS>(hint);

        private static T FindInSceneRoots<T>(GameObject hint) where T : Component
        {
            if (hint == null) return null;
            var scene = hint.scene;
            if (!scene.IsValid()) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
