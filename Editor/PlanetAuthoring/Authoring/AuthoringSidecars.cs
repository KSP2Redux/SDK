using System.Collections.Generic;
using System.IO;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Convention-based lookup for editor-only authoring sidecars.
    /// </summary>
    /// <remarks>
    /// Each sidecar is a standalone <c>.asset</c> stored under a <c>Data/</c> subfolder next to the runtime asset it shadows.
    /// For a runtime asset at <c>&lt;dir&gt;/&lt;name&gt;.asset</c>, the sidecar lives at
    /// <c>&lt;dir&gt;/Data/&lt;name&gt;_&lt;TypeName&gt;.asset</c>. The helper creates the <c>Data/</c> folder on demand.
    /// Replaces the legacy single-file <c>PlanetAuthoringRegistry</c>.
    /// </remarks>
    public static class AuthoringSidecars
    {
        private const string DataFolderName = "Data";

        /// <summary>Returns the PQSData sidecar for <paramref name="pqsData" />, creating one if absent.</summary>
        public static PQSDataAuthoring GetOrCreate(PQSData pqsData) =>
            GetOrCreateSidecar<PQSDataAuthoring>(pqsData);

        /// <summary>Looks up the PQSData sidecar for <paramref name="pqsData" />, or null when absent.</summary>
        public static PQSDataAuthoring Find(PQSData pqsData) =>
            FindSidecar<PQSDataAuthoring>(pqsData);

        /// <summary>Returns the decal-controller bake-state sidecar for <paramref name="pqsDecalData" />, creating one if absent.</summary>
        public static PQSDecalControllerAuthoring GetOrCreate(PQSDecalData pqsDecalData) =>
            GetOrCreateSidecar<PQSDecalControllerAuthoring>(pqsDecalData);

        /// <summary>Looks up the decal-controller bake-state sidecar for <paramref name="pqsDecalData" />, or null when absent.</summary>
        public static PQSDecalControllerAuthoring Find(PQSDecalData pqsDecalData) =>
            FindSidecar<PQSDecalControllerAuthoring>(pqsDecalData);

        /// <summary>Returns the science-region sidecar for <paramref name="scienceRegionData" />, creating one if absent.</summary>
        public static ScienceRegionAuthoring GetOrCreate(ScienceRegionData scienceRegionData) =>
            GetOrCreateSidecar<ScienceRegionAuthoring>(scienceRegionData);

        /// <summary>Looks up the science-region sidecar for <paramref name="scienceRegionData" />, or null when absent.</summary>
        public static ScienceRegionAuthoring Find(ScienceRegionData scienceRegionData) =>
            FindSidecar<ScienceRegionAuthoring>(scienceRegionData);

        /// <summary>Returns the decal-template sidecar for <paramref name="decal" />, creating one if absent.</summary>
        public static PQSDecalTemplateAuthoring GetOrCreate(PQSDecal decal) =>
            GetOrCreateSidecar<PQSDecalTemplateAuthoring>(decal);

        /// <summary>Looks up the decal-template sidecar for <paramref name="decal" />, or null when absent.</summary>
        public static PQSDecalTemplateAuthoring Find(PQSDecal decal) =>
            FindSidecar<PQSDecalTemplateAuthoring>(decal);

        /// <summary>Enumerates every <see cref="PQSDataAuthoring" /> sidecar in the project.</summary>
        public static IEnumerable<PQSDataAuthoring> AllPQSDataAuthorings() => FindAllOfType<PQSDataAuthoring>();

        /// <summary>
        /// Returns the path where a sidecar of <typeparamref name="T" /> would live for <paramref name="runtimeAsset" />,
        /// or null when the asset has no AssetDatabase path.
        /// </summary>
        public static string GetSidecarPath<T>(Object runtimeAsset) where T : ScriptableObject
        {
            string runtimePath = AssetDatabase.GetAssetPath(runtimeAsset);
            if (string.IsNullOrEmpty(runtimePath)) return null;
            string dir = Path.GetDirectoryName(runtimePath)?.Replace('\\', '/');
            string baseName = Path.GetFileNameWithoutExtension(runtimePath);
            if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName)) return null;
            return $"{dir}/{DataFolderName}/{baseName}_{typeof(T).Name}.asset";
        }

        /// <summary>
        /// Returns the runtime asset whose sidecar is <paramref name="sidecar" />, by reversing the
        /// <see cref="GetSidecarPath{T}" /> convention. Null when the sidecar's path doesn't match the convention.
        /// </summary>
        public static T GetRuntimeAsset<T>(ScriptableObject sidecar) where T : Object
        {
            if (sidecar == null) return null;
            string sidecarPath = AssetDatabase.GetAssetPath(sidecar);
            if (string.IsNullOrEmpty(sidecarPath)) return null;
            string suffix = "_" + sidecar.GetType().Name + ".asset";
            if (!sidecarPath.EndsWith(suffix)) return null;
            string dir = Path.GetDirectoryName(sidecarPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(dir) || !dir.EndsWith("/" + DataFolderName)) return null;
            string runtimeDir = dir.Substring(0, dir.Length - DataFolderName.Length - 1);
            string baseName = Path.GetFileName(sidecarPath.Substring(0, sidecarPath.Length - suffix.Length));
            return AssetDatabase.LoadAssetAtPath<T>($"{runtimeDir}/{baseName}.asset");
        }

        private static T GetOrCreateSidecar<T>(Object runtimeAsset) where T : ScriptableObject
        {
            string sidecarPath = GetSidecarPath<T>(runtimeAsset);
            if (string.IsNullOrEmpty(sidecarPath)) return null;
            var existing = AssetDatabase.LoadAssetAtPath<T>(sidecarPath);
            if (existing != null) return existing;
            EnsureFolderExists(Path.GetDirectoryName(sidecarPath)?.Replace('\\', '/'));
            var sidecar = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(sidecar, sidecarPath);
            return sidecar;
        }

        private static T FindSidecar<T>(Object runtimeAsset) where T : ScriptableObject
        {
            string sidecarPath = GetSidecarPath<T>(runtimeAsset);
            if (string.IsNullOrEmpty(sidecarPath)) return null;
            return AssetDatabase.LoadAssetAtPath<T>(sidecarPath);
        }

        private static IEnumerable<T> FindAllOfType<T>() where T : ScriptableObject
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).FullName);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) yield return asset;
            }
        }

        private static void EnsureFolderExists(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string leaf = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolderExists(parent);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
