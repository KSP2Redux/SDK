using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring
{
    /// <summary>
    /// Menu entries for the tech tree authoring workflow.
    /// </summary>
    public static class TechTreeAuthoringMenu
    {
        [MenuItem("Assets/Redux SDK/New Tech Tree", priority = KSP2UnityTools.MenuPriority)]
        private static void NewTechTree()
        {
            var tree = ScriptableObject.CreateInstance<TechTreeAsset>();
            ProjectWindowUtil.CreateAsset(tree, "NewTechTree.asset");
        }

        // Brings an existing tree of node JSON under the editor once. The tree asset is the source from then on.
        [MenuItem("Assets/Redux SDK/Import Tech Tree JSON...", priority = KSP2UnityTools.MenuPriority)]
        private static void ImportTechTreeJson()
        {
            string folder = EditorUtility.OpenFolderPanel("Folder of tech node JSON", Application.dataPath, "");
            if (string.IsNullOrEmpty(folder))
                return;

            string assetPath = EditorUtility.SaveFilePanelInProject(
                "Save tech tree",
                "TechTree",
                "asset",
                "Choose where to save the imported tech tree."
            );
            if (string.IsNullOrEmpty(assetPath))
                return;

            var english = TechTreeImport.ReadProjectEnglish();
            TechTreeAsset tree = TechTreeImport.FromJson(
                Directory.GetFiles(folder, "*.json", SearchOption.AllDirectories).Select(File.ReadAllText),
                key => key != null && english.TryGetValue(key, out string text) ? text : null
            );
            if (tree.Nodes.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Tech Tree JSON", $"No tech node JSON found in '{folder}'.", "OK");
                Object.DestroyImmediate(tree);
                return;
            }

            AssetDatabase.CreateAsset(tree, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = tree;
            Debug.Log($"[TechTreeImport] Imported {tree.Nodes.Count} nodes in layer '{tree.Layer}' to {assetPath}.");
        }
    }
}
