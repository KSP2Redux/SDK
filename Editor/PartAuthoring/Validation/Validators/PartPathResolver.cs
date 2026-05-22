using KSP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators
{
    /// <summary>
    /// Resolves the on-disk prefab asset path for a <see cref="CorePartData" /> across the three
    /// editor representations: prefab-stage contents root, in-project asset, and scene instance.
    /// </summary>
    /// <remarks>
    /// No single Unity API covers all three contexts on its own. The chain is documented in
    /// memory feedback-prefab-asset-identity-three-apis. Validators that need cross-prefab
    /// identity (self-skip in duplicate scans, baked-asset mtime checks, etc.) consume this.
    /// </remarks>
    internal static class PartPathResolver
    {
        /// <summary>
        /// Returns the underlying prefab asset path for <paramref name="part" /> across all
        /// editor contexts, or empty string when the part has no resolvable on-disk asset.
        /// </summary>
        public static string ResolvePrefabPath(CorePartData part)
        {
            if (part == null || part.gameObject == null)
            {
                return string.Empty;
            }
            GameObject go = part.gameObject;

            // Prefab stage edit: GameObject is the contents root of an open prefab stage.
            PrefabStage stage = PrefabStageUtility.GetPrefabStage(go);
            if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
            {
                return stage.assetPath;
            }

            // In-project asset: component loaded directly from disk.
            string assetPath = AssetDatabase.GetAssetPath(part);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }

            // Scene instance: GameObject is a prefab-instance placed in a scene.
            string instancePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return instancePath ?? string.Empty;
        }
    }
}
