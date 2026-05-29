using System.Collections.Generic;
using System.Text;
using Ksp2UnityTools.Editor.Localization.Widgets;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    /// <summary>
    /// Parsed result of an I2-format localization CSV file, with enough format metadata for
    /// <see cref="LocCsvWriter" /> to reproduce the original line-ending convention and trailing-newline
    /// state on save.
    /// </summary>
    public sealed class LocCsvParseResult
    {
        /// <summary>Column specs derived from the header row.</summary>
        public List<LocColumnSpec> Columns = new();

        /// <summary>Data rows, keyed by column Id.</summary>
        public List<LocRow> Rows = new();

        /// <summary>Detected line ending of the source file ("\n" or "\r\n").</summary>
        public string LineEnding = "\n";

        /// <summary>Whether the source file ends with a trailing newline.</summary>
        public bool HasTrailingNewline = true;

        /// <summary>Source file path, for round-trip context.</summary>
        public string FilePath;
    }

    /// <summary>
    /// Standard CSV parser tuned for the I2 localization format. Quote-aware, handles doubled
    /// quote escapes, and records line-ending metadata so writes round-trip byte-stably.
    /// </summary>
    public static class LocCsvReader
    {
        public static LocCsvParseResult Parse(string text, string filePath = null)
        {
            var result = new LocCsvParseResult { FilePath = filePath };
            if (text == null) return result;

            result.LineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
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

                if (c == '\r' || c == '\n')
                {
                    currentRow.Add(field.ToString());
                    field.Clear();
                    rows.Add(currentRow);
                    currentRow = new List<string>();
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i += 2;
                    }
                    else
                    {
                        i++;
                    }
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
                    DefaultWidth = DefaultWidthFor(id),
                    MinWidth = 60f,
                    Frozen = i == 0,
                });
            }
            return columns;
        }

        private static float DefaultWidthFor(string id)
        {
            return id switch
            {
                "Key" => 220f,
                "Type" => 60f,
                "Desc" => 220f,
                "$Context" => 80f,
                "$Status" => 90f,
                _ => 140f,
            };
        }
    }
}
