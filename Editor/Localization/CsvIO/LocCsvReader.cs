using System.Collections.Generic;
using System.Text;
using Ksp2UnityTools.Editor.Localization.Widgets;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    /// <summary>
    /// Parsed result of an I2-format localization CSV file.
    /// </summary>
    /// <remarks>
    /// Records the trailing-newline state so <see cref="LocCsvWriter" /> can reproduce it on save
    /// for a byte-stable round-trip on unmodified files. Line endings are normalized to <c>\n</c>
    /// on read because the I2 runtime CSV parser (LocalizationReader.ParseCSVline) only treats
    /// <c>\n</c> as a row break.
    /// </remarks>
    public sealed class LocCsvParseResult
    {
        /// <summary>
        /// Column specs derived from the header row.
        /// </summary>
        public List<LocColumnSpec> Columns = new();

        /// <summary>
        /// Data rows, keyed by column Id.
        /// </summary>
        public List<LocRow> Rows = new();

        /// <summary>
        /// True if the source file ends with a trailing newline, false otherwise.
        /// </summary>
        public bool HasTrailingNewline = true;

        /// <summary>
        /// Source file path, for round-trip context.
        /// </summary>
        public string FilePath;
    }

    /// <summary>
    /// Standard CSV parser tuned for the I2 localization format.
    /// </summary>
    /// <remarks>
    /// Quote-aware and handles doubled quote escapes. Line endings are normalized to <c>\n</c> on
    /// read so a write-back round-trip always emits the line-ending convention the runtime parser
    /// expects.
    /// </remarks>
    public static class LocCsvReader
    {
        /// <summary>
        /// Parses CSV text into a <see cref="LocCsvParseResult" />.
        /// </summary>
        /// <param name="text">The raw CSV text to parse.</param>
        /// <param name="filePath">The source file path recorded on the result for round-trip context.</param>
        /// <returns>The parsed result, or an empty result if <paramref name="text" /> is null.</returns>
        public static LocCsvParseResult Parse(string text, string filePath = null)
        {
            var result = new LocCsvParseResult { FilePath = filePath };
            if (text == null) return result;

            text = text.Replace("\r\n", "\n").Replace("\r", "\n");
            result.HasTrailingNewline = text.EndsWith("\n");

            var raw = ParseRowsRaw(text);
            if (raw.Count == 0) return result;

            var headerFields = raw[0];
            result.Columns = BuildColumnSpecs(headerFields);

            for (int i = 1; i < raw.Count; i++)
            {
                var fields = raw[i];
                if (fields.Count == 1 && string.IsNullOrEmpty(fields[0])) continue;
                var row = new LocRow();
                int n = fields.Count < headerFields.Count ? fields.Count : headerFields.Count;
                for (int j = 0; j < n; j++)
                {
                    row.Set(headerFields[j], fields[j]);
                }
                result.Rows.Add(row);
            }
            return result;
        }

        private static List<List<string>> ParseRowsRaw(string text)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var field = new StringBuilder();
            bool inQuoted = false;

            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];

                if (inQuoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i += 2;
                            continue;
                        }
                        inQuoted = false;
                        i++;
                        continue;
                    }
                    field.Append(c);
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    i++;
                    continue;
                }

                if (c == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    i++;
                    continue;
                }

                if (c == '"' && field.Length == 0)
                {
                    inQuoted = true;
                    i++;
                    continue;
                }

                field.Append(c);
                i++;
            }

            if (field.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(field.ToString());
                rows.Add(currentRow);
            }
            return rows;
        }

        private static List<LocColumnSpec> BuildColumnSpecs(List<string> headerFields)
        {
            var columns = new List<LocColumnSpec>(headerFields.Count);
            for (int i = 0; i < headerFields.Count; i++)
            {
                var id = headerFields[i];
                columns.Add(new LocColumnSpec
                {
                    Id = id,
                    HeaderLabel = id,
                    DefaultWidth = LocColumnSpecDefaults.WidthFor(id),
                    MinWidth = LocColumnSpecDefaults.MinWidthFor(id),
                    Frozen = i == 0,
                });
            }
            return columns;
        }
    }
}
