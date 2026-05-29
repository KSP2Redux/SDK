using System.Collections.Generic;
using System.IO;
using System.Text;
using Ksp2UnityTools.Editor.Localization.Widgets;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    /// <summary>
    /// Serializes column specs and rows back to I2-format CSV text.
    /// </summary>
    /// <remarks>
    /// Always emits LF line endings because the I2 runtime CSV parser only treats <c>\n</c> as a
    /// row break. Quote-escape rules match <see cref="LocCsvReader" />.
    /// </remarks>
    public static class LocCsvWriter
    {
        /// <summary>
        /// Serializes the given columns and rows to a CSV string.
        /// </summary>
        /// <param name="columns">The column specs whose ids form the header row and field order.</param>
        /// <param name="rows">The data rows to emit, looked up by column id.</param>
        /// <param name="addTrailingNewline">True to append a trailing newline after the final row, false otherwise.</param>
        /// <returns>The serialized CSV text.</returns>
        public static string Write(
            IList<LocColumnSpec> columns,
            IList<LocRow> rows,
            bool addTrailingNewline = true)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(QuoteIfNeeded(columns[i].Id));
            }
            sb.Append('\n');

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int c = 0; c < columns.Count; c++)
                {
                    if (c > 0) sb.Append(',');
                    sb.Append(QuoteIfNeeded(row.Get(columns[c].Id)));
                }
                bool isLast = r == rows.Count - 1;
                if (!isLast || addTrailingNewline)
                {
                    sb.Append('\n');
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes the given columns and rows and writes the result to a file as UTF-8 without BOM.
        /// </summary>
        /// <param name="path">The destination file path.</param>
        /// <param name="columns">The column specs whose ids form the header row and field order.</param>
        /// <param name="rows">The data rows to emit, looked up by column id.</param>
        /// <param name="addTrailingNewline">True to append a trailing newline after the final row, false otherwise.</param>
        public static void Write(
            string path,
            IList<LocColumnSpec> columns,
            IList<LocRow> rows,
            bool addTrailingNewline = true)
        {
            var text = Write(columns, rows, addTrailingNewline);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool needs = false;
            foreach (var c in value)
            {
                if (c == ',' || c == '"' || c == '\n' || c == '\r')
                {
                    needs = true;
                    break;
                }
            }
            if (!needs) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
