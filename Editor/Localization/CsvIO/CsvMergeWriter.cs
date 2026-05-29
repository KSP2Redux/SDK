using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Localization.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    public enum MergeMode
    {
        AppendOnly,
        RefreshDescriptions,
    }

    /// <summary>
    /// Result summary returned by <see cref="CsvMergeWriter.Merge" /> for preview / status reporting.
    /// </summary>
    public sealed class MergeResult
    {
        public List<string> NewKeys = new();
        public List<string> RefreshedDescs = new();
        public int Unchanged;
    }

    /// <summary>
    /// Reads an existing localization CSV, merges incoming entries by key according to
    /// <see cref="MergeMode" />, and writes back via <see cref="LocCsvWriter" />.
    /// Round-trip preserves line endings, trailing newline, and column order from the source.
    /// </summary>
    public static class CsvMergeWriter
    {
        public static MergeResult Merge(string targetPath, IList<LocalizationKeyEntry> entries, MergeMode mode)
        {
            var result = new MergeResult();
            if (string.IsNullOrEmpty(targetPath) || entries == null) return result;

            List<LocColumnSpec> columns;
            List<LocRow> rows;
            string lineEnding = "\n";
            bool hasTrailingNewline = true;

            if (File.Exists(targetPath))
            {
                var text = File.ReadAllText(targetPath);
                var parsed = LocCsvReader.Parse(text, targetPath);
                columns = parsed.Columns;
                rows = parsed.Rows;
                lineEnding = parsed.LineEnding;
                hasTrailingNewline = parsed.HasTrailingNewline;
            }
            else
            {
                columns = DefaultColumns();
                rows = new List<LocRow>();
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
            }

            var byKey = new Dictionary<string, LocRow>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var k = row.Get("Key");
                if (!string.IsNullOrEmpty(k)) byKey[k] = row;
            }

            var defaultValueColumnId = ResolveDefaultValueColumn(columns);

            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (byKey.TryGetValue(entry.Key, out var existing))
                {
                    if (mode == MergeMode.RefreshDescriptions)
                    {
                        var oldDesc = existing.Get("Desc");
                        if (oldDesc != entry.Description)
                        {
                            existing.Set("Desc", entry.Description);
                            result.RefreshedDescs.Add(entry.Key);
                        }
                        else
                        {
                            result.Unchanged++;
                        }
                    }
                    else
                    {
                        result.Unchanged++;
                    }
                    continue;
                }
                var row = new LocRow();
                row.Set("Key", entry.Key);
                row.Set("Type", "Text");
                row.Set("Desc", entry.Description);
                if (!string.IsNullOrEmpty(defaultValueColumnId))
                {
                    row.Set(defaultValueColumnId, entry.DefaultEnglish);
                }
                rows.Add(row);
                byKey[entry.Key] = row;
                result.NewKeys.Add(entry.Key);
            }

            LocCsvWriter.Write(targetPath, columns, rows, lineEnding, hasTrailingNewline);
            if (targetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(targetPath);
            }
            return result;
        }

        private static List<LocColumnSpec> DefaultColumns()
        {
            return new List<LocColumnSpec>
            {
                new() { Id = "Key", HeaderLabel = "Key", DefaultWidth = 220f, MinWidth = 80f, Frozen = true },
                new() { Id = "Type", HeaderLabel = "Type", DefaultWidth = 60f, MinWidth = 50f },
                new() { Id = "Desc", HeaderLabel = "Desc", DefaultWidth = 220f, MinWidth = 80f },
                new() { Id = "English", HeaderLabel = "English", DefaultWidth = 140f, MinWidth = 60f },
            };
        }

        private static string ResolveDefaultValueColumn(List<LocColumnSpec> columns)
        {
            if (columns.Any(c => c.Id == "English")) return "English";
            int descIndex = -1;
            for (int i = 0; i < columns.Count; i++)
            {
                if (columns[i].Id == "Desc")
                {
                    descIndex = i;
                    break;
                }
            }
            if (descIndex >= 0 && descIndex + 1 < columns.Count)
            {
                return columns[descIndex + 1].Id;
            }
            return null;
        }
    }
}
