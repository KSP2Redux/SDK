namespace Ksp2UnityTools.Editor.Localization.Widgets
{
    /// <summary>
    /// Shared defaults for standard I2 column ids (Key, Type, Desc, language names, $Context, $Status).
    /// </summary>
    /// <remarks>
    /// One source of truth for column widths so the reader, the merge writer, and the dev sandbox
    /// stay in sync.
    /// </remarks>
    public static class LocColumnSpecDefaults
    {
        /// <summary>
        /// Returns the default display width in pixels for the given column id.
        /// </summary>
        /// <param name="id">The column id (Key, Type, Desc, $Context, $Status, or a language name).</param>
        /// <returns>The default width in pixels, or a generic language-column fallback for unknown ids.</returns>
        public static float WidthFor(string id) => id switch
        {
            "Key" => 220f,
            "Type" => 60f,
            "Desc" => 220f,
            "$Context" => 80f,
            "$Status" => 90f,
            _ => 140f,
        };

        /// <summary>
        /// Returns the minimum display width in pixels for the given column id.
        /// </summary>
        /// <param name="id">The column id (Key, Type, Desc, $Context, $Status, or a language name).</param>
        /// <returns>The minimum width in pixels, or a generic language-column fallback for unknown ids.</returns>
        public static float MinWidthFor(string id) => id switch
        {
            "Key" => 80f,
            "Desc" => 80f,
            "Type" => 50f,
            "$Context" => 50f,
            "$Status" => 50f,
            _ => 60f,
        };
    }
}
