using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Writes a tech tree's baked files into the project and registers them with Addressables.
    /// </summary>
    public static class TechTreeBaker
    {
        /// <summary>
        /// Bakes a tree beside its asset, registers every file it writes, and removes the files of the last bake that
        /// this one no longer writes.
        /// </summary>
        /// <param name="tree">The tree to bake.</param>
        /// <returns>True if the tree baked, false if a problem stopped it.</returns>
        public static bool Bake(TechTreeAsset tree)
        {
            string assetPath = AssetDatabase.GetAssetPath(tree);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Bake JSON", "Save the tech tree as an asset before baking it.", "OK");
                return false;
            }

            List<string> problems = TechTreeBake.FindBlockingProblems(tree);
            if (problems.Count > 0)
            {
                EditorUtility.DisplayDialog("Bake JSON", "Fix these first:\n\n" + string.Join("\n", problems), "OK");
                return false;
            }

            string folder = TechTreeBake.GetOutputFolder(assetPath);
            Directory.CreateDirectory(folder);
            AddressableAssetGroup group = TechTreeAuthoringAddressables.ResolveGroup(tree);
            if (group == null)
            {
                Debug.LogWarning(
                    $"[TechTreeBaker] No tech node Addressables group found for {assetPath}, so the baked files are not registered."
                );
            }

            var written = new List<string>();
            foreach (TechTreeBakeOutput output in TechTreeBake.BuildOutputs(tree))
            {
                string path = $"{folder}/{output.FileName}";
                File.WriteAllText(path, output.Json);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (group != null)
                {
                    AddressablesTools.MakeAddressable(group, path, output.FileName, output.Label);
                }

                written.Add(path);
            }

            // The files of nodes removed or renamed since the last bake would otherwise stay in the game
            foreach (string stale in tree.BakedFiles.Except(written))
            {
                AssetDatabase.DeleteAsset(stale);
            }

            Undo.RecordObject(tree, "Bake Tech Tree");
            tree.BakedFiles = written;
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TechTreeBaker] Baked {written.Count} file(s) for {assetPath} into {folder}.");
            return true;
        }
    }
}
