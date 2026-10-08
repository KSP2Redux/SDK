using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Redux.Ksp1Import;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorLocalizationWriter
    {
        public static void Write(
            string path,
            Ksp1PartLocalizationBuilder localizationBuilder,
            Ksp1EditorImportManifest manifest,
            Ksp1ImportReport report
        )
        {
            if (localizationBuilder == null)
            {
                return;
            }

            string csv = localizationBuilder.BuildCsv();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return;
            }

            string mergedCsv = Merge(File.Exists(path) ? File.ReadAllText(path) : null, csv, report);
            if (string.IsNullOrWhiteSpace(mergedCsv))
            {
                return;
            }

            File.WriteAllText(path, mergedCsv);
            AssetDatabase.ImportAsset(path);
            manifest.MarkGenerated(path);
        }

        internal static string Merge(string existingCsv, string importCsv, Ksp1ImportReport report)
        {
            CsvTable incoming = CsvTable.Parse(importCsv);
            if (incoming.Rows.Count == 0)
            {
                return existingCsv ?? "";
            }

            int incomingKeyIndex = incoming.GetColumnIndex("Key");
            if (incomingKeyIndex < 0)
            {
                report.Warn("KSP1 localization import did not contain a Key column.");
                return existingCsv ?? "";
            }

            if (string.IsNullOrWhiteSpace(existingCsv))
            {
                return incoming.ToCsv();
            }

            CsvTable existing = CsvTable.Parse(existingCsv);
            int existingKeyIndex = existing.GetColumnIndex("Key");
            if (existing.Rows.Count == 0 || existingKeyIndex < 0)
            {
                report.Warn("Existing KSP1 localization CSV did not contain a Key column; replacing it with regenerated localization.");
                return incoming.ToCsv();
            }

            Dictionary<string, int> existingRowsByKey = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < existing.Rows.Count; i++)
            {
                string key = existing.GetCell(existing.Rows[i], existingKeyIndex);
                if (!string.IsNullOrWhiteSpace(key) && !existingRowsByKey.ContainsKey(key))
                {
                    existingRowsByKey.Add(key, i);
                }
            }

            int replaced = 0;
            int appended = 0;
            for (int i = 0; i < incoming.Rows.Count; i++)
            {
                List<string> incomingRow = incoming.Rows[i];
                string key = incoming.GetCell(incomingRow, incomingKeyIndex);
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (existingRowsByKey.TryGetValue(key, out int existingRowIndex))
                {
                    existing.MergeRow(existing.Rows[existingRowIndex], incoming, incomingRow);
                    replaced++;
                }
                else
                {
                    List<string> row = existing.CreateRow();
                    existing.MergeRow(row, incoming, incomingRow);
                    existing.Rows.Add(row);
                    existingRowsByKey.Add(key, existing.Rows.Count - 1);
                    appended++;
                }
            }

            report.Important($"KSP1 localization merged into existing CSV: replaced {replaced}, appended {appended}.");
            return existing.ToCsv();
        }

        private sealed class CsvTable
        {
            public readonly List<string> Header = new();
            public readonly List<List<string>> Rows = new();

            public static CsvTable Parse(string csv)
            {
                CsvTable table = new();
                List<List<string>> records = ParseRecords(csv);
                if (records.Count == 0)
                {
                    return table;
                }

                table.Header.AddRange(records[0]);
                for (int i = 1; i < records.Count; i++)
                {
                    if (records[i].Count == 1 && string.IsNullOrEmpty(records[i][0]))
                    {
                        continue;
                    }

                    table.Rows.Add(records[i]);
                }

                return table;
            }

            public int GetColumnIndex(string name)
            {
                for (int i = 0; i < Header.Count; i++)
                {
                    if (string.Equals(Header[i], name, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }

                return -1;
            }

            public string GetCell(List<string> row, int index)
            {
                return index >= 0 && index < row.Count ? row[index] : "";
            }

            public List<string> CreateRow()
            {
                List<string> row = new(Header.Count);
                for (int i = 0; i < Header.Count; i++)
                {
                    row.Add("");
                }

                return row;
            }

            public void MergeRow(List<string> target, CsvTable source, List<string> sourceRow)
            {
                while (target.Count < Header.Count)
                {
                    target.Add("");
                }

                for (int i = 0; i < source.Header.Count; i++)
                {
                    int targetIndex = GetColumnIndex(source.Header[i]);
                    if (targetIndex < 0)
                    {
                        continue;
                    }

                    target[targetIndex] = source.GetCell(sourceRow, i);
                }
            }

            public string ToCsv()
            {
                StringBuilder builder = new();
                WriteRecord(builder, Header);
                foreach (List<string> row in Rows)
                {
                    WriteRecord(builder, row);
                }

                return builder.ToString();
            }

            private static List<List<string>> ParseRecords(string csv)
            {
                List<List<string>> records = new();
                if (string.IsNullOrEmpty(csv))
                {
                    return records;
                }

                List<string> record = new();
                StringBuilder field = new();
                bool quoted = false;
                for (int i = 0; i < csv.Length; i++)
                {
                    char current = csv[i];
                    if (quoted)
                    {
                        if (current == '"')
                        {
                            if (i + 1 < csv.Length && csv[i + 1] == '"')
                            {
                                field.Append('"');
                                i++;
                            }
                            else
                            {
                                quoted = false;
                            }
                        }
                        else
                        {
                            field.Append(current);
                        }

                        continue;
                    }

                    switch (current)
                    {
                        case '"':
                            quoted = true;
                            break;
                        case ',':
                            record.Add(field.ToString());
                            field.Clear();
                            break;
                        case '\r':
                            if (i + 1 < csv.Length && csv[i + 1] == '\n')
                            {
                                i++;
                            }

                            record = AddRecord(records, record, field);
                            break;
                        case '\n':
                            record = AddRecord(records, record, field);
                            break;
                        default:
                            field.Append(current);
                            break;
                    }
                }

                if (field.Length > 0 || record.Count > 0)
                {
                    AddRecord(records, record, field);
                }

                return records;
            }

            private static List<string> AddRecord(List<List<string>> records, List<string> record, StringBuilder field)
            {
                record.Add(field.ToString());
                field.Clear();
                records.Add(record);
                return new List<string>();
            }

            private static void WriteRecord(StringBuilder builder, List<string> record)
            {
                for (int i = 0; i < record.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(Escape(record[i]));
                }

                builder.AppendLine();
            }

            private static string Escape(string value)
            {
                value ??= "";
                return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
            }
        }
    }
}
