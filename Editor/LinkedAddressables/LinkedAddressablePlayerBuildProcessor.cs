using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public sealed class LinkedAddressablePlayerBuildProcessor
        : IPreprocessBuildWithReport,
            IPostprocessBuildWithReport
    {
        private const string LogPrefix = "[ReduxSDK.LinkedAddressables.Build]";
        private const string JsonCatalogDefine = "ENABLE_JSON_CATALOG";

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var manifest =
                LinkedAddressablePlayerBuildOptions.UseTranslatedSceneBootstrap
                    ? LinkedAddressableRuntimeManifestBuilder.RebuildForPlayer(false)
                    : LinkedAddressableRuntimeManifestBuilder.Rebuild(false);
            if (!RequiresLinkedAddressableBuild(manifest))
            {
                Debug.Log(
                    $"{LogPrefix} No linked Addressables assets are registered. "
                        + "Skipping linked-content validation and staging."
                );
                return;
            }

            if (report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                throw new BuildFailedException(
                    "Redux SDK linked Addressables currently support only "
                        + "StandaloneWindows64 player builds."
                );
            }

            var symbols = PlayerSettings
                .GetScriptingDefineSymbols(NamedBuildTarget.Standalone)
                .Split(';');
            if (!symbols.Contains(JsonCatalogDefine, StringComparer.Ordinal))
            {
                throw new BuildFailedException(
                    $"The {JsonCatalogDefine} scripting define is required because the "
                        + "linked prebuilt player uses a JSON Addressables catalog."
                );
            }

            ValidateExternalSource(manifest);
            if (manifest.UseTranslatedSceneBootstrap)
                ValidateTranslatedContent(manifest);
            Debug.Log(
                $"{LogPrefix} Validated {manifest.Entries.Length} linked root(s) and "
                    + "prebuilt translated target content. External source copy policy: "
                    + $"{(manifest.CopySourceToPlayer ? "copy into player" : "resolve from configured installation")}. "
                    + $"Source: '{manifest.SourceRoot}'.",
                manifest
            );
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            var manifest = AssetDatabase.LoadAssetAtPath<LinkedAddressableRuntimeManifest>(
                LinkedAddressableRuntimeManifestBuilder.ManifestAssetPath
            );
            var outputPath = Path.GetFullPath(report.summary.outputPath);
            var outputDirectory = Path.GetDirectoryName(outputPath);
            var dataDirectory = Path.Combine(
                outputDirectory,
                $"{Path.GetFileNameWithoutExtension(outputPath)}_Data"
            );
            var streamingAssetsDirectory = Path.Combine(
                dataDirectory,
                "StreamingAssets"
            );
            var targetAddressablesDirectory = Path.Combine(
                streamingAssetsDirectory,
                "aa"
            );
            var externalAddressablesDirectory = Path.Combine(
                streamingAssetsDirectory,
                manifest?.StagedExternalMetadataRelativePath
                    ?? "ReduxSDK/ExternalAddressables"
            );
            var translatedDirectory = Path.Combine(
                streamingAssetsDirectory,
                manifest?.ContentDirectoryName ?? "ReduxSDK/LinkedAddressables"
            );
            var receiptDirectory = Path.Combine(
                streamingAssetsDirectory,
                "ReduxSDK"
            );
            var receiptPath = Path.Combine(
                receiptDirectory,
                "linked-addressables-build.json"
            );
            if (!RequiresLinkedAddressableBuild(manifest))
            {
                DeleteContainedDirectory(
                    externalAddressablesDirectory,
                    streamingAssetsDirectory
                );
                DeleteContainedDirectory(
                    translatedDirectory,
                    streamingAssetsDirectory
                );
                if (File.Exists(receiptPath))
                    File.Delete(receiptPath);
                Debug.Log(
                    $"{LogPrefix} No linked Addressables assets are registered. "
                        + "Removed stale linked-content staging from the player."
                );
                return;
            }

            ValidateExternalSource(manifest);
            if (manifest.UseTranslatedSceneBootstrap)
                ValidateTranslatedContent(manifest);

            CleanDirectoryContents(
                externalAddressablesDirectory,
                streamingAssetsDirectory
            );

            var stagedMetadata = new List<string>();
            if (manifest.CopySourceToPlayer)
            {
                CopyDirectory(
                    manifest.SourceRoot,
                    externalAddressablesDirectory
                );
            }
            else
            {
                foreach (var sourceFile in GetMetadataFiles(manifest))
                {
                    var destination = Path.Combine(
                        externalAddressablesDirectory,
                        Path.GetFileName(sourceFile)
                    );
                    File.Copy(sourceFile, destination, true);
                }
            }

            foreach (var sourceFile in GetMetadataFiles(manifest))
            {
                var stagedPath = Path.Combine(
                    externalAddressablesDirectory,
                    Path.GetFileName(sourceFile)
                );
                if (!File.Exists(stagedPath))
                {
                    throw new BuildFailedException(
                        $"Required staged Addressables metadata is missing: "
                            + $"'{stagedPath}'."
                    );
                }
                stagedMetadata.Add(stagedPath);
            }

            var externalPayloads = Directory
                .EnumerateFiles(
                    externalAddressablesDirectory,
                    "*.bundle",
                    SearchOption.AllDirectories
                )
                .ToArray();
            if (!manifest.CopySourceToPlayer && externalPayloads.Length > 0)
            {
                throw new BuildFailedException(
                    "The linked Addressables metadata staging directory unexpectedly "
                        + $"contains external bundle payloads: "
                        + $"{string.Join(", ", externalPayloads)}"
                );
            }
            if (manifest.CopySourceToPlayer && externalPayloads.Length == 0)
            {
                throw new BuildFailedException(
                    "Copying the external Addressables source was enabled, but the "
                        + "staged source contains no bundle payloads."
                );
            }

            var stagedTargetBundles = new List<string>();
            CleanDirectoryContents(
                translatedDirectory,
                streamingAssetsDirectory
            );
            if (manifest.UseTranslatedSceneBootstrap)
            {
                foreach (var fileName in GetTranslatedBundleNames(manifest))
                {
                    var sourcePath = Path.Combine(
                        LinkedAddressableTranslatedContentBuilder.StagingDirectory,
                        fileName
                    );
                    var destination = Path.Combine(translatedDirectory, fileName);
                    File.Copy(sourcePath, destination, true);
                    stagedTargetBundles.Add(destination);
                }
            }

            Directory.CreateDirectory(receiptDirectory);
            var receipt = new LinkedAddressableBuildReceipt
            {
                SchemaVersion = 4,
                ExternalSourceRoot = manifest.SourceRoot,
                ExternalSourceCopied = manifest.CopySourceToPlayer,
                CopiedSourceRelativePath = manifest.CopiedSourceRelativePath,
                StagedExternalMetadataRelativePath =
                    manifest.StagedExternalMetadataRelativePath,
                CatalogId = manifest.CatalogId,
                CatalogHash = manifest.CatalogHash,
                LinkedAssetCount = manifest.Entries.Length,
                StagedMetadataFiles = stagedMetadata
                    .Select(Path.GetFileName)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToArray(),
                StagedTargetBundles = stagedTargetBundles
                    .Select(path => new LinkedAddressableStagedFile
                    {
                        FileName = Path.GetFileName(path),
                        ByteCount = new FileInfo(path).Length
                    })
                    .OrderBy(file => file.FileName, StringComparer.Ordinal)
                    .ToArray(),
                StagedExternalBundleCount = externalPayloads.Length,
                StagedExternalBundleBytes = externalPayloads
                    .Sum(path => new FileInfo(path).Length),
                PreservedTargetAddressablesFileCount =
                    Directory.Exists(targetAddressablesDirectory)
                        ? Directory
                            .EnumerateFiles(
                                targetAddressablesDirectory,
                                "*",
                                SearchOption.AllDirectories
                            )
                            .Count()
                        : 0,
                PreservedTargetAddressablesBytes =
                    Directory.Exists(targetAddressablesDirectory)
                        ? Directory
                            .EnumerateFiles(
                                targetAddressablesDirectory,
                                "*",
                                SearchOption.AllDirectories
                            )
                            .Sum(path => new FileInfo(path).Length)
                        : 0
            };
            File.WriteAllText(receiptPath, JsonUtility.ToJson(receipt, true));

            Debug.Log(
                $"{LogPrefix} Staged {stagedMetadata.Count} metadata file(s), "
                    + $"{stagedTargetBundles.Count} translated target bundle(s), and "
                    + $"{externalPayloads.Length} external bundle payload(s). "
                    + (
                        manifest.CopySourceToPlayer
                            ? $"The player-local source is "
                                + $"'{externalAddressablesDirectory}'. "
                            : $"External bundles remain at '{manifest.SourceRoot}'. "
                    )
                    + $"Target Addressables at '{targetAddressablesDirectory}' "
                    + "were preserved. "
                    + $"Receipt: '{receiptPath}'."
            );
        }

        internal static void ValidateExternalSource(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            if (manifest == null || manifest.Entries == null || manifest.Entries.Length == 0)
                throw new BuildFailedException("No linked Addressables assets are registered.");

            if (
                string.IsNullOrWhiteSpace(manifest.SourceRoot)
                || !Directory.Exists(manifest.SourceRoot)
            )
            {
                throw new BuildFailedException(
                    $"The external Addressables source root is missing: "
                        + $"'{manifest?.SourceRoot}'."
                );
            }

            foreach (var metadataPath in GetMetadataFiles(manifest))
            {
                if (!File.Exists(metadataPath))
                    throw new BuildFailedException(
                        $"Required linked Addressables metadata is missing: '{metadataPath}'."
                    );
            }

            var missingBundles = manifest
                .Entries.Where(entry => entry?.Descriptor?.Dependencies != null)
                .SelectMany(entry => entry.Descriptor.Dependencies)
                .Where(dependency => dependency != null)
                .Select(
                    dependency =>
                        Path.GetFileName(dependency.InternalId?.Replace('\\', '/'))
                )
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(
                    fileName =>
                        Path.Combine(
                            manifest.SourceRoot,
                            "StandaloneWindows64",
                            fileName
                        )
                )
                .Where(path => !File.Exists(path))
                .ToArray();
            if (missingBundles.Length > 0)
            {
                throw new BuildFailedException(
                    "Required external Addressables bundles are missing: "
                        + string.Join(", ", missingBundles)
                );
            }
        }

        internal static bool RequiresLinkedAddressableBuild(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            return manifest?.Entries != null && manifest.Entries.Length > 0;
        }

        internal static void ValidateTranslatedContent(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            if (!File.Exists(LinkedAddressableTranslatedContentBuilder.ReceiptPath))
            {
                throw new BuildFailedException(
                    "Translated linked content has not been built. Use the Redux SDK "
                        + "linked-player build command so content translation runs first."
                );
            }

            if (string.IsNullOrWhiteSpace(manifest?.SceneBundleFileName))
            {
                throw new BuildFailedException(
                    "The runtime manifest has no translated scene bundle."
                );
            }

            var missing = GetTranslatedBundleNames(manifest)
                .Select(
                    fileName =>
                        Path.Combine(
                            LinkedAddressableTranslatedContentBuilder.StagingDirectory,
                            fileName
                        )
                )
                .Where(path => !File.Exists(path))
                .ToArray();
            if (missing.Length > 0)
            {
                throw new BuildFailedException(
                    "Translated target bundles are missing: " + string.Join(", ", missing)
                );
            }
        }

        internal static string[] GetTranslatedBundleNames(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            return new[]
                {
                    manifest?.SceneBundleFileName,
                    manifest?.AssetBundleFileName
                }
                .Concat(manifest?.SupportBundleFileNames ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string[] GetMetadataFiles(
            LinkedAddressableRuntimeManifest manifest
        )
        {
            var settingsFileName = manifest?.SettingsFileName ?? "settings.json";
            var catalogFileName = manifest?.CatalogFileName ?? "catalog.json";
            var catalogHashFileName =
                Path.GetFileNameWithoutExtension(catalogFileName) + ".hash";
            var sourceRoot = manifest?.SourceRoot ?? string.Empty;
            var catalogHashPath = Path.Combine(
                sourceRoot,
                catalogHashFileName
            );
            var required = new[]
            {
                Path.Combine(sourceRoot, settingsFileName),
                Path.Combine(sourceRoot, catalogFileName)
            };
            if (
                File.Exists(catalogHashPath)
                || !string.IsNullOrWhiteSpace(manifest?.CatalogHash)
            )
            {
                return required.Concat(new[] { catalogHashPath }).ToArray();
            }

            return required;
        }

        private static void CopyDirectory(string source, string destination)
        {
            var sourceDirectory = new DirectoryInfo(source);
            if (!sourceDirectory.Exists)
            {
                throw new DirectoryNotFoundException(
                    $"External Addressables source not found: '{sourceDirectory.FullName}'."
                );
            }

            Directory.CreateDirectory(destination);
            foreach (var file in sourceDirectory.GetFiles())
                file.CopyTo(Path.Combine(destination, file.Name), true);

            foreach (var child in sourceDirectory.GetDirectories())
            {
                CopyDirectory(
                    child.FullName,
                    Path.Combine(destination, child.Name)
                );
            }
        }

        private static void CleanDirectoryContents(
            string directoryPath,
            string containingDirectoryPath
        )
        {
            var fullPath = ValidateContainedPath(
                directoryPath,
                containingDirectoryPath
            );
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
            Directory.CreateDirectory(fullPath);
        }

        private static void DeleteContainedDirectory(
            string directoryPath,
            string containingDirectoryPath
        )
        {
            var fullPath = ValidateContainedPath(
                directoryPath,
                containingDirectoryPath
            );
            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, true);
        }

        private static string ValidateContainedPath(
            string directoryPath,
            string containingDirectoryPath
        )
        {
            var fullPath = Path.GetFullPath(directoryPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
            var containingPath = Path.GetFullPath(containingDirectoryPath)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar
                );
            var containingPrefix = containingPath + Path.DirectorySeparatorChar;
            if (
                string.Equals(
                    fullPath,
                    containingPath,
                    StringComparison.OrdinalIgnoreCase
                )
                || !fullPath.StartsWith(
                    containingPrefix,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new BuildFailedException(
                    $"Refusing to clean unsafe Addressables staging path: "
                        + $"'{fullPath}'."
                );
            }

            return fullPath;
        }
    }

    [Serializable]
    internal sealed class LinkedAddressableBuildReceipt
    {
        public int SchemaVersion;
        public string ExternalSourceRoot;
        public bool ExternalSourceCopied;
        public string CopiedSourceRelativePath;
        public string StagedExternalMetadataRelativePath;
        public string CatalogId;
        public string CatalogHash;
        public int LinkedAssetCount;
        public string[] StagedMetadataFiles;
        public LinkedAddressableStagedFile[] StagedTargetBundles;
        public int StagedExternalBundleCount;
        public long StagedExternalBundleBytes;
        public int PreservedTargetAddressablesFileCount;
        public long PreservedTargetAddressablesBytes;
    }

    [Serializable]
    internal sealed class LinkedAddressableStagedFile
    {
        public string FileName;
        public long ByteCount;
    }
}
