using System;
using System.IO;
using KSP;
using Ksp2UnityTools.Editor.IO;
using Redux.VFX.ReentryMeshGeneration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Tools
{
    /// <summary>
    /// Wraps the reentry-mesh generator for the part-authoring UI.
    /// </summary>
    /// <remarks>
    /// Exposes Validate / Bake / RemoveGenerated as three static methods that each return a
    /// human-readable status string for the inspector to display. The underlying
    /// <see cref="ReentryMeshGenerator" /> pipeline is unchanged.
    /// </remarks>
    public static class ReentryMeshBaker
    {
        /// <summary>
        /// Counts reentry-mesh components, generated mesh roots, and candidate source renderers on the part.
        /// </summary>
        /// <param name="target">The part to inspect.</param>
        /// <returns>A status line summarising the counts.</returns>
        public static string Validate(CorePartData target)
        {
            if (target == null)
            {
                return string.Empty;
            }
            GameObject root = target.gameObject;
            int reentryMeshCount = root.GetComponentsInChildren<KSP.VFX.Reentry.ReentryMesh>(true).Length;
            int generatedCount = root.GetComponentsInChildren<GeneratedReentryMeshRoot>(true).Length;
            int rendererCount = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer ||
                    renderer.GetComponentInParent<KSP.VFX.Reentry.ReentryMesh>(true) != null ||
                    renderer.GetComponentInParent<GeneratedReentryMeshRoot>(true) != null)
                {
                    continue;
                }
                rendererCount++;
            }
            return $"ReentryMesh components: {reentryMeshCount}. Generated roots: {generatedCount}. Candidate source renderers: {rendererCount}.";
        }

        /// <summary>
        /// Runs the reentry-mesh generator against the part and saves the generated meshes alongside the prefab.
        /// </summary>
        /// <param name="target">The part to bake.</param>
        /// <returns>A status line describing the generated groups, or a failure message.</returns>
        public static string Bake(CorePartData target)
        {
            return Bake(target, null, true);
        }

        /// <summary>
        /// Runs the reentry-mesh generator against the part and saves the generated meshes alongside the prefab.
        /// </summary>
        /// <param name="target">The part to bake.</param>
        /// <param name="prefabPathOverride">Optional prefab path for callers editing loaded prefab contents.</param>
        /// <param name="savePrefabChanges">When true, saves prefab hierarchy changes before returning.</param>
        /// <returns>A status line describing the generated groups, or a failure message.</returns>
        public static string Bake(CorePartData target, string prefabPathOverride, bool savePrefabChanges)
        {
            if (target == null)
            {
                return string.Empty;
            }
            if (string.IsNullOrWhiteSpace(prefabPathOverride) && TryGetPrefabAssetPath(target, out string assetPrefabPath))
            {
                return BakePrefabContents(assetPrefabPath);
            }

            string prefabPath = string.IsNullOrWhiteSpace(prefabPathOverride)
                ? PathUtils.GetPrefabOrAssetPath(target, target.gameObject)
                : prefabPathOverride;
            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Open or select a prefab-backed part to generate reentry meshes.";
            }

            var settings = ReentryMeshGenerationSettings.CreateDefault();
            settings.previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/ReduxAssets/Definitions/Parts/Common/ReentryMat.mat"
            );

            string partName = ResolvePartName(target);
            ReentryMeshGenerator.Result result;
            try
            {
                result = ReentryMeshGenerator.GenerateForPart(
                    target.gameObject,
                    partName,
                    settings,
                    true,
                    (progress, status) =>
                        EditorUtility.DisplayProgressBar($"Generating reentry meshes for {partName}", status, progress)
                );
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (!result.Success)
            {
                return "No reentry meshes were generated. " + string.Join(" ", result.Warnings);
            }

            try
            {
                EditorUtility.DisplayProgressBar(
                    $"Generating reentry meshes for {partName}", "Saving generated meshes", 0.95f);
                SaveGeneratedReentryMeshAssets(result, prefabPath, partName);
                if (savePrefabChanges)
                {
                    SavePrefabChanges(target.gameObject, prefabPath);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string status =
                $"Generated {result.Groups.Count} reentry groups from {result.SourceRendererCount} source renderers and {result.SourceVertexCount} source vertices.";
            if (result.Warnings.Count > 0)
            {
                status += " Warnings: " + string.Join(" ", result.Warnings);
            }
            return status;
        }

        /// <summary>
        /// Removes generated reentry-mesh roots from the part hierarchy and deletes the associated mesh assets.
        /// </summary>
        /// <param name="target">The part to clean up.</param>
        /// <returns>A status line confirming the removal.</returns>
        public static string RemoveGenerated(CorePartData target)
        {
            if (target == null)
            {
                return string.Empty;
            }
            if (TryGetPrefabAssetPath(target, out string assetPrefabPath))
            {
                return RemoveGeneratedFromPrefabContents(assetPrefabPath);
            }

            string prefabPath = PathUtils.GetPrefabOrAssetPath(target, target.gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                return "Open or select a prefab-backed part to remove generated reentry meshes.";
            }
            string partName = ResolvePartName(target);
            ReentryMeshGenerator.RemoveGenerated(target.gameObject);
            DeleteGeneratedReentryMeshAssets(prefabPath, partName);
            SavePrefabChanges(target.gameObject, prefabPath);
            return "Removed generated reentry mesh roots and generated mesh assets for this part.";
        }

        private static string BakePrefabContents(string prefabPath)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var core = contents.GetComponent<CorePartData>();
                if (core == null)
                {
                    return "Selected prefab does not contain CorePartData.";
                }

                string status = Bake(core, prefabPath, false);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                AssetDatabase.SaveAssets();
                return status;
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static string RemoveGeneratedFromPrefabContents(string prefabPath)
        {
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var core = contents.GetComponent<CorePartData>();
                if (core == null)
                {
                    return "Selected prefab does not contain CorePartData.";
                }

                string partName = ResolvePartName(core);
                ReentryMeshGenerator.RemoveGenerated(contents);
                DeleteGeneratedReentryMeshAssets(prefabPath, partName);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                AssetDatabase.SaveAssets();
                return "Removed generated reentry mesh roots and generated mesh assets for this part.";
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static bool TryGetPrefabAssetPath(CorePartData target, out string prefabPath)
        {
            prefabPath = null;
            if (target == null || target.gameObject == null || !EditorUtility.IsPersistent(target.gameObject))
            {
                return false;
            }

            prefabPath = AssetDatabase.GetAssetPath(target.gameObject);
            if (string.IsNullOrWhiteSpace(prefabPath))
            {
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target.gameObject);
            }

            return !string.IsNullOrWhiteSpace(prefabPath) &&
                   prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolvePartName(CorePartData target)
        {
            return !string.IsNullOrWhiteSpace(target.Core?.data?.partName)
                ? target.Core.data.partName
                : target.gameObject.name;
        }

        private static void SaveGeneratedReentryMeshAssets(
            ReentryMeshGenerator.Result result,
            string prefabPath,
            string partName
        )
        {
            string directory = Path.Combine(Path.GetDirectoryName(prefabPath), "ReentryMeshes").Replace('\\', '/');
            Directory.CreateDirectory(directory);

            foreach (ReentryMeshGenerator.GeneratedGroup group in result.Groups)
            {
                for (int i = 0; i < group.LodMeshes.Length; i++)
                {
                    Mesh mesh = group.LodMeshes[i];
                    string assetPath =
                        $"{directory}/{SanitizeAssetName(partName)}_{SanitizeAssetName(group.Name)}_lod{i}.asset";
                    AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(mesh, assetPath);
                    var savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                    if (savedMesh != null && group.LodRenderers[i] != null &&
                        group.LodRenderers[i].TryGetComponent(out MeshFilter meshFilter))
                    {
                        meshFilter.sharedMesh = savedMesh;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void DeleteGeneratedReentryMeshAssets(string prefabPath, string partName)
        {
            string directory = Path.Combine(Path.GetDirectoryName(prefabPath), "ReentryMeshes").Replace('\\', '/');
            if (!Directory.Exists(directory))
            {
                return;
            }
            foreach (string file in Directory.GetFiles(directory, $"{SanitizeAssetName(partName)}_*_lod*.asset"))
            {
                AssetDatabase.DeleteAsset(file.Replace('\\', '/'));
            }
            AssetDatabase.Refresh();
        }

        private static void SavePrefabChanges(GameObject targetObject, string prefabPath)
        {
            EditorUtility.SetDirty(targetObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(targetObject);
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot != null &&
                stage.IsPartOfPrefabContents(targetObject))
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, prefabPath);
                return;
            }
            if (PrefabUtility.IsPartOfPrefabAsset(targetObject))
            {
                PrefabUtility.SavePrefabAsset(targetObject.transform.root.gameObject);
            }
            AssetDatabase.SaveAssets();
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "part";
            }
            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0 || chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
