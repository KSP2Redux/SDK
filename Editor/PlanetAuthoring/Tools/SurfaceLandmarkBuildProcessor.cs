using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Build-time scene processor that strips <see cref="Authoring.SurfaceLandmark" /> wrappers
    /// from scenes before they ship in addressable bundles or player builds.
    /// </summary>
    /// <remarks>
    /// Fires for every scene Unity processes during a build (player builds and addressable bundle
    /// builds via the Scriptable Build Pipeline). The scene Unity hands us is a temporary copy, so
    /// the flatten is non-destructive to the source the artist is editing. <paramref name="report" />
    /// is null when the call is a play-mode scene load rather than an actual build, in which case
    /// we skip the flatten so wrappers stay live during edit-and-test.
    /// </remarks>
    internal sealed class SurfaceLandmarkBuildProcessor : IProcessSceneWithReport
    {
        /// <inheritdoc />
        public int callbackOrder => 0;

        /// <inheritdoc />
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            SurfaceLandmarkFlattener.FlattenScene(scene);
        }
    }
}
