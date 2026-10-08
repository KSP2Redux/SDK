using KSP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Blocks entering play mode while a celestial body authoring scene is the active scene.
    /// </summary>
    /// <remarks>
    /// The runtime never loads authoring scenes, so play mode would boot the game into an invalid state.
    /// </remarks>
    [InitializeOnLoad]
    public static class CelestialBodyAuthoringSceneGuard
    {
        static CelestialBodyAuthoringSceneGuard()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode)
                return;

            Scene active = EditorSceneManager.GetActiveScene();
            if (!IsCelestialBodyAuthoringScene(active))
                return;

            EditorApplication.isPlaying = false;
            EditorUtility.DisplayDialog(
                "Play mode blocked",
                $"'{active.name}' is a celestial body authoring scene. It is editor-only and not playable. Open a real scene (e.g. boot-ksp) before entering play mode.",
                "OK"
            );
        }

        private static bool IsCelestialBodyAuthoringScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return false;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.GetComponentInChildren<CoreCelestialBodyData>(true) != null)
                    return true;
            }
            return false;
        }
    }
}
