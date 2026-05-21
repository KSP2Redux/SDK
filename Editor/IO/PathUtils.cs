using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.IO
{
    public class PathUtils
    {
        /// <summary>
        /// Find the prefab or asset path for the given target object (for example component) and GameObject.
        /// </summary>
        public static string? GetPrefabOrAssetPath(Object target, GameObject go)
        {
            // if the selected object is an actual asset, this works
            string? path = AssetDatabase.GetAssetPath(target);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }
    
            // Editing a prefab in Prefab Mode (isolated or in-context). IsPartOfPrefabContents
            // covers both modes; transform.root would only match isolated mode because in-context
            // parents the prefab under a hidden environment root.
            PrefabStage? stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && go != null && stage.IsPartOfPrefabContents(go))
            {
                return stage.assetPath;
            }
    
            // if this is a prefab instance in a scene, ask PrefabUtility for the prefab asset path
            if (go != null)
            {
                string? prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    return prefabPath;
                }
            }
    
            return null;
        }
    }
}
