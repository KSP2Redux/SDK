using System.Collections.Generic;
using System.IO;
using Redux.Ksp1Import;
using Redux.Ksp1Import.Config;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal sealed class Ksp1EditorPartSelection
    {
        public Ksp1EditorPartSelection(string partName, string title, string filePath, string manufacturer)
        {
            PartName = partName;
            Title = title;
            FilePath = filePath;
            Manufacturer = manufacturer;
            IsSelected = true;
        }

        public string PartName { get; }
        public string Title { get; }
        public string FilePath { get; }
        public string Manufacturer { get; }
        public bool IsSelected { get; set; }

        public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? PartName : Title;

        public string Detail
        {
            get
            {
                string fileName = string.IsNullOrWhiteSpace(FilePath) ? "" : Path.GetFileName(FilePath);
                if (string.IsNullOrWhiteSpace(Manufacturer))
                {
                    return fileName;
                }

                return string.IsNullOrWhiteSpace(fileName) ? Manufacturer : $"{Manufacturer} - {fileName}";
            }
        }

        public bool MatchesFilter(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return true;
            }

            return Contains(DisplayTitle, filter) ||
                   Contains(PartName, filter) ||
                   Contains(Manufacturer, filter) ||
                   Contains(Path.GetFileName(FilePath), filter) ||
                   Contains(FilePath, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    internal sealed class Ksp1EditorPartScanResult
    {
        public Ksp1EditorPartScanResult(IReadOnlyList<Ksp1EditorPartSelection> parts, string reportText)
        {
            Parts = parts;
            ReportText = reportText;
        }

        public IReadOnlyList<Ksp1EditorPartSelection> Parts { get; }
        public string ReportText { get; }
    }

    internal static class Ksp1EditorPartScanner
    {
        public static Ksp1EditorPartScanResult Scan(string sourceRoot)
        {
            Redux.Ksp1Import.Ksp1ImportReport report = new();
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                report.Warn("Select a valid KSP1 mod folder before scanning.");
                return new Ksp1EditorPartScanResult(new List<Ksp1EditorPartSelection>(), string.Join("\n", report.Messages));
            }

            IReadOnlyList<Ksp1ModRoot> roots = new[]
            {
                new Ksp1ModRoot(sourceRoot, Ksp1EditorModConverter.FindGameDataRoot(sourceRoot))
            };
            Ksp1FinalizedConfigDatabase configDatabase = Ksp1FinalizedConfigDatabase.Get(report, roots);
            Ksp1LocalizationCatalog localizationCatalog = Ksp1LocalizationCatalog.FromFiles(configDatabase.Files);
            List<Ksp1EditorPartSelection> parts = new();
            HashSet<string> seenPartNames = new(System.StringComparer.OrdinalIgnoreCase);
            foreach (Ksp1ConfigFile file in configDatabase.Files)
            {
                foreach (Ksp1ConfigNode partNode in file.Node.GetNodes("PART"))
                {
                    if (Ksp1ConfigNode.IsModuleManagerPatchNodeName(partNode.Name))
                    {
                        continue;
                    }

                    string partName = partNode.GetValue("name");
                    if (string.IsNullOrWhiteSpace(partName))
                    {
                        report.Warn($"Skipping PART without a name in '{file.Path}'.");
                        continue;
                    }

                    if (!seenPartNames.Add(partName))
                    {
                        report.Warn($"Skipping duplicate finalized PART '{partName}' from '{file.Path}'.");
                        continue;
                    }

                    parts.Add(
                        new Ksp1EditorPartSelection(
                            partName,
                            localizationCatalog.Resolve(partNode.GetValue("title", partName)),
                            file.Path,
                            localizationCatalog.Resolve(partNode.GetValue("manufacturer", partNode.GetValue("author", "")))
                        )
                    );
                }
            }

            report.Info($"Scanned {parts.Count} KSP1 part(s) from '{sourceRoot}'.");
            return new Ksp1EditorPartScanResult(parts, string.Join("\n", report.Messages));
        }
    }
}
