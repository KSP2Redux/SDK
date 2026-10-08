namespace Ksp2UnityTools.Editor.Localization.Widgets
{
    /// <summary>
    /// Per-column spec used to lay out and persist <see cref="LocTableView" /> columns.
    /// </summary>
    /// <remarks>
    /// Columns are described once at construction. <see cref="CurrentWidth" /> and <see cref="Hidden" />
    /// are mutated at runtime by drag-resize and the Columns dropdown, and persisted to EditorPrefs
    /// keyed by the file path passed to the table.
    /// </remarks>
    public sealed class LocColumnSpec
    {
        /// <summary>Stable identifier used as the key in <see cref="LocRow" />'s cell dictionary.</summary>
        public string Id;

        /// <summary>Text rendered in the column header cell.</summary>
        public string HeaderLabel;

        /// <summary>Default cell width in pixels when no persisted state exists.</summary>
        public float DefaultWidth = 120f;

        /// <summary>Minimum cell width in pixels. The resize handle clamps to this value.</summary>
        public float MinWidth = 60f;

        /// <summary>When true, this column is rendered in the sticky-left frozen area.</summary>
        public bool Frozen;

        /// <summary>Current cell width in pixels. Mutated by drag-resize and restored from EditorPrefs.</summary>
        public float CurrentWidth;

        /// <summary>When true, the column is hidden from header and body but kept in the spec list.</summary>
        public bool Hidden;
    }
}
