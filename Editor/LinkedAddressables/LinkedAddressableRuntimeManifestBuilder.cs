using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    [InitializeOnLoad]
    public static class LinkedAddressableRuntimeManifestBuilder
    {
        public const string ManifestAssetPath =
            "Assets/ReduxSDK/Generated/LinkedAddressables/Resources/ReduxSDK/LinkedAddressableRuntimeManifest.asset";
        private const string InterimGeneratedRoot = "Assets/ReduxSDKGenerated";
        private const string LegacyGeneratedRoot = "Assets/KSP2UnityToolsGenerated";

        private static bool rebuildScheduled;

        static LinkedAddressableRuntimeManifestBuilder()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ScheduleRebuild();
        }

        [MenuItem("Modding/Linked Addressables/Advanced/Rebuild Runtime Manifest", false, 210)]
        public static void RebuildFromMenu()
        {
            Rebuild(true);
        }

        public static LinkedAddressableRuntimeManifest Rebuild(bool logResult)
        {
            return Rebuild(logResult, GetDescriptorPaths());
        }

        internal static LinkedAddressableRuntimeManifest RebuildForPlayer(
            bool logResult
        )
        {
            var translatedContent = ReadTranslatedContentReceipt();
            if (
                translatedContent?.UsedDescriptorPaths == null
                || translatedContent.UsedDescriptorPaths.Length == 0
            )
            {
                throw new InvalidOperationException(
                    "The translated-content receipt does not identify any linked "
                        + "Addressables used by the player. Rebuild translated content."
                );
            }

            return Rebuild(logResult, translatedContent.UsedDescriptorPaths);
        }

        private static LinkedAddressableRuntimeManifest Rebuild(
            bool logResult,
            IReadOnlyCollection<string> descriptorPaths
        )
        {
            rebuildScheduled = false;
            RemoveObsoleteGeneratedRoots();

            var entries = descriptorPaths
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(
                    path =>
                        new LinkedAddressableRuntimeEntry
                        {
                            Descriptor = LinkedAddressableAssetImporter.ReadDescriptor(path)
                        }
                )
                .ToArray();

            var sourceIds = entries
                .Select(entry => entry.Descriptor?.SourceId)
                .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (sourceIds.Length > 1)
            {
                throw new InvalidOperationException(
                    "The current runtime manifest supports one external Addressables source. "
                    + $"Found: {string.Join(", ", sourceIds)}"
                );
            }

            var directory = Path.GetDirectoryName(ManifestAssetPath);
            Directory.CreateDirectory(directory);
            var manifest = AssetDatabase.LoadAssetAtPath<LinkedAddressableRuntimeManifest>(
                ManifestAssetPath
            );
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<LinkedAddressableRuntimeManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestAssetPath);
            }

            if (entries.Length == 0)
            {
                manifest.SourceId = null;
                manifest.SourceRoot = null;
                manifest.CatalogId = null;
                manifest.CatalogHash = null;
                manifest.CatalogFileName = "catalog.json";
                manifest.SettingsFileName = "settings.json";
                manifest.CopySourceToPlayer = false;
                manifest.CopiedSourceRelativePath =
                    "ReduxSDK/ExternalAddressables";
                manifest.StagedExternalMetadataRelativePath =
                    "ReduxSDK/ExternalAddressables";
                manifest.ContentDirectoryName = "ReduxSDK/LinkedAddressables";
                manifest.SceneBundleFileName = null;
                manifest.AssetBundleFileName = null;
                manifest.InitialScenePath = null;
                manifest.UseTranslatedSceneBootstrap = false;
                manifest.SupportBundleFileNames = Array.Empty<string>();
                manifest.Entries = entries;
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);

                if (logResult)
                {
                    Debug.Log(
                        "[ReduxSDK.LinkedAddressables] Rebuilt an empty runtime manifest.",
                        manifest
                    );
                }

                return manifest;
            }

            var thunderKitSettings =
                ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var firstDescriptor = entries.FirstOrDefault()?.Descriptor;
            manifest.SourceId = firstDescriptor?.SourceId ?? thunderKitSettings.PackageName;
            manifest.SourceRoot = Path.GetFullPath(
                    thunderKitSettings.AddressableAssetsPath
                )
                .Replace('\\', '/');
            manifest.CatalogId = firstDescriptor?.CatalogId;
            manifest.CatalogHash = firstDescriptor?.CatalogHash;
            manifest.CatalogFileName = firstDescriptor?.CatalogFileName ?? "catalog.json";
            manifest.SettingsFileName = firstDescriptor?.SettingsFileName ?? "settings.json";
            manifest.CopySourceToPlayer =
                LinkedAddressablePlayerBuildOptions.CopySourceToPlayer;
            manifest.CopiedSourceRelativePath =
                "ReduxSDK/ExternalAddressables";
            manifest.StagedExternalMetadataRelativePath =
                "ReduxSDK/ExternalAddressables";
            manifest.ContentDirectoryName = "ReduxSDK/LinkedAddressables";
            var translatedContent = ReadTranslatedContentReceipt();
            manifest.SceneBundleFileName = translatedContent?.SceneBundleName;
            manifest.AssetBundleFileName = translatedContent?.AssetBundleName;
            manifest.InitialScenePath = translatedContent?.ScenePaths?.FirstOrDefault();
            manifest.UseTranslatedSceneBootstrap =
                LinkedAddressablePlayerBuildOptions.UseTranslatedSceneBootstrap;
            manifest.SupportBundleFileNames =
                translatedContent
                    ?.StagedBundleNames?.Where(
                        fileName =>
                            !string.Equals(
                                fileName,
                                translatedContent.SceneBundleName,
                                StringComparison.OrdinalIgnoreCase
                            )
                            && !string.Equals(
                                fileName,
                                translatedContent.AssetBundleName,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    .ToArray()
                ?? Array.Empty<string>();
            manifest.Entries = entries;
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssetIfDirty(manifest);

            if (logResult)
            {
                Debug.Log(
                    $"[ReduxSDK.LinkedAddressables] Rebuilt runtime manifest with "
                        + $"{entries.Length} linked asset(s) from '{manifest.SourceRoot}'.",
                    manifest
                );
            }

            return manifest;
        }

        private static void RemoveObsoleteGeneratedRoots()
        {
            RemoveObsoleteGeneratedRoot(InterimGeneratedRoot);
            RemoveObsoleteGeneratedRoot(LegacyGeneratedRoot);
        }

        private static void RemoveObsoleteGeneratedRoot(string assetPath)
        {
            if (
                AssetDatabase.IsValidFolder(assetPath)
                && !AssetDatabase.DeleteAsset(assetPath)
            )
            {
                throw new IOException(
                    $"Could not remove the obsolete generated SDK folder '{assetPath}'."
                );
            }
        }

        private static LinkedAddressableTranslatedContentBuild ReadTranslatedContentReceipt()
        {
            if (!File.Exists(LinkedAddressableTranslatedContentBuilder.ReceiptPath))
                return null;

            try
            {
                return JsonUtility.FromJson<LinkedAddressableTranslatedContentBuild>(
                    File.ReadAllText(LinkedAddressableTranslatedContentBuilder.ReceiptPath)
                );
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[ReduxSDK.LinkedAddressables] Could not read the translated-content "
                        + $"receipt: {exception.Message}"
                );
                return null;
            }
        }

        public static void ScheduleRebuild()
        {
            if (rebuildScheduled)
                return;

            rebuildScheduled = true;
            EditorApplication.delayCall += RebuildAfterImport;
        }

        private static void RebuildAfterImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RebuildAfterImport;
                return;
            }

            Rebuild(false);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Rebuild(false);
                LinkedAddressableEditorSource.PrepareForPlayMode();
            }
        }

        internal static string[] GetDescriptorPaths()
        {
            return AssetDatabase
                .GetAllAssetPaths()
                .Where(
                    path =>
                        path.StartsWith("Assets/", StringComparison.Ordinal)
                        && path.EndsWith(
                            $".{LinkedAddressableAssetImporter.Extension}",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

    }

    internal sealed class LinkedAddressableRuntimeManifestPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (
                importedAssets
                    .Concat(deletedAssets)
                    .Concat(movedAssets)
                    .Concat(movedFromAssetPaths)
                    .Any(
                        path =>
                            path.EndsWith(
                                $".{LinkedAddressableAssetImporter.Extension}",
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
            )
            {
                LinkedAddressableRuntimeManifestBuilder.ScheduleRebuild();
            }
        }
    }
}
