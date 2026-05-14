using System;
using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Rendering.Planets;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Redux.CelestialBody;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Migration
{
    /// <summary>
    /// One-shot migrator that converts legacy wrapper-based celestial bodies to the new
    /// scaled-root architecture where the Scaled prefab carries CoreCelestialBodyData.
    /// </summary>
    /// <remarks>
    /// Legacy layout has Celestial.&lt;Key&gt;.prefab as the wrapper holding CoreCelestialBodyData,
    /// with Scaled and Local as transform children in the authoring scene. The new layout drops
    /// the wrapper, moves CoreCelestialBodyData onto Celestial.&lt;Key&gt;.Scaled.prefab, and leaves
    /// the Scaled and Local prefab instances as sibling scene roots so neither can accidentally
    /// nest into the other's prefab asset.
    /// </remarks>
    public static class ScaledRootMigrator
    {
        [MenuItem("Assets/KSP2 Unity Tools/Planet Authoring/Migrate Selected Body To Scaled-Root", priority = KSP2UnityTools.MenuPriority + 2)]
        public static void MigrateSelected()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string path = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Migrate body", "Select a body folder or a wrapper prefab (Celestial.<Key>.prefab) in the Project window.", "OK");
                return;
            }

            string wrapperPath = ResolveWrapperPath(path);
            if (wrapperPath == null)
            {
                EditorUtility.DisplayDialog("Migrate body", "Could not find a legacy wrapper prefab in: " + path, "OK");
                return;
            }

            try
            {
                if (MigrateOne(wrapperPath, out string summary))
                    EditorUtility.DisplayDialog("Migrated", summary, "OK");
                else
                    EditorUtility.DisplayDialog("Migration skipped", summary, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Migration failed", ex.Message, "OK");
            }
        }

        [MenuItem("Assets/KSP2 Unity Tools/Planet Authoring/Migrate All Bodies To Scaled-Root", priority = KSP2UnityTools.MenuPriority + 3)]
        public static void MigrateAll()
        {
            const string definitionsRoot = "Assets/ReduxAssets/Definitions/CelestialBodies";
            if (!AssetDatabase.IsValidFolder(definitionsRoot))
            {
                EditorUtility.DisplayDialog("Migrate all bodies", "Definitions folder not found: " + definitionsRoot, "OK");
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { definitionsRoot });
            var wrappers = new List<string>();
            foreach (var guid in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (IsWrapperPrefabPath(p))
                    wrappers.Add(p);
            }

            if (wrappers.Count == 0)
            {
                EditorUtility.DisplayDialog("Migrate all bodies", "No legacy wrapper prefabs found under " + definitionsRoot, "OK");
                return;
            }

            bool go = EditorUtility.DisplayDialog(
                "Migrate all bodies",
                $"Found {wrappers.Count} wrapper prefab(s) to migrate. Continue?\n\n" + string.Join("\n", wrappers),
                "Migrate",
                "Cancel"
            );
            if (!go) return;

            var report = new List<string>();
            int migrated = 0;
            foreach (var w in wrappers)
            {
                try
                {
                    if (MigrateOne(w, out string summary))
                    {
                        migrated++;
                        report.Add(summary);
                    }
                    else
                    {
                        report.Add(w + ": " + summary);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    report.Add($"{w}: FAILED - {ex.Message}");
                }
            }
            EditorUtility.DisplayDialog("Migration complete", $"Migrated {migrated}/{wrappers.Count}.\n\n" + string.Join("\n\n", report), "OK");
        }

        private static string ResolveWrapperPath(string selectionPath)
        {
            if (selectionPath.EndsWith(".prefab") && IsWrapperPrefabPath(selectionPath))
                return selectionPath;

            string folder = AssetDatabase.IsValidFolder(selectionPath)
                ? selectionPath
                : Path.GetDirectoryName(selectionPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder)) return null;

            foreach (var f in Directory.GetFiles(folder, "Celestial.*.prefab"))
            {
                var assetPath = f.Replace('\\', '/');
                if (IsWrapperPrefabPath(assetPath))
                    return assetPath;
            }
            return null;
        }

        // A wrapper prefab is Celestial.<Key>.prefab without the .Scaled or .Local suffix.
        private static bool IsWrapperPrefabPath(string assetPath)
        {
            var fileName = Path.GetFileName(assetPath);
            if (!fileName.StartsWith(PlanetAuthoringNaming.CelestialPrefix) || !fileName.EndsWith(".prefab")) return false;
            if (fileName.EndsWith(PlanetAuthoringNaming.ScaledPrefabSuffix) || fileName.EndsWith(PlanetAuthoringNaming.LocalPrefabSuffix)) return false;
            return true;
        }

        private enum SceneMigrationResult
        {
            /// <summary>No wrapper instance in the scene, nothing to do.</summary>
            NoChange,
            /// <summary>Scene was rewritten to the new layout.</summary>
            Migrated,
            /// <summary>Wrapper instance found but the migrator could not parse its contents. Caller must not delete the wrapper prefab asset.</summary>
            Failed,
        }

        private static bool MigrateOne(string wrapperPath, out string summary)
        {
            string folder = Path.GetDirectoryName(wrapperPath).Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(wrapperPath);
            string key = stem.Substring(PlanetAuthoringNaming.CelestialPrefix.Length);

            var wrapperPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wrapperPath);
            if (wrapperPrefab == null)
            {
                summary = $"{stem}: wrapper failed to load.";
                return false;
            }
            var wrapperCore = wrapperPrefab.GetComponent<CoreCelestialBodyData>();
            if (wrapperCore == null)
            {
                summary = $"{stem}: not a legacy wrapper (no CoreCelestialBodyData).";
                return false;
            }

            string scaledPath = $"{folder}/{PlanetAuthoringNaming.ScaledPrefab(key)}";
            var scaledPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(scaledPath);
            if (scaledPrefab == null)
            {
                summary = $"{stem}: no Scaled prefab at {scaledPath}.";
                return false;
            }

            CelestialBodyData legacyData = wrapperCore.Core?.data;

            // 1. Copy CelestialBodyData onto the Scaled prefab.
            var scaledContents = PrefabUtility.LoadPrefabContents(scaledPath);
            try
            {
                var scaledCore = scaledContents.GetComponent<CoreCelestialBodyData>();
                if (scaledCore == null)
                    scaledCore = scaledContents.AddComponent<CoreCelestialBodyData>();
                if (legacyData != null)
                    scaledCore.Core.data = legacyData;
                PrefabUtility.SaveAsPrefabAsset(scaledContents, scaledPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(scaledContents);
            }

            // 2. Migrate the authoring scene if present.
            string scenePath = $"{folder}/{stem}.unity";
            var sceneResult = File.Exists(scenePath) ? MigrateScene(scenePath, key) : SceneMigrationResult.NoChange;

            // 3. Delete the wrapper prefab asset only when the scene side either had nothing to
            // change or successfully migrated. A Failed result means the scene still references
            // the wrapper - deleting it now would leave a dangling Missing Prefab in the scene.
            if (sceneResult == SceneMigrationResult.Failed)
            {
                summary = $"{key}: Scaled prefab updated, but scene migration failed. Wrapper prefab kept to avoid dangling references; check the warning above for why the scene could not be parsed.";
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return false;
            }

            AssetDatabase.DeleteAsset(wrapperPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sceneNote = sceneResult == SceneMigrationResult.Migrated ? "; scene rewritten" : "";
            summary = $"{key}: Scaled prefab updated{sceneNote}; wrapper deleted.";
            return true;
        }

        private static SceneMigrationResult MigrateScene(string scenePath, string key)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject wrapperInstance = null;
                string wrapperName = $"{PlanetAuthoringNaming.CelestialPrefix}{key}";
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == wrapperName)
                    {
                        wrapperInstance = root;
                        break;
                    }
                }
                if (wrapperInstance == null)
                {
                    return SceneMigrationResult.NoChange;
                }

                GameObject scaledInstance = null;
                GameObject localInstance = null;
                var otherChildren = new List<GameObject>();
                foreach (Transform childTransform in wrapperInstance.transform)
                {
                    var child = childTransform.gameObject;
                    if (child.name == "Scaled")
                        scaledInstance = child;
                    else if (child.name == "Local")
                        localInstance = child;
                    else
                        otherChildren.Add(child);
                }

                if (scaledInstance == null)
                {
                    Debug.LogWarning($"[ScaledRootMigrator] {scenePath}: no child named 'Scaled' under wrapper '{wrapperName}'. Scene left untouched.");
                    return SceneMigrationResult.Failed;
                }

                // Promote Scaled and Local to sibling scene roots. Other children the artist had
                // under the wrapper get promoted to scene roots too so nothing is lost.
                scaledInstance.transform.SetParent(null, worldPositionStays: true);
                SceneManager.MoveGameObjectToScene(scaledInstance, scene);

                if (localInstance != null)
                {
                    localInstance.transform.SetParent(null, worldPositionStays: true);
                    SceneManager.MoveGameObjectToScene(localInstance, scene);
                }

                foreach (var child in otherChildren)
                {
                    child.transform.SetParent(null, worldPositionStays: true);
                    SceneManager.MoveGameObjectToScene(child, scene);
                }

                // Re-wire the decal controller's body reference to the new Scaled-side CoreCelestialBodyData
                // so the helper doesn't have to lazy-resolve it on the next session start.
                if (localInstance != null)
                {
                    var scaledCore = scaledInstance.GetComponent<CoreCelestialBodyData>();
                    var decalController = localInstance.GetComponentInChildren<PQSDecalController>(true);
                    if (decalController != null && scaledCore != null)
                    {
                        decalController.CoreCelestialBodyData = scaledCore;
                        EditorUtility.SetDirty(decalController);
                    }
                }

                UnityEngine.Object.DestroyImmediate(wrapperInstance, allowDestroyingAssets: false);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, scenePath);
                return SceneMigrationResult.Migrated;
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
