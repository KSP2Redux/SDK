using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.Localization.Widgets
{
    /// <summary>
    /// Mutable in-memory row model backing one entry in <see cref="LocTableView" />.
    /// </summary>
    /// <remarks>
    /// Cells are keyed by the owning column's <see cref="LocColumnSpec.Id" />. Missing entries are
    /// treated as empty strings rather than null so callers do not have to null-check on read.
    /// </remarks>
    public sealed class LocRow
    {
        /// <summary>Cell values keyed by column Id.</summary>
        public Dictionary<string, string> Cells = new();

        /// <summary>
        /// Returns the cell value for the given column Id, or an empty string if absent.
        /// </summary>
        /// <param name="columnId">The column Id to look up.</param>
        /// <returns>The cell value, or an empty string when the column Id has no entry.</returns>
        public string Get(string columnId)
        {
            return Cells.TryGetValue(columnId, out var v) ? v : string.Empty;
        }

        /// <summary>
        /// Assigns the cell value for the given column Id.
        /// </summary>
        /// <param name="columnId">The column Id to write.</param>
        /// <param name="value">The new cell value.</param>
        public void Set(string columnId, string value)
        {
            Cells[columnId] = value;
        }
    }
}
