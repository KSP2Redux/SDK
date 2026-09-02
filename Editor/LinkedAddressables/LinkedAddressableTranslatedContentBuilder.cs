using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public static class LinkedAddressableTranslatedContentBuilder
    {
        public const string SceneBundleName =
            "ksp2ut-linked-player-scenes.bundle";
        public const string AssetBundleName =
            "ksp2ut-linked-player-assets.bundle";
        public const string OutputDirectory =
            "Library/KSP2UnityTools/LinkedAddressables/TranslatedContent";
        public const string StagingDirectory =
            OutputDirectory + "/Staged";
        public const string ReceiptPath =
            OutputDirectory + "/translated-content-build.json";

        [MenuItem("Modding/Linked Addressables/Advanced/Build Translated Content Only", false, 200)]
        public static void BuildFromMenu()
        {
            Build(true);
        }

        public static LinkedAddressableTranslatedContentBuild Build(bool logResult)
        {
            if (BuildPipeline.isBuildingPlayer)
            {
                throw new InvalidOperationException(
                    "Translated linked content must be built before the player build starts."
                );
            }

            var descriptorPaths =
                LinkedAddressableRuntimeManifestBuilder.GetDescriptorPaths();
            if (descriptorPaths.Length == 0)
                throw new InvalidOperationException("No linked Addressables are registered.");

            var scenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && File.Exists(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            if (scenePaths.Length == 0)
            {
                var activeScenePath = SceneManager.GetActiveScene().path;
                if (
                    !string.IsNullOrWhiteSpace(activeScenePath)
                    && File.Exists(activeScenePath)
                )
                {
                    scenePaths = new[] { activeScenePath };
                }
            }
            if (scenePaths.Length == 0)
            {
                throw new InvalidOperationException(
                    "No enabled or saved active target scene is available for "
                        + "translated content."
                );
            }

            var consumerPaths = FindConsumerAssets(
                descriptorPaths,
                scenePaths
            );
            var usedDescriptorPaths = FindUsedDescriptorPaths(
                descriptorPaths,
                scenePaths.Concat(consumerPaths)
            );
            if (usedDescriptorPaths.Length == 0)
            {
                throw new InvalidOperationException(
                    "The enabled target scenes and consumer assets do not reference "
                        + "any linked Addressables."
                );
            }

            var translation = BuildTranslationTable(usedDescriptorPaths);
            PrepareOutputDirectory();

            var builds = new List<AssetBundleBuild>
            {
                new AssetBundleBuild
                {
                    assetBundleName = SceneBundleName,
                    assetNames = scenePaths
                }
            };
            if (consumerPaths.Length > 0)
            {
                builds.Add(
                    new AssetBundleBuild
                    {
                        assetBundleName = AssetBundleName,
                        assetNames = consumerPaths
                    }
                );
            }

            var identifiers = new LinkedAddressablePackedIdentifiers(
                translation.Objects
            );
            var parameters = new BundleBuildParameters(
                BuildTarget.StandaloneWindows64,
                BuildTargetGroup.Standalone,
                OutputDirectory
            )
            {
                AppendHash = false,
                BundleCompression = BuildCompression.LZ4Runtime,
                ContiguousBundles = true
            };
            var content = new BundleBuildContent(builds);
            var tasks = CreateBuildTasks();
            var returnCode = ContentPipeline.BuildAssetBundles(
                parameters,
                content,
                out var results,
                tasks,
                identifiers
            );
            if (returnCode < ReturnCode.Success)
            {
                throw new InvalidOperationException(
                    $"Translated linked content build failed with {returnCode}."
                );
            }

            var stagedBundleNames = new List<string> { SceneBundleName };
            if (consumerPaths.Length > 0)
                stagedBundleNames.Add(AssetBundleName);
            stagedBundleNames.AddRange(
                new[] { "UnityBuiltIn.bundle", "UnityMonoScripts.bundle" }
                    .Where(
                        bundleName =>
                            File.Exists(Path.Combine(OutputDirectory, bundleName))
                    )
            );

            Directory.CreateDirectory(StagingDirectory);
            foreach (var bundleName in stagedBundleNames)
            {
                var sourcePath = Path.Combine(OutputDirectory, bundleName);
                if (!File.Exists(sourcePath))
                {
                    throw new FileNotFoundException(
                        $"The translated content build did not produce '{bundleName}'.",
                        sourcePath
                    );
                }

                File.Copy(
                    sourcePath,
                    Path.Combine(StagingDirectory, bundleName),
                    true
                );
            }

            var missingMappings = translation
                .Objects.Keys.Where(key => !identifiers.UsedMappings.Contains(key))
                .Select(key => key.ToString())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var receipt = new LinkedAddressableTranslatedContentBuild
            {
                SchemaVersion = 2,
                SceneBundleName = SceneBundleName,
                AssetBundleName =
                    consumerPaths.Length > 0 ? AssetBundleName : null,
                ScenePaths = scenePaths,
                ConsumerAssetPaths = consumerPaths,
                UsedDescriptorPaths = usedDescriptorPaths,
                StagedBundleNames = stagedBundleNames.ToArray(),
                DiscardedProxyBundleNames = Array.Empty<string>(),
                ExternalSerializedFiles = translation
                    .Objects.Values.Select(
                        identity => identity.SourceSerializedFileName
                    )
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                TranslationCount = translation.Objects.Count,
                UsedTranslationCount = identifiers.UsedMappings.Count,
                UnusedTranslations = missingMappings
            };
            File.WriteAllText(ReceiptPath, JsonUtility.ToJson(receipt, true));
            LinkedAddressableRuntimeManifestBuilder.RebuildForPlayer(false);

            if (logResult)
            {
                Debug.Log(
                    $"[KSP2UnityTools.LinkedAddressables.Build] Built "
                        + $"{stagedBundleNames.Count} translated target bundle(s), "
                        + $"and used {identifiers.UsedMappings.Count} of "
                        + $"{translation.Objects.Count} source-object translations. "
                        + "No proxy or external payload bundle was produced. "
                        + $"Staging: '{Path.GetFullPath(StagingDirectory)}'."
                );
            }

            return receipt;
        }

        private static LinkedAddressableTranslationTable BuildTranslationTable(
            IReadOnlyList<string> descriptorPaths
        )
        {
            var objects = new Dictionary<
                LinkedAddressableTargetObjectKey,
                LinkedAddressableSourceIdentity
            >();

            foreach (var descriptorPath in descriptorPaths)
            {
                var sourceMap = AssetDatabase
                    .LoadAllAssetsAtPath(descriptorPath)
                    .OfType<LinkedAddressableSourceMap>()
                    .SingleOrDefault();
                if (sourceMap == null || sourceMap.Objects.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Linked asset '{descriptorPath}' has no source-object map. "
                            + "Reimport it before building translated content."
                    );
                }

                foreach (var sourceObject in sourceMap.Objects)
                {
                    if (
                        !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                            sourceObject.Target,
                            out string guid,
                            out long localId
                        )
                        || string.IsNullOrWhiteSpace(guid)
                    )
                    {
                        throw new InvalidOperationException(
                            $"Source-map target '{sourceObject.Target?.name}' in "
                                + $"'{descriptorPath}' has no persistent target identity."
                        );
                    }

                    var key = new LinkedAddressableTargetObjectKey(
                        guid,
                        localId
                    );
                    var identity = new LinkedAddressableSourceIdentity(
                        sourceObject.SourceBundleFileName,
                        sourceObject.SourceSerializedFileName,
                        sourceObject.SourcePathId,
                        sourceObject.SourceType
                    );
                    if (
                        objects.TryGetValue(key, out var existing)
                        && !existing.Equals(identity)
                    )
                    {
                        throw new InvalidOperationException(
                            $"Target object '{key}' maps to conflicting external "
                                + "source identities."
                        );
                    }
                    objects[key] = identity;
                }
            }

            return new LinkedAddressableTranslationTable(objects);
        }

        private static IList<IBuildTask> CreateBuildTasks()
        {
            var tasks = DefaultBuildTasks.Create(
                DefaultBuildTasks.Preset.AssetBundleShaderAndScriptExtraction
            );
            var externalReferences = new LinkedAddressableExternalReferenceContext();
            var packingIndex = tasks
                .Select((task, index) => new { Task = task, Index = index })
                .Single(
                    entry =>
                        entry.Task
                            is UnityEditor.Build.Pipeline.Tasks.GenerateBundlePacking
                )
                .Index;
            tasks.Insert(
                packingIndex,
                new FilterLinkedAddressableReferences(externalReferences)
            );

            var bundleMapsIndex = tasks
                .Select((task, index) => new { Task = task, Index = index })
                .Single(
                    entry =>
                        entry.Task
                            is UnityEditor.Build.Pipeline.Tasks.GenerateBundleMaps
                )
                .Index;
            tasks.Insert(
                bundleMapsIndex + 1,
                new InjectLinkedAddressableReferences(externalReferences)
            );
            return tasks;
        }

        private static string[] FindConsumerAssets(
            IReadOnlyCollection<string> descriptorPaths,
            IReadOnlyCollection<string> scenePaths
        )
        {
            var descriptors = new HashSet<string>(
                descriptorPaths,
                StringComparer.OrdinalIgnoreCase
            );
            var scenes = new HashSet<string>(
                scenePaths,
                StringComparer.OrdinalIgnoreCase
            );
            var excludedExtensions = new HashSet<string>(
                new[]
                {
                    ".cs",
                    ".dll",
                    ".asmdef",
                    ".asmref",
                    ".meta",
                    ".unity"
                },
                StringComparer.OrdinalIgnoreCase
            );
            return AssetDatabase
                .GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal))
                .Where(path => !descriptors.Contains(path) && !scenes.Contains(path))
                .Where(path => !AssetDatabase.IsValidFolder(path))
                .Where(path => !excludedExtensions.Contains(Path.GetExtension(path)))
                .Where(
                    path =>
                        !string.Equals(
                            path,
                            LinkedAddressableRuntimeManifestBuilder.ManifestAssetPath,
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Where(
                    path =>
                        AssetDatabase
                            .GetDependencies(path, false)
                            .Any(dependency => descriptors.Contains(dependency))
                )
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] FindUsedDescriptorPaths(
            IReadOnlyCollection<string> descriptorPaths,
            IEnumerable<string> rootPaths
        )
        {
            var descriptors = new HashSet<string>(
                descriptorPaths,
                StringComparer.OrdinalIgnoreCase
            );
            return rootPaths
                .SelectMany(path => AssetDatabase.GetDependencies(path, true))
                .Where(descriptors.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void PrepareOutputDirectory()
        {
            var fullOutputPath = Path.GetFullPath(OutputDirectory);
            var fullLibraryPath = Path.GetFullPath("Library");
            if (
                !fullOutputPath.StartsWith(
                    fullLibraryPath + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    $"Translated content output escaped the project Library: "
                        + $"'{fullOutputPath}'."
                );
            }

            if (Directory.Exists(fullOutputPath))
                Directory.Delete(fullOutputPath, true);
            Directory.CreateDirectory(fullOutputPath);
        }

    }

    [Serializable]
    public sealed class LinkedAddressableTranslatedContentBuild
    {
        public int SchemaVersion;
        public string SceneBundleName;
        public string AssetBundleName;
        public string[] ScenePaths;
        public string[] ConsumerAssetPaths;
        public string[] UsedDescriptorPaths;
        public string[] StagedBundleNames;
        public string[] DiscardedProxyBundleNames;
        public string[] ExternalSerializedFiles;
        public int TranslationCount;
        public int UsedTranslationCount;
        public string[] UnusedTranslations;
    }

    internal sealed class LinkedAddressablePackedIdentifiers
        : IDeterministicIdentifiers,
            ILinkedAddressableExternalReferences
    {
        private readonly PrefabPackedIdentifiers fallback =
            new PrefabPackedIdentifiers();
        private readonly IReadOnlyDictionary<
            LinkedAddressableTargetObjectKey,
            LinkedAddressableSourceIdentity
        > translations;
        private readonly HashSet<LinkedAddressableTargetObjectKey> usedMappings =
            new HashSet<LinkedAddressableTargetObjectKey>();

        public LinkedAddressablePackedIdentifiers(
            IReadOnlyDictionary<
                LinkedAddressableTargetObjectKey,
                LinkedAddressableSourceIdentity
            > translations
        )
        {
            this.translations = translations;
        }

        public ISet<LinkedAddressableTargetObjectKey> UsedMappings =>
            usedMappings;

        public string GenerateInternalFileName(string name)
        {
            return fallback.GenerateInternalFileName(name);
        }

        public long SerializationIndexFromObjectIdentifier(
            ObjectIdentifier objectId
        )
        {
            var key = new LinkedAddressableTargetObjectKey(
                objectId.guid.ToString(),
                objectId.localIdentifierInFile
            );
            if (translations.TryGetValue(key, out var source))
            {
                usedMappings.Add(key);
                return source.SourcePathId;
            }

            return fallback.SerializationIndexFromObjectIdentifier(objectId);
        }

        public bool TryGetExternalReference(
            ObjectIdentifier objectId,
            out string internalFileName,
            out long sourcePathId
        )
        {
            var key = new LinkedAddressableTargetObjectKey(
                objectId.guid.ToString(),
                objectId.localIdentifierInFile
            );
            if (translations.TryGetValue(key, out var source))
            {
                usedMappings.Add(key);
                internalFileName =
                    $"archive:/{source.SourceSerializedFileName}/"
                    + source.SourceSerializedFileName;
                sourcePathId = source.SourcePathId;
                return true;
            }

            internalFileName = null;
            sourcePathId = 0;
            return false;
        }
    }

    internal readonly struct LinkedAddressableTargetObjectKey
        : IEquatable<LinkedAddressableTargetObjectKey>
    {
        public LinkedAddressableTargetObjectKey(string guid, long localId)
        {
            Guid = guid ?? string.Empty;
            LocalId = localId;
        }

        private string Guid { get; }

        private long LocalId { get; }

        public bool Equals(LinkedAddressableTargetObjectKey other)
        {
            return LocalId == other.LocalId
                && string.Equals(Guid, other.Guid, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object other)
        {
            return other is LinkedAddressableTargetObjectKey key && Equals(key);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.OrdinalIgnoreCase.GetHashCode(Guid) * 397)
                    ^ LocalId.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{Guid}:{LocalId}";
        }
    }

    internal readonly struct LinkedAddressableSourceIdentity
        : IEquatable<LinkedAddressableSourceIdentity>
    {
        public LinkedAddressableSourceIdentity(
            string sourceBundleFileName,
            string sourceSerializedFileName,
            long sourcePathId,
            string sourceType
        )
        {
            SourceBundleFileName = sourceBundleFileName;
            SourceSerializedFileName = sourceSerializedFileName;
            SourcePathId = sourcePathId;
            SourceType = sourceType;
        }

        public string SourceBundleFileName { get; }

        public string SourceSerializedFileName { get; }

        public long SourcePathId { get; }

        private string SourceType { get; }

        public bool Equals(LinkedAddressableSourceIdentity other)
        {
            return SourcePathId == other.SourcePathId
                && string.Equals(
                    SourceBundleFileName,
                    other.SourceBundleFileName,
                    StringComparison.OrdinalIgnoreCase
                )
                && string.Equals(
                    SourceSerializedFileName,
                    other.SourceSerializedFileName,
                    StringComparison.OrdinalIgnoreCase
                )
                && string.Equals(SourceType, other.SourceType, StringComparison.Ordinal);
        }

        public override bool Equals(object other)
        {
            return other is LinkedAddressableSourceIdentity identity
                && Equals(identity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourcePathId.GetHashCode();
                hash =
                    (hash * 397)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        SourceBundleFileName ?? string.Empty
                    );
                hash =
                    (hash * 397)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        SourceSerializedFileName ?? string.Empty
                    );
                return (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(
                        SourceType ?? string.Empty
                    );
            }
        }
    }

    internal sealed class LinkedAddressableTranslationTable
    {
        public LinkedAddressableTranslationTable(
            IReadOnlyDictionary<
                LinkedAddressableTargetObjectKey,
                LinkedAddressableSourceIdentity
            > objects
        )
        {
            Objects = objects;
        }

        public IReadOnlyDictionary<
            LinkedAddressableTargetObjectKey,
            LinkedAddressableSourceIdentity
        > Objects { get; }
    }
}
