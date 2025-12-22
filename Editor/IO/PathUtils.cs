using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.IO;

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

        // if we're editing a prefab in Prefab Mode, use the prefab stage asset path
        PrefabStage? stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.prefabContentsRoot != null)
        {
            if (go != null && go.transform.root == stage.prefabContentsRoot.transform)
            {
                return stage.assetPath;
            }
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