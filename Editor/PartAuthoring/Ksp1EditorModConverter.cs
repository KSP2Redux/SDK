using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using KSP;
using KSP.IO;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.PartAuthoring.StockStats;
using Redux.Ksp1Import;
using Redux.Ksp1Import.Assets;
using Redux.Ksp1Import.Config;
using Redux.Ksp1Import.Model;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PartAuthoring
{
    internal static class Ksp1EditorModConverter
    {
        private const string GeneratedManifestName = "ksp1_import_manifest.json";
        private const string ReportName = "ksp1_import_report.txt";

        public static string Run(
            Mod targetMod,
            string sourceRoot,
            bool overwriteGenerated,
            IReadOnlyCollection<string> selectedPartNames = null
        )
        {
            if (targetMod == null)
            {
                return "No target mod selected.";
            }

            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return "Select a valid KSP1 mod folder.";
            }

            IOProvider.Init();
            targetMod.CreateAddressablesGroups();

            Ksp1ImportReport report = new();
            string modFolder = targetMod.Folder.Replace('\\', '/');
            string generatedRoot = Ksp1EditorAssetUtility.EnsureFolder(modFolder, "Definitions");
            string partsRoot = Ksp1EditorAssetUtility.EnsureFolder(generatedRoot, "Parts");
            string resourceRoot = Ksp1EditorAssetUtility.EnsureFolder(
                Ksp1EditorAssetUtility.EnsureFolder(generatedRoot, "ResourceSystem"),
                "ResourceDefinitions"
            );
            string localizationRoot = Ksp1EditorAssetUtility.EnsureFolder(
                Ksp1EditorAssetUtility.EnsureFolder(modFolder, "Copied"),
                "localizations"
            );
            string manifestPath = $"{modFolder}/{GeneratedManifestName}";
            Ksp1EditorImportManifest manifest = Ksp1EditorImportManifest.Load(manifestPath);

            IReadOnlyList<Ksp1ModRoot> roots = new[]
            {
                new Ksp1ModRoot(sourceRoot, FindGameDataRoot(sourceRoot))
            };

            Ksp1ImportedPartRegistry.BeginImport("editor:" + sourceRoot);
            Ksp1OabCategoryRegistry.Clear();
            Ksp1FinalizedConfigDatabase configDatabase = Ksp1FinalizedConfigDatabase.Get(report, roots);
            IReadOnlyList<Ksp1ConfigFile> configFiles = configDatabase.Files;
            Ksp1B9TankCatalog b9TankCatalog = Ksp1B9TankCatalog.FromConfigFiles(configFiles);
            Ksp1EnginePlumeCatalog plumeCatalog = Ksp1EnginePlumeCatalog.FromConfigFiles(configFiles);
            Ksp1LocalizationCatalog localizationCatalog = Ksp1LocalizationCatalog.FromFiles(configFiles);
            Ksp1PartTranslator translator = new(
                b9TankCatalog,
                plumeCatalog,
                CreateStockResourceMassLookup(report),
                localizationCatalog,
                Ksp1PartTranslator.CategoryMode.PartConfigCategories
            );
            Ksp1PartLocalizationBuilder localizationBuilder = new(localizationCatalog);
            HashSet<string> selectedParts = selectedPartNames == null
                ? null
                : new HashSet<string>(selectedPartNames.Where(name => !string.IsNullOrWhiteSpace(name)), StringComparer.OrdinalIgnoreCase);

            if (selectedParts != null && selectedParts.Count == 0)
            {
                report.Warn("No KSP1 parts were selected for conversion.");
                return BuildAndSaveReport(targetMod, sourceRoot, report);
            }

            List<PartConfigEntry> partEntries = GetUniquePartEntries(configFiles, selectedParts, report);
            int partCount = partEntries.Count;
            int completed = 0;

            try
            {
                Ksp1EditorAssetWriter.ImportResources(configFiles, targetMod, resourceRoot, manifest, overwriteGenerated, report);

                foreach (PartConfigEntry entry in partEntries)
                {
                    GameObject prefab = null;
                    try
                    {
                        completed++;
                        EditorUtility.DisplayProgressBar(
                            "KSP1 Mod Converter",
                            string.IsNullOrWhiteSpace(entry.PartName) ? entry.File.Path : entry.PartName,
                            partCount == 0 ? 1f : completed / (float)partCount
                        );

                        if (!translator.TryTranslatePart(entry.File, entry.PartNode, report, out PartCore core, out string rawJson))
                        {
                            continue;
                        }

                        if (!Ksp1ImportedPartRegistry.TryGetPrefab(Ksp1ImportedPartRegistry.GetPrefabKey(core.data.partName), out prefab))
                        {
                            report.Warn($"Part '{core.data.partName}' converted data but did not produce a prefab.");
                            continue;
                        }

                        string partFolder = Ksp1EditorAssetUtility.EnsureFolder(
                            partsRoot,
                            Ksp1EditorAssetUtility.SanitizePathSegment(core.data.partName)
                        );
                        Ksp1EditorNativeDeployableConverter.Convert(prefab, core, entry.PartNode, partFolder, report);
                        Ksp1EditorPartModuleSync.Sync(prefab, core, report);
                        Ksp1EditorAssetWriter.PersistTransientAssets(prefab, partFolder);
                        Ksp1EditorPlumeVariantWriter.Apply(
                            entry.PartNode,
                            prefab,
                            core.data,
                            plumeCatalog,
                            partFolder,
                            overwriteGenerated,
                            report,
                            core.data.partName
                        );

                        Ksp1EditorAssetWriter.SavePart(prefab, core, rawJson, targetMod, partFolder, manifest, overwriteGenerated, report);
                        localizationBuilder.AddPart(entry.PartNode, core.data.partName);
                        report.PartImported(core.data.partName);
                    }
                    finally
                    {
                        if (prefab != null)
                        {
                            UnityEngine.Object.DestroyImmediate(prefab);
                        }
                    }
                }

                Ksp1EditorLocalizationWriter.Write($"{localizationRoot}/ksp1_import.csv", localizationBuilder, manifest, report);
                manifest.Save(manifestPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                report.Error("KSP1 editor conversion failed.", ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Ksp1ImportedPartRegistry.ClearEditorImportSession();
                Ksp1TextureLoader.ClearCache(false);
            }

            return BuildAndSaveReport(targetMod, sourceRoot, report);
        }

        private static List<PartConfigEntry> GetUniquePartEntries(
            IReadOnlyList<Ksp1ConfigFile> configFiles,
            HashSet<string> selectedParts,
            Ksp1ImportReport report
        )
        {
            List<PartConfigEntry> entries = new();
            HashSet<string> seenPartNames = new(StringComparer.OrdinalIgnoreCase);
            foreach (Ksp1ConfigFile file in configFiles)
            {
                foreach (Ksp1ConfigNode partNode in file.Node.GetNodes("PART"))
                {
                    if (Ksp1ConfigNode.IsModuleManagerPatchNodeName(partNode.Name))
                    {
                        continue;
                    }

                    string partName = partNode.GetValue("name");
                    if (!ShouldConvertPart(partName, selectedParts))
                    {
                        continue;
                    }

                    if (!seenPartNames.Add(partName ?? ""))
                    {
                        report.Warn($"Skipping duplicate finalized PART '{partName}' from '{file.Path}'.");
                        continue;
                    }

                    entries.Add(new PartConfigEntry(file, partNode, partName));
                }
            }

            return entries;
        }

        private static bool ShouldConvertPart(string partName, HashSet<string> selectedParts)
        {
            return selectedParts == null || selectedParts.Contains(partName ?? "");
        }

        private readonly struct PartConfigEntry
        {
            public readonly Ksp1ConfigFile File;
            public readonly Ksp1ConfigNode PartNode;
            public readonly string PartName;

            public PartConfigEntry(Ksp1ConfigFile file, Ksp1ConfigNode partNode, string partName)
            {
                File = file;
                PartNode = partNode;
                PartName = partName;
            }
        }

        private static string BuildAndSaveReport(Mod targetMod, string sourceRoot, Ksp1ImportReport report)
        {
            string reportText = BuildReportText(sourceRoot, report);
            string reportPath = $"{targetMod.Folder.Replace('\\', '/')}/{ReportName}";
            File.WriteAllText(reportPath, reportText);
            AssetDatabase.ImportAsset(reportPath);
            return reportText;
        }

        private static Ksp1ResourceMapper.Ksp2ResourceMassLookup CreateStockResourceMassLookup(Ksp1ImportReport report)
        {
            const string lookupPath = SDKConfiguration.BasePath + "/Assets/StockStats/StockStatsLookup.asset";
            StockStatsLookup lookup = AssetDatabase.LoadAssetAtPath<StockStatsLookup>(lookupPath);
            if (lookup == null)
            {
                report.Warn(
                    $"KSP1 resource conversion could not load stock resource masses from '{lookupPath}'. " +
                    "Falling back to runtime resource definitions where available."
                );
                return null;
            }

            return (string resourceName, out double massPerUnit) =>
            {
                massPerUnit = 0.0;
                if (lookup.TryGetResourceMass(resourceName, out float stockMass))
                {
                    massPerUnit = stockMass;
                    return true;
                }

                return false;
            };
        }

        internal static string FindGameDataRoot(string sourceRoot)
        {
            string gameData = Path.Combine(sourceRoot, "GameData");
            return Directory.Exists(gameData) ? gameData : sourceRoot;
        }

        private static string BuildReportText(string sourceRoot, Ksp1ImportReport report)
        {
            return "KSP1 editor conversion report\n" +
                   "Source: " + sourceRoot + "\n" +
                   "Parts: " + report.ImportedPartCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                   "Resources: " + report.ImportedResourceCount.ToString(CultureInfo.InvariantCulture) + "\n" +
                   "Warnings: " + report.WarningCount.ToString(CultureInfo.InvariantCulture) + "\n\n" +
                   string.Join("\n", report.Messages);
        }
    }
}
