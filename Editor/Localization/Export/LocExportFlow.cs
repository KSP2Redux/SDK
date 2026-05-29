using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.Localization.Windows;
using Ksp2UnityTools.Editor.MissionAuthoring;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Single entry point for the localization export pipeline. Dispatches on the supplied asset's
    /// type to pick the right extractor, resolves a default target CSV path under the asset's mod
    /// (or the Redux loc folder), and opens <see cref="LocExportModal" /> with the result.
    /// </summary>
    public static class LocExportFlow
    {
        public static void RunForAsset(Object asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[LocExportFlow] No asset provided.");
                return;
            }

            List<LocalizationKeyEntry> entries = null;
            string defaultFilename = null;

            if (asset is GameObject go)
            {
                if (go.TryGetComponent<CorePartData>(out var corePart))
                {
                    entries = PartLocalizationExtractor.Extract(corePart);
                    defaultFilename = "parts_loc.csv";
                }
                else if (go.TryGetComponent<CoreCelestialBodyData>(out var coreBody))
                {
                    entries = CelestialBodyLocalizationExtractor.Extract(coreBody);
                    defaultFilename = "celestialbody_loc.csv";
                }
            }
            else if (asset is Mission mission)
            {
                entries = MissionLocalizationExtractor.Extract(mission);
                defaultFilename = "missions_loc.csv";
            }

            if (entries == null || defaultFilename == null)
            {
                Debug.LogWarning($"[LocExportFlow] Asset type not supported for export: {asset.GetType().Name}");
                return;
            }

            var defaultPath = ResolveDefaultTargetPath(asset, defaultFilename);
            LocExportModal.Open(entries, defaultPath);
        }

        private static string ResolveDefaultTargetPath(Object asset, string filename)
        {
            var mod = KSP2UnityTools.FindParentMod(asset);
            if (mod != null && !string.IsNullOrEmpty(mod.id))
            {
                return $"Assets/{mod.id}/Copied/localizations/{filename}";
            }
            return "Assets/ReduxAssets/Localizations/" + filename;
        }
    }
}
