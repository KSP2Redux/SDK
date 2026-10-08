namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Shared formatter for the <see cref="LocalizationKeyEntry.SourceHint" /> string used by
    /// every domain extractor. One source of truth so preview diffs stay uniform.
    /// </summary>
    internal static class LocalizationSourceHint
    {
        /// <summary>
        /// Formats a source hint string identifying the originating asset type, id, and path.
        /// </summary>
        /// <param name="typeName">The originating asset type name.</param>
        /// <param name="id">The asset identifier (part name, body name, mission id, etc.).</param>
        /// <param name="assetPath">The project-relative asset path.</param>
        /// <returns>The formatted source hint.</returns>
        public static string Format(string typeName, string id, string assetPath)
        {
            return $"{typeName}: {id} ({assetPath})";
        }
    }
}
