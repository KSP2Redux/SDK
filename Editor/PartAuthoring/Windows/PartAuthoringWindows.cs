namespace Ksp2UnityTools.Editor.PartAuthoring.Windows
{
    /// <summary>
    /// Shared menu-root and priority constants for the Redux Part Authoring window family.
    /// </summary>
    /// <remarks>
    /// Mirrors the planet-authoring layout. Window classes pull <see cref="MENU_ROOT" /> and
    /// their priority constants from here so the menu hierarchy stays in one place.
    /// </remarks>
    internal static class PartAuthoringWindows
    {
        /// <summary>
        /// Root menu path under which every Part Authoring window is registered.
        /// </summary>
        public const string MENU_ROOT = "Modding/Part Authoring/";

        /// <summary>
        /// Title used by the shared placeholder dialog.
        /// </summary>
        public const string DIALOG_TITLE = "Part Authoring";

        /// <summary>
        /// Menu priority for the Reference Parts window.
        /// </summary>
        public const int PRIORITY_REFERENCE_PARTS = 100;

        /// <summary>
        /// Menu priority for the Validation Report window.
        /// </summary>
        public const int PRIORITY_VALIDATION_REPORT = 110;

        /// <summary>
        /// Menu priority for the New Part wizard.
        /// </summary>
        public const int PRIORITY_NEW_PART_WIZARD = 120;

        /// <summary>
        /// Menu priority for the Stock Stats Bake window.
        /// </summary>
        public const int PRIORITY_STOCK_STATS_BAKE = 140;

        /// <summary>
        /// Menu priority for the KSP1 part-mod converter.
        /// </summary>
        public const int PRIORITY_KSP1_MOD_CONVERTER = 150;
    }
}
