using KSP;
using UnityEditor;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor
{
    /// <summary>
    /// Loads the shared KSP2UnityTools editor stylesheet (and any per-window overlays) onto a root
    /// <see cref="VisualElement" /> in one call.
    /// </summary>
    /// <remarks>
    /// Every inspector and editor window that ships with the KSP2UnityTools SDK calls
    /// <see cref="Apply" /> from its <c>CreateGUI</c> / <c>CreateInspectorGUI</c> to pick up unified
    /// theming. The shared sheet at <c>/Assets/Windows/Ksp2UnityToolsStyles.uss</c> is loaded first;
    /// window-specific overrides are layered on top in argument order.
    /// </remarks>
    public static class Ksp2UnityToolsStyles
    {
        private const string CommonStyleSheetPath = "/Assets/Windows/Ksp2UnityToolsStyles.uss";

        /// <summary>
        /// Loads the shared stylesheet onto <paramref name="root" />, then layers any per-window
        /// overrides supplied via <paramref name="additionalStyleSheetPaths" /> on top. Paths are
        /// resolved relative to <see cref="SDKConfiguration.BasePath" />.
        /// </summary>
        public static void Apply(VisualElement root, params string[] additionalStyleSheetPaths)
        {
            if (root == null)
                return;

            var common = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + CommonStyleSheetPath);
            if (common != null)
                root.styleSheets.Add(common);

            if (additionalStyleSheetPaths == null)
                return;

            foreach (string path in additionalStyleSheetPaths)
            {
                if (string.IsNullOrEmpty(path))
                    continue;
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SDKConfiguration.BasePath + path);
                if (sheet != null)
                    root.styleSheets.Add(sheet);
            }
        }
    }
}
