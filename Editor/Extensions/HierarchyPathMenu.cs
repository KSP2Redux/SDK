using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Extensions
{
    public static class HierarchyPathMenu
    {
        private const string CopyPathMenuItem = "GameObject/Copy Hierarchy Path";

        [MenuItem(CopyPathMenuItem, false, 0)]
        private static void CopyHierarchyPath()
        {
            Transform transform = Selection.activeTransform;
            if (transform == null)
            {
                return;
            }

            string path = GetHierarchyPath(transform);
            EditorGUIUtility.systemCopyBuffer = path;
            Debug.Log($"Copied hierarchy path: {path}");
        }

        [MenuItem(CopyPathMenuItem, true)]
        private static bool ValidateCopyHierarchyPath()
        {
            return Selection.transforms.Length == 1;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}
