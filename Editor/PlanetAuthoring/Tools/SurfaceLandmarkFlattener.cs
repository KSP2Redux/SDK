using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Removes <see cref="SurfaceLandmark" /> wrapper GameObjects, re-parenting their managed
    /// children to the wrapper's parent so the runtime hierarchy is left intact.
    /// </summary>
    /// <remarks>
    /// Called by <see cref="SurfaceLandmarkBuildProcessor" /> at bundle-build time to strip
    /// editor-only wrappers from outgoing scenes. Operates on a temporary scene copy during the
    /// build, so the source scene the artist is editing is never modified.
    /// </remarks>
    internal static class SurfaceLandmarkFlattener
    {
        /// <summary>
        /// Flattens every <see cref="SurfaceLandmark" /> in <paramref name="scene" /> in place.
        /// </summary>
        /// <param name="scene">The scene to mutate.</param>
        /// <returns>The number of landmarks that were flattened.</returns>
        public static int FlattenScene(Scene scene)
        {
            if (!scene.IsValid()) return 0;
            var count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root == null) continue;
                var landmarks = root.GetComponentsInChildren<SurfaceLandmark>(true);
                foreach (var landmark in landmarks)
                {
                    if (landmark == null) continue;
                    Flatten(landmark);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Re-parents <paramref name="landmark" />'s children to its parent and destroys the
        /// landmark GameObject. World transforms are preserved.
        /// </summary>
        public static void Flatten(SurfaceLandmark landmark)
        {
            if (landmark == null) return;
            var t = landmark.transform;
            var parent = t.parent;
            // Snapshot first because re-parenting mutates the child collection mid-iteration.
            var children = new Transform[t.childCount];
            for (var i = 0; i < t.childCount; i++)
            {
                children[i] = t.GetChild(i);
            }
            foreach (var child in children)
            {
                child.SetParent(parent, worldPositionStays: true);
            }
            Object.DestroyImmediate(landmark.gameObject);
        }
    }
}
