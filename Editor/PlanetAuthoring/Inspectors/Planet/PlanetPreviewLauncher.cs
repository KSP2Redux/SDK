using System.IO;
using KSP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Starts and stops the planet preview for a body, and opens the body's authoring scene.
    /// </summary>
    public static class PlanetPreviewLauncher
    {
        /// <summary>
        /// Gets whether the running preview is of <paramref name="body" />, either the instance itself or its prefab.
        /// </summary>
        /// <param name="body">The body, a scene instance or a prefab asset.</param>
        /// <returns>True if the active session previews this body, false otherwise.</returns>
        public static bool IsPreviewing(CoreCelestialBodyData body)
        {
            PlanetAuthoringSession session = PlanetAuthoringSession.Active;
            if (session == null || body == null || session.Body == null)
                return false;

            return session.Body == body || session.Body.Core?.data?.bodyName == body.Core?.data?.bodyName;
        }

        /// <summary>
        /// Stops the preview of <paramref name="body" /> if it runs, otherwise starts one.
        /// </summary>
        /// <param name="body">The body, a scene instance or a prefab asset.</param>
        /// <returns>A message to show, or null when there is nothing to say.</returns>
        public static string Toggle(CoreCelestialBodyData body)
        {
            if (IsPreviewing(body))
            {
                PlanetAuthoringSession.Active.End();
                return null;
            }

            CoreCelestialBodyData sceneBody = OpenAuthoringScene(body);
            if (sceneBody == null)
                return "No authoring scene was found next to this body's prefab. Run Assets, Redux SDK, Planet Authoring, Create Authoring Scene For Selected Celestial Body on the Scaled prefab first.";

            PlanetAuthoringSession session = PlanetAuthoringSession.Begin(sceneBody);
            if (session != null && !EditorWindow.HasOpenInstances<Windows.PreviewControlsWindow>())
            {
                Windows.PreviewControlsWindow.ShowWindow();
            }

            return null;
        }

        /// <summary>
        /// Returns the scene instance of <paramref name="body" />, opening the authoring scene next to its prefab when
        /// the body is a prefab asset.
        /// </summary>
        /// <remarks>
        /// The scene opens additively so the scenes already open survive, rather than being closed with any unsaved
        /// changes they hold.
        /// </remarks>
        /// <param name="body">The body.</param>
        /// <returns>The scene instance, or null if the authoring scene is missing.</returns>
        public static CoreCelestialBodyData OpenAuthoringScene(CoreCelestialBodyData body)
        {
            if (body == null)
                return null;
            if (!PrefabUtility.IsPartOfPrefabAsset(body))
                return body;

            string prefabPath = AssetDatabase.GetAssetPath(body);
            if (string.IsNullOrEmpty(prefabPath))
                return null;

            string scenePath = AuthoringScenePath(prefabPath);
            if (!File.Exists(scenePath))
                return null;

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            string bodyName = body.Core?.data?.bodyName;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var candidate = root.GetComponentInChildren<CoreCelestialBodyData>(true);
                if (candidate != null && (string.IsNullOrEmpty(bodyName) || candidate.Core?.data?.bodyName == bodyName))
                    return candidate;
            }

            return null;
        }

        // The scene is Celestial.<Key>.unity, beside Celestial.<Key>.Scaled.prefab and Celestial.<Key>.Local.prefab.
        private static string AuthoringScenePath(string prefabPath)
        {
            string folder = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/');
            string fileName = Path.GetFileName(prefabPath);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            foreach (string suffix in new[] { PlanetAuthoringNaming.ScaledPrefabSuffix, PlanetAuthoringNaming.LocalPrefabSuffix })
            {
                if (fileName.EndsWith(suffix))
                {
                    stem = fileName.Substring(0, fileName.Length - suffix.Length);
                    break;
                }
            }

            return $"{folder}/{stem}.unity";
        }
    }
}
