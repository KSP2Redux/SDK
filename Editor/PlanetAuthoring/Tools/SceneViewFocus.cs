using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Helper for reliably focusing the SceneView from the activation path of a place/pick tool.
    /// </summary>
    /// <remarks>
    /// Calling <c>EditorWindow.Focus()</c> synchronously from a button click in an inspector or
    /// custom window often loses focus immediately because the originating window's GUI processing
    /// re-claims focus when the click event finishes. Deferring the focus to the next editor frame
    /// via <see cref="EditorApplication.delayCall" /> lets the click finish first, after which the
    /// focus change sticks. Also opens a SceneView if none is around so the focus has somewhere to
    /// land.
    /// </remarks>
    internal static class SceneViewFocus
    {
        /// <summary>Defers a focus of the SceneView to the next editor frame.</summary>
        public static void FocusNextFrame()
        {
            EditorApplication.delayCall += FocusNow;
        }

        private static void FocusNow()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                // No SceneView was active recently. Try to find any open SceneView; only create one
                // if Unity has truly never opened one this session.
                var existing = Resources.FindObjectsOfTypeAll<SceneView>();
                sv = existing != null && existing.Length > 0 ? existing[0] : null;
            }
            sv?.Focus();
        }
    }
}
