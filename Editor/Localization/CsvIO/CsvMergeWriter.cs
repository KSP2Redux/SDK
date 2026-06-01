using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.Localization.Export;
using Ksp2UnityTools.Editor.Localization.Widgets;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.CsvIO
{
    /// <summary>
    /// Strategy controlling how <see cref="CsvMergeWriter.Merge" /> reconciles incoming entries
    /// against existing rows.
    /// </summary>
    public enum MergeMode
    {
        /// <summary>
        /// Adds only entries whose key is not already present, leaving existing rows untouched.
        /// </summary>
        AppendOnly,

        /// <summary>
        /// Adds new entries and also refreshes the description on existing rows when the incoming
        /// description differs.
        /// </summary>
        RefreshDescriptions,
    }

    /// <summary>
    /// Classification of incoming entries relative to an existing-row lookup.
    /// </summary>
    /// <remarks>
    /// Used by both the preview pane and the merge writer so they cannot drift on what counts as
    /// new vs refreshed vs unchanged.
    /// </remarks>
    public sealed class MergeClassification
    {
        /// <summary>
        /// Keys of entries that have no matching existing row.
        /// </summary>
        public List<string> NewKeys = new();

        /// <summary>
        /// Keys of existing rows whose Desc column differs from the incoming entry.
        /// </summary>
        public List<string> DescUpdates = new();

        /// <summary>
        /// Count of entries that match an existing row with no Desc difference.
        /// </summary>
        public int Unchanged;
    }

    /// <summary>
    /// Result summary returned by <see cref="CsvMergeWriter.Merge" /> for preview and status reporting.
    /// </summary>
    public sealed class MergeResult
    {
        /// <summary>
        /// Keys appended as new rows during the merge.
        /// </summary>
        public List<string> NewKeys = new();

        /// <summary>
        /// Keys whose Desc column was refreshed against the incoming entry.
        /// </summary>
        public List<string> RefreshedDescs = new();

        /// <summary>
        /// Count of entries that matched an existing row with no change.
        /// </summary>
        public int Unchanged;
    }

    /// <summary>
    /// Reads an existing localization CSV, merges incoming entries by key according to
    /// <see cref="MergeMode" />, and writes back via <see cref="LocCsvWriter" />.
    /// </summary>
    /// <remarks>
    /// Round-trip preserves trailing newline and column order from the source.
    /// </remarks>
    public static class CsvMergeWriter
    {
        /// <summary>
        /// Merges <paramref name="entries" /> into the CSV at <paramref name="targetPath" /> and writes the result.
        /// </summary>
        /// <param name="targetPath">The path to the CSV file to merge into. Created if missing.</param>
        /// <param name="entries">The incoming entries to merge by key.</param>
        /// <param name="mode">The merge strategy controlling how existing rows are reconciled.</param>
        /// <returns>A summary of new, refreshed, and unchanged keys.</returns>
        public static MergeResult Merge(string targetPath, IList<LocalizationKeyEntry> entries, MergeMode mode)
        {
            var result = new MergeResult();
            if (string.IsNullOrEmpty(targetPath) || entries == null) return result;

            List<LocColumnSpec> columns;
            List<LocRow> rows;
            var hasTrailingNewline = true;

            if (File.Exists(targetPath))
            {
                var text = File.ReadAllText(targetPath);
                var parsed = LocCsvReader.Parse(text, targetPath);
                columns = parsed.Columns;
                rows = parsed.Rows;
                hasTrailingNewline = parsed.HasTrailingNewline;
            }
            else
            {
                columns = DefaultColumns();
                rows = new List<LocRow>();
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? ".");
            }

            var classification = Classify(entries, rows, mode);
            result.NewKeys = classification.NewKeys;
            result.RefreshedDescs = classification.DescUpdates;
            result.Unchanged = classification.Unchanged;

            if (result.NewKeys.Count == 0 && result.RefreshedDescs.Count == 0) return result;

            var entriesByKey = BuildEntryLookup(entries);
            var rowsByKey = BuildRowLookup(rows);

            foreach (var key in classification.DescUpdates)
            {
                if (rowsByKey.TryGetValue(key, out var row) && entriesByKey.TryGetValue(key, out var entry))
                {
                    row.Set("Desc", entry.Description);
                }
            }

            var defaultValueColumnId = ResolveDefaultValueColumn(columns);
            foreach (var key in classification.NewKeys)
            {
                if (!entriesByKey.TryGetValue(key, out var entry)) continue;
                var row = new LocRow();
                row.Set("Key", entry.Key);
                row.Set("Type", "Text");
                row.Set("Desc", entry.Description);
                if (!string.IsNullOrEmpty(defaultValueColumnId))
                {
                    row.Set(defaultValueColumnId, entry.DefaultEnglish);
                }
                rows.Add(row);
            }

            LocCsvWriter.Write(targetPath, columns, rows, hasTrailingNewline);
            if (targetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                AssetDatabase.ImportAsset(targetPath);
            }
            return result;
        }

        /// <summary>
        /// Categorizes each entry in <paramref name="entries" /> as new, desc-refresh, or unchanged
        /// against <paramref name="existingRows" />.
        /// </summary>
        /// <remarks>
        /// Pure with no mutations to the inputs.
        /// </remarks>
        /// <param name="entries">The incoming entries to classify.</param>
        /// <param name="existingRows">The existing rows to compare keys and descriptions against.</param>
        /// <param name="mode">The merge strategy controlling whether desc differences register as refreshes.</param>
        /// <returns>The classification of each entry as new, desc-refresh, or unchanged.</returns>
        public static MergeClassification Classify(IList<LocalizationKeyEntry> entries, IEnumerable<LocRow> existingRows, MergeMode mode)
        {
            var classification = new MergeClassification();
            if (entries == null) return classification;
            var rowsByKey = BuildRowLookup(existingRows);
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key)) continue;
                if (rowsByKey.TryGetValue(entry.Key, out var row))
                {
                    if (mode == MergeMode.RefreshDescriptions && row.Get("Desc") != entry.Description)
                    {
                        classification.DescUpdates.Add(entry.Key);
                    }
                    else
                    {
                        classification.Unchanged++;
                    }
                }
                else
                {
                    classification.NewKeys.Add(entry.Key);
                }
            }
            return classification;
        }

        private static Dictionary<string, LocRow> BuildRowLookup(IEnumerable<LocRow> rows)
        {
            var byKey = new Dictionary<string, LocRow>(StringComparer.Ordinal);
            if (rows == null) return byKey;
            foreach (var row in rows)
            {
                var k = row.Get("Key");
                if (!string.IsNullOrEmpty(k)) byKey[k] = row;
            }
            return byKey;
        }

        private static Dictionary<string, LocalizationKeyEntry> BuildEntryLookup(IEnumerable<LocalizationKeyEntry> entries)
        {
            var byKey = new Dictionary<string, LocalizationKeyEntry>(StringComparer.Ordinal);
            if (entries == null) return byKey;
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.Key)) byKey[entry.Key] = entry;
            }
            return byKey;
        }

        private static List<LocColumnSpec> DefaultColumns()
        {
            return new List<LocColumnSpec>
            {
                BuildDefault("Key", frozen: true),
                BuildDefault("Type"),
                BuildDefault("Desc"),
                BuildDefault("English"),
            };
        }

        private static LocColumnSpec BuildDefault(string id, bool frozen = false)
        {
            return new LocColumnSpec
            {
                Id = id,
                HeaderLabel = id,
                DefaultWidth = LocColumnSpecDefaults.WidthFor(id),
                MinWidth = LocColumnSpecDefaults.MinWidthFor(id),
                Frozen = frozen,
            };
        }

        private static string ResolveDefaultValueColumn(List<LocColumnSpec> columns)
        {
            if (columns.Any(c => c.Id == "English")) return "English";
            var descIndex = columns.FindIndex(c => c.Id == "Desc");
            return descIndex >= 0 && descIndex + 1 < columns.Count ? columns[descIndex + 1].Id : null;
        }
    }
}
