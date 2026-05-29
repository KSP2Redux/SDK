using System.Collections.Generic;
using System.IO;
using System.Text;
using Ksp2UnityTools.Editor.Localization.Widgets;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    /// <summary>
    /// Serializes column specs and rows back to I2-format CSV text. Quote-escape rules match
    /// <see cref="LocCsvReader" /> so a read-then-write cycle on an unmodified file is byte-stable.
    /// </summary>
    public static class LocCsvWriter
    {
        public static string Write(
            IList<LocColumnSpec> columns,
            IList<LocRow> rows,
            string lineEnding = "\n",
            bool addTrailingNewline = true)
        {
            var sb = new StringBuilder();

            for (int i = 0; i < columns.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(QuoteIfNeeded(columns[i].Id));
            }
            sb.Append(lineEnding);

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
                    sb.Append(lineEnding);
                }
            }
            return sb.ToString();
        }

        public static void Write(
            string path,
            IList<LocColumnSpec> columns,
            IList<LocRow> rows,
            string lineEnding = "\n",
            bool addTrailingNewline = true)
        {
            var text = Write(columns, rows, lineEnding, addTrailingNewline);
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static string QuoteIfNeeded(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            bool needs = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
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
