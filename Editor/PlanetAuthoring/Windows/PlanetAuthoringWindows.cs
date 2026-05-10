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
        /// Menu priority for the Environment window.
        /// </summary>
        public const int PriorityEnvironment = 120;

        /// <summary>
        /// Menu priority for the Biome Painter window.
        /// </summary>
        public const int PriorityBiomePainter = 130;

        /// <summary>
        /// Menu priority for the Decal Manager window.
        /// </summary>
        public const int PriorityDecalManager = 140;

        /// <summary>
        /// Menu priority for the Discoverable Manager window.
        /// </summary>
        public const int PriorityDiscoverableManager = 150;

        /// <summary>
        /// Menu priority for the Preset Browser window.
        /// </summary>
        public const int PriorityPresetBrowser = 160;

        /// <summary>
        /// Shows the placeholder dialog for the Validation Report window.
        /// </summary>
        [MenuItem(MenuRoot + "Validation Report", priority = PriorityValidationReport)]
        public static void ShowValidationReportPlaceholder() => ShowNotImplemented("Validation Report");

        /// <summary>
        /// Shows the placeholder dialog for the Environment window.
        /// </summary>
        [MenuItem(MenuRoot + "Environment", priority = PriorityEnvironment)]
        public static void ShowEnvironmentPlaceholder() => ShowNotImplemented("Environment");

        /// <summary>
        /// Shows the placeholder dialog for the Biome Painter window.
        /// </summary>
        [MenuItem(MenuRoot + "Biome Painter", priority = PriorityBiomePainter)]
        public static void ShowBiomePainterPlaceholder() => ShowNotImplemented("Biome Painter");

        /// <summary>
        /// Shows the placeholder dialog for the Decal Manager window.
        /// </summary>
        [MenuItem(MenuRoot + "Decal Manager", priority = PriorityDecalManager)]
        public static void ShowDecalManagerPlaceholder() => ShowNotImplemented("Decal Manager");

        /// <summary>
        /// Shows the placeholder dialog for the Discoverable Manager window.
        /// </summary>
        [MenuItem(MenuRoot + "Discoverable Manager", priority = PriorityDiscoverableManager)]
        public static void ShowDiscoverableManagerPlaceholder() => ShowNotImplemented("Discoverable Manager");

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
