using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Menu registrations and shared constants for the Redux Planet Authoring window family.
    /// </summary>
    /// <remarks>
    /// Placeholder windows that have not been implemented yet show a uniform "not yet implemented"
    /// dialog. Implemented windows such as <see cref="PreviewControlsWindow" /> live in their own
    /// files and pull priority and menu-root constants from here.
    /// </remarks>
    internal static class PlanetAuthoringWindows
    {
        /// <summary>
        /// Root menu path under which every Planet Authoring window is registered.
        /// </summary>
        public const string MenuRoot = "Modding/Planet Authoring/";

        /// <summary>
        /// Title used by the shared placeholder dialog.
        /// </summary>
        public const string DialogTitle = "Planet Authoring";

        /// <summary>
        /// Menu priority for the Preview Controls window.
        /// </summary>
        public const int PriorityPreviewControls = 100;

        /// <summary>
        /// Menu priority for the Validation Report window.
        /// </summary>
        public const int PriorityValidationReport = 110;

        /// <summary>
        /// Menu priority for the Landmark Manager window.
        /// </summary>
        public const int PriorityLandmarkManager = 140;

        /// <summary>
        /// Menu priority for the Preset Browser window.
        /// </summary>
        public const int PriorityPresetBrowser = 160;

        /// <summary>
        /// Opens the combined Landmark Manager window (decals, prefabs, discoverables, landmarks).
        /// </summary>
        [MenuItem(MenuRoot + "Landmark Manager", priority = PriorityLandmarkManager)]
        public static void ShowLandmarkManager() => LandmarkManagerWindow.ShowWindow();

        /// <summary>
        /// Shows the placeholder dialog for the Preset Browser window.
        /// </summary>
        [MenuItem(MenuRoot + "Preset Browser", priority = PriorityPresetBrowser)]
        public static void ShowPresetBrowserPlaceholder() => ShowNotImplemented("Preset Browser");

        private static void ShowNotImplemented(string windowName)
        {
            EditorUtility.DisplayDialog(
                DialogTitle,
                $"{windowName} window is not yet implemented.",
                "OK"
            );
        }
    }
}
