namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// One localization-key fact emitted by a domain extractor. Plain immutable record.
    /// </summary>
    public readonly struct LocalizationKeyEntry
    {
        /// <summary>The loc key string the runtime will request.</summary>
        public string Key { get; }

        /// <summary>Suggested English default value when the key is first written into a CSV.</summary>
        public string DefaultEnglish { get; }

        /// <summary>Translator-facing context describing what this key controls.</summary>
        public string Description { get; }

        /// <summary>Free-form provenance string for debugging and preview diffs.</summary>
        public string SourceHint { get; }

        /// <summary>
        /// Constructs an immutable localization key entry. Null inputs are normalized to empty strings.
        /// </summary>
        /// <param name="key">The loc key string.</param>
        /// <param name="defaultEnglish">The suggested English default value.</param>
        /// <param name="description">The translator-facing context.</param>
        /// <param name="sourceHint">The free-form provenance string.</param>
        public LocalizationKeyEntry(string key, string defaultEnglish, string description, string sourceHint)
        {
            Key = key ?? string.Empty;
            DefaultEnglish = defaultEnglish ?? string.Empty;
            Description = description ?? string.Empty;
            SourceHint = sourceHint ?? string.Empty;
        }
    }
}
