using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal sealed class LinkedAddressableSourceMap : ScriptableObject
    {
        public const string SubAssetIdentifier = "__ReduxSDKSourceMap";

        [SerializeField]
        private LinkedAddressableSourceObject[] objects =
            Array.Empty<LinkedAddressableSourceObject>();

        public IReadOnlyList<LinkedAddressableSourceObject> Objects => objects;

        public static LinkedAddressableSourceMap Create(
            LinkedAddressableDescriptor descriptor,
            LinkedAddressableMaterializedGraph graph
        )
        {
            var sourceMap = CreateInstance<LinkedAddressableSourceMap>();
            sourceMap.name = "Redux SDK Source Map";
            sourceMap.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            sourceMap.objects = LinkedAddressableSourceLocator
                .Resolve(descriptor, graph.SourceObjects)
                .ToArray();
            return sourceMap;
        }
    }

    [Serializable]
    internal sealed class LinkedAddressableSourceObject
    {
        [SerializeField]
        private UnityEngine.Object target;

        [SerializeField]
        private string sourceBundleFileName;

        [SerializeField]
        private string sourceSerializedFileName;

        [SerializeField]
        private long sourcePathId;

        [SerializeField]
        private string sourceType;

        public LinkedAddressableSourceObject(
            UnityEngine.Object target,
            string sourceBundleFileName,
            string sourceSerializedFileName,
            long sourcePathId,
            string sourceType
        )
        {
            this.target = target;
            this.sourceBundleFileName = sourceBundleFileName;
            this.sourceSerializedFileName = sourceSerializedFileName;
            this.sourcePathId = sourcePathId;
            this.sourceType = sourceType;
        }

        public UnityEngine.Object Target => target;

        public string SourceBundleFileName => sourceBundleFileName;

        public string SourceSerializedFileName => sourceSerializedFileName;

        public long SourcePathId => sourcePathId;

        public string SourceType => sourceType;
    }

    internal static class LinkedAddressableSourceLocator
    {
        private static readonly Dictionary<string, SourceBundleIndex> BundleIndexes =
            new Dictionary<string, SourceBundleIndex>(
                StringComparer.OrdinalIgnoreCase
            );

        public static IReadOnlyList<LinkedAddressableSourceObject> Resolve(
            LinkedAddressableDescriptor descriptor,
            IReadOnlyList<LinkedAddressableMaterializedSourceObject> sourceObjects
        )
        {
            if (sourceObjects == null || sourceObjects.Count == 0)
                return Array.Empty<LinkedAddressableSourceObject>();

            var bundlePaths = GetBundlePaths(descriptor).ToArray();
            if (bundlePaths.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Linked asset '{descriptor.Address}' declares no source bundles."
                );
            }

            var indexes = bundlePaths
                .Select(GetBundleIndex)
                .ToArray();
            var indexesBySerializedFileName = indexes
                .GroupBy(
                    index => index.SerializedFileName,
                    StringComparer.OrdinalIgnoreCase
                )
                .ToDictionary(
                    group => group.Key,
                    group => group.First(),
                    StringComparer.OrdinalIgnoreCase
                );
            var primaryIndex = indexes[0];
            var result = new List<LinkedAddressableSourceObject>(
                sourceObjects.Count
            );
            using (var semanticSession = new SemanticComparisonSession())
            {
                foreach (var sourceObject in sourceObjects)
                {
                    ResolveSourceObject(
                        descriptor,
                        sourceObject,
                        indexes,
                        indexesBySerializedFileName,
                        primaryIndex,
                        semanticSession,
                        result
                    );
                }
            }

            return result;
        }

        private static void ResolveSourceObject(
            LinkedAddressableDescriptor descriptor,
            LinkedAddressableMaterializedSourceObject sourceObject,
            IReadOnlyList<SourceBundleIndex> indexes,
            IReadOnlyDictionary<string, SourceBundleIndex>
                indexesBySerializedFileName,
            SourceBundleIndex primaryIndex,
            SemanticComparisonSession semanticSession,
            ICollection<LinkedAddressableSourceObject> result
        )
        {
            var sourceKey = new SourceObjectKey(
                sourceObject.SourceLocalId,
                sourceObject.SourceClassId
            );
            var matches = sourceObject.PreferBuiltinBundle
                ? indexes
                    .Where(index => index.IsBuiltinBundle)
                    .Where(
                        index =>
                            index.Contains(sourceKey)
                            || indexes.Count(candidate => candidate.IsBuiltinBundle)
                                == 1
                    )
                    .ToArray()
                : indexes
                    .Where(index => index.Contains(sourceKey))
                    .ToArray();

            if (matches.Length == 0 && sourceObject.OwnerHintLocalId != 0)
            {
                var ownerKey = new SourceObjectKey(
                    sourceObject.OwnerHintLocalId,
                    sourceObject.OwnerHintClassId
                );
                matches = indexes
                    .Where(index => index.Contains(ownerKey))
                    .ToArray();
            }

            if (matches.Length == 0 && sourceObject.IsMainSource)
                matches = new[] { primaryIndex };

            if (matches.Length == 0)
            {
                Debug.LogWarning(
                    $"[ReduxSDK.LinkedAddressables] Skipping optional editor-preview "
                        + $"mapping for external object path ID "
                        + $"{sourceObject.SourceLocalId} "
                        + $"('{sourceObject.SourceType}') in any declared bundle for "
                        + $"'{descriptor.Address}'. The linked root and resolvable "
                        + "hierarchy objects remain player-translatable."
                );
                return;
            }

            var distinctMatches = matches
                .GroupBy(
                    match => match.SerializedFileName,
                    StringComparer.OrdinalIgnoreCase
                )
                .Select(group => group.First())
                .ToArray();
            SourceBundleIndex sourceIndex;
            if (distinctMatches.Length == 1)
            {
                sourceIndex = distinctMatches[0];
            }
            else
            {
                var orderedMatches = distinctMatches
                    .OrderBy(
                        match => match.SerializedFileName,
                        StringComparer.Ordinal
                    )
                    .ToArray();
                sourceIndex = orderedMatches[0];
                if (
                    orderedMatches
                        .Skip(1)
                        .Any(
                            match =>
                                !sourceIndex.IsSemanticallyEquivalentTo(
                                    match,
                                    sourceObject.SourceLocalId,
                                    indexesBySerializedFileName,
                                    semanticSession
                                )
                        )
                )
                {
                    throw new InvalidOperationException(
                        $"External object path ID {sourceObject.SourceLocalId} "
                            + $"('{sourceObject.SourceType}', "
                            + $"name '{sourceObject.SourceName}') is ambiguous across source "
                            + $"serialized files: {string.Join(", ", distinctMatches.Select(match => match.SerializedFileName))}."
                    );
                }
            }

            result.Add(
                new LinkedAddressableSourceObject(
                    sourceObject.Target,
                    sourceIndex.BundleFileName,
                    sourceIndex.SerializedFileName,
                    sourceObject.SourceLocalId,
                    sourceObject.SourceType
                )
            );
        }

        private static IEnumerable<string> GetBundlePaths(
            LinkedAddressableDescriptor descriptor
        )
        {
            var settings =
                ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var bundleDirectory = Path.Combine(
                settings.AddressableAssetsPath,
                "StandaloneWindows64"
            );
            return (descriptor.Dependencies ?? Array.Empty<LinkedAddressableDependency>())
                .Select(
                    dependency =>
                        Path.GetFileName(dependency.InternalId?.Replace('\\', '/'))
                )
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(fileName => Path.GetFullPath(Path.Combine(bundleDirectory, fileName)));
        }

        private static SourceBundleIndex GetBundleIndex(string bundlePath)
        {
            if (!File.Exists(bundlePath))
            {
                throw new FileNotFoundException(
                    "A declared linked Addressables bundle is missing.",
                    bundlePath
                );
            }

            var fileInfo = new FileInfo(bundlePath);
            if (
                BundleIndexes.TryGetValue(bundlePath, out var cached)
                && cached.FileLength == fileInfo.Length
                && cached.LastWriteUtcTicks == fileInfo.LastWriteTimeUtc.Ticks
            )
            {
                return cached;
            }

            var index = BuildBundleIndex(
                bundlePath,
                fileInfo.Length,
                fileInfo.LastWriteTimeUtc.Ticks
            );
            BundleIndexes[bundlePath] = index;
            return index;
        }

        private static SourceBundleIndex BuildBundleIndex(
            string bundlePath,
            long fileLength,
            long lastWriteUtcTicks
        )
        {
            var serializedFileName = GetSerializedFileName(bundlePath);
            var objectKeys = new HashSet<SourceObjectKey>();
            var manager = new AssetsManager();
            try
            {
                var bundle = manager.LoadBundleFile(bundlePath, true);
                var fileNames = bundle.file.GetAllFileNames();
                var fileIndex = Enumerable
                    .Range(0, fileNames.Count)
                    .Where(
                        index =>
                            string.Equals(
                                fileNames[index],
                                serializedFileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    .DefaultIfEmpty(-1)
                    .First();
                if (fileIndex < 0)
                {
                    throw new InvalidOperationException(
                        $"Could not find serialized file '{serializedFileName}' "
                            + $"inside linked bundle '{bundlePath}'."
                    );
                }

                var file = manager.LoadAssetsFileFromBundle(bundle, fileIndex);
                foreach (var info in file.file.AssetInfos)
                    objectKeys.Add(
                        new SourceObjectKey(info.PathId, info.TypeId)
                    );
            }
            finally
            {
                manager.UnloadAll(true);
            }

            return new SourceBundleIndex(
                bundlePath,
                Path.GetFileName(bundlePath),
                serializedFileName,
                fileLength,
                lastWriteUtcTicks,
                objectKeys
            );
        }

        private static string GetSerializedFileName(string bundlePath)
        {
            var manager = new AssetsManager();
            try
            {
                var bundle = manager.LoadBundleFile(bundlePath, true);
                var serializedFiles = bundle.file
                    .GetAllFileNames()
                    .Select(
                        (fileName, index) =>
                            new { FileName = fileName, Index = index }
                    )
                    .Where(
                        entry =>
                            bundle.file.IsAssetsFile(entry.Index)
                            && !IsStreamedResourceFile(entry.FileName)
                    )
                    .Select(entry => entry.FileName)
                    .ToArray();
                if (serializedFiles.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Linked Addressables bundle '{bundlePath}' contains "
                            + $"{serializedFiles.Length} serialized files; exactly one is "
                            + "required for source-object translation."
                    );
                }

                return serializedFiles[0];
            }
            finally
            {
                manager.UnloadAll(true);
            }
        }

        private static bool IsStreamedResourceFile(string fileName)
        {
            return fileName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase)
                || fileName.EndsWith(
                    ".resource",
                    StringComparison.OrdinalIgnoreCase
                )
                || fileName.EndsWith(
                    ".resources",
                    StringComparison.OrdinalIgnoreCase
                );
        }

        private readonly struct SourceObjectKey : IEquatable<SourceObjectKey>
        {
            public SourceObjectKey(long localId, int classId)
            {
                LocalId = localId;
                ClassId = classId;
            }

            private long LocalId { get; }

            private int ClassId { get; }

            public bool Equals(SourceObjectKey other)
            {
                return LocalId == other.LocalId
                    && ClassId == other.ClassId;
            }

            public override bool Equals(object other)
            {
                return other is SourceObjectKey key && Equals(key);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (LocalId.GetHashCode() * 397)
                        ^ ClassId;
                }
            }
        }

        private sealed class SourceBundleIndex
        {
            private readonly ISet<SourceObjectKey> objectKeys;
            private readonly Dictionary<long, ParsedSourceAsset> parsedAssets =
                new Dictionary<long, ParsedSourceAsset>();

            public SourceBundleIndex(
                string bundlePath,
                string bundleFileName,
                string serializedFileName,
                long fileLength,
                long lastWriteUtcTicks,
                ISet<SourceObjectKey> objectKeys
            )
            {
                BundlePath = bundlePath;
                BundleFileName = bundleFileName;
                SerializedFileName = serializedFileName;
                FileLength = fileLength;
                LastWriteUtcTicks = lastWriteUtcTicks;
                this.objectKeys = objectKeys;
            }

            public string BundlePath { get; }

            public string BundleFileName { get; }

            public string SerializedFileName { get; }

            public bool IsBuiltinBundle =>
                BundleFileName.IndexOf(
                    "_unitybuiltinassets_",
                    StringComparison.OrdinalIgnoreCase
                )
                >= 0;

            public long FileLength { get; }

            public long LastWriteUtcTicks { get; }

            public bool Contains(SourceObjectKey key)
            {
                return objectKeys.Contains(key);
            }

            public bool IsSemanticallyEquivalentTo(
                SourceBundleIndex other,
                long pathId,
                IReadOnlyDictionary<string, SourceBundleIndex> indexes,
                SemanticComparisonSession session
            )
            {
                return AreSemanticallyEquivalent(
                    this,
                    pathId,
                    other,
                    pathId,
                    indexes,
                    session,
                    new HashSet<string>(StringComparer.Ordinal)
                );
            }

            private ParsedSourceAsset GetParsedAsset(
                long pathId,
                SemanticComparisonSession session
            )
            {
                if (parsedAssets.TryGetValue(pathId, out var cached))
                    return cached;

                ParsedSourceAsset parsed = null;
                try
                {
                    var reader = session.GetReader(this);
                    var info = reader.File.file.GetAssetInfo(pathId);
                    if (info == null)
                        return CacheParsedAsset(pathId, null);

                    var baseField = reader.Manager.GetBaseField(
                        reader.File,
                        info
                    );
                    if (baseField == null || baseField.IsDummy)
                        return CacheParsedAsset(pathId, null);

                    var references = new List<SourcePPtr>();
                    if (
                        !NormalizePPtrs(
                            baseField,
                            reader.File.file.Metadata.Externals,
                            references
                        )
                    )
                    {
                        return CacheParsedAsset(pathId, null);
                    }

                    var streamHashes = new List<byte[]>();
                    if (
                        !NormalizeStreams(
                            baseField,
                            reader.Bundle,
                            streamHashes
                        )
                    )
                    {
                        return CacheParsedAsset(pathId, null);
                    }

                    parsed = new ParsedSourceAsset(
                        info.TypeId,
                        baseField.WriteToByteArray(false),
                        references,
                        streamHashes
                    );
                    return CacheParsedAsset(pathId, parsed);
                }
                catch
                {
                    return CacheParsedAsset(pathId, null);
                }
            }

            private ParsedSourceAsset CacheParsedAsset(
                long pathId,
                ParsedSourceAsset parsed
            )
            {
                parsedAssets[pathId] = parsed;
                return parsed;
            }

            private bool NormalizePPtrs(
                AssetTypeValueField field,
                IReadOnlyList<AssetsFileExternal> externals,
                ICollection<SourcePPtr> references
            )
            {
                if (
                    field?.TemplateField?.Type != null
                    && field.TemplateField.Type.StartsWith(
                        "PPtr<",
                        StringComparison.Ordinal
                    )
                )
                {
                    var fileIdField = field["m_FileID"];
                    var pathIdField = field["m_PathID"];
                    if (fileIdField.IsDummy || pathIdField.IsDummy)
                        return false;

                    var fileId = fileIdField.AsInt;
                    var referencedPathId = pathIdField.AsLong;
                    string serializedFileName = null;
                    if (referencedPathId != 0)
                    {
                        if (fileId == 0)
                        {
                            serializedFileName = SerializedFileName;
                        }
                        else
                        {
                            if (fileId < 1 || fileId > externals.Count)
                                return false;
                            serializedFileName = GetExternalSerializedFileName(
                                externals[fileId - 1].OriginalPathName
                            );
                            if (string.IsNullOrWhiteSpace(serializedFileName))
                                return false;
                        }
                    }

                    references.Add(
                        new SourcePPtr(
                            serializedFileName,
                            referencedPathId
                        )
                    );
                    fileIdField.AsInt = 0;
                    pathIdField.AsLong = 0;
                    return true;
                }

                if (field?.Children == null)
                    return true;
                foreach (var child in field.Children)
                {
                    if (!NormalizePPtrs(child, externals, references))
                        return false;
                }

                return true;
            }

            private static bool NormalizeStreams(
                AssetTypeValueField field,
                BundleFileInstance bundle,
                ICollection<byte[]> streamHashes
            )
            {
                var pathField = field?["path"];
                var offsetField = field?["offset"];
                var sizeField = field?["size"];
                if (
                    pathField != null
                    && offsetField != null
                    && sizeField != null
                    && !pathField.IsDummy
                    && !offsetField.IsDummy
                    && !sizeField.IsDummy
                    && !string.IsNullOrWhiteSpace(pathField.AsString)
                    && sizeField.AsULong > 0
                )
                {
                    var resourceName = Path.GetFileName(
                        pathField.AsString.Replace('\\', '/')
                    );
                    var resourceIndex = bundle.file.GetFileIndex(resourceName);
                    if (resourceIndex < 0 || sizeField.AsULong > int.MaxValue)
                        return false;

                    bundle.file.GetFileRange(
                        resourceIndex,
                        out var resourceOffset,
                        out var resourceLength
                    );
                    var streamOffset = offsetField.AsULong;
                    var streamSize = sizeField.AsULong;
                    if (
                        resourceOffset < 0
                        || resourceLength <= 0
                        || streamOffset > (ulong)resourceLength
                        || streamSize
                            > (ulong)resourceLength - streamOffset
                    )
                    {
                        return false;
                    }

                    bundle.file.DataReader.Position =
                        resourceOffset + (long)streamOffset;
                    var payload = bundle.file.DataReader.ReadBytes(
                        (int)streamSize
                    );
                    using (var sha256 = SHA256.Create())
                        streamHashes.Add(sha256.ComputeHash(payload));

                    pathField.AsString = string.Empty;
                    offsetField.AsULong = 0;
                }

                if (field?.Children == null)
                    return true;
                foreach (var child in field.Children)
                {
                    if (!NormalizeStreams(child, bundle, streamHashes))
                        return false;
                }

                return true;
            }

            private static string GetExternalSerializedFileName(
                string originalPath
            )
            {
                return (originalPath ?? string.Empty)
                    .Replace('\\', '/')
                    .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                    .LastOrDefault();
            }

            private static bool AreSemanticallyEquivalent(
                SourceBundleIndex leftIndex,
                long leftPathId,
                SourceBundleIndex rightIndex,
                long rightPathId,
                IReadOnlyDictionary<string, SourceBundleIndex> indexes,
                SemanticComparisonSession session,
                ISet<string> visited
            )
            {
                var pairKey =
                    $"{leftIndex.SerializedFileName}:{leftPathId}|"
                    + $"{rightIndex.SerializedFileName}:{rightPathId}";
                if (!visited.Add(pairKey))
                    return true;

                var left = leftIndex.GetParsedAsset(leftPathId, session);
                var right = rightIndex.GetParsedAsset(rightPathId, session);
                if (
                    left == null
                    || right == null
                    || left.TypeId != right.TypeId
                    || !left.NormalizedData.SequenceEqual(right.NormalizedData)
                    || left.References.Count != right.References.Count
                    || left.StreamHashes.Count != right.StreamHashes.Count
                )
                {
                    return false;
                }

                for (var index = 0; index < left.StreamHashes.Count; index++)
                {
                    if (
                        !left.StreamHashes[index]
                            .SequenceEqual(right.StreamHashes[index])
                    )
                    {
                        return false;
                    }
                }

                for (var index = 0; index < left.References.Count; index++)
                {
                    var leftReference = left.References[index];
                    var rightReference = right.References[index];
                    if (
                        leftReference.PathId == 0
                        || rightReference.PathId == 0
                    )
                    {
                        if (leftReference.PathId != rightReference.PathId)
                            return false;
                        continue;
                    }

                    if (
                        string.Equals(
                            leftReference.SerializedFileName,
                            rightReference.SerializedFileName,
                            StringComparison.OrdinalIgnoreCase
                        )
                        && leftReference.PathId == rightReference.PathId
                    )
                    {
                        continue;
                    }

                    if (
                        !indexes.TryGetValue(
                            leftReference.SerializedFileName,
                            out var leftReferenceIndex
                        )
                        || !indexes.TryGetValue(
                            rightReference.SerializedFileName,
                            out var rightReferenceIndex
                        )
                        || !AreSemanticallyEquivalent(
                            leftReferenceIndex,
                            leftReference.PathId,
                            rightReferenceIndex,
                            rightReference.PathId,
                            indexes,
                            session,
                            visited
                        )
                    )
                    {
                        return false;
                    }
                }

                return true;
            }

            private sealed class ParsedSourceAsset
            {
                public ParsedSourceAsset(
                    int typeId,
                    byte[] normalizedData,
                    IReadOnlyList<SourcePPtr> references,
                    IReadOnlyList<byte[]> streamHashes
                )
                {
                    TypeId = typeId;
                    NormalizedData = normalizedData;
                    References = references;
                    StreamHashes = streamHashes;
                }

                public int TypeId { get; }

                public byte[] NormalizedData { get; }

                public IReadOnlyList<SourcePPtr> References { get; }

                public IReadOnlyList<byte[]> StreamHashes { get; }
            }

            private readonly struct SourcePPtr
            {
                public SourcePPtr(
                    string serializedFileName,
                    long pathId
                )
                {
                    SerializedFileName = serializedFileName;
                    PathId = pathId;
                }

                public string SerializedFileName { get; }

                public long PathId { get; }
            }
        }

        private sealed class SemanticComparisonSession : IDisposable
        {
            private readonly Dictionary<SourceBundleIndex, SourceBundleReader>
                readers =
                    new Dictionary<SourceBundleIndex, SourceBundleReader>();

            public SourceBundleReader GetReader(SourceBundleIndex index)
            {
                if (readers.TryGetValue(index, out var reader))
                    return reader;
                reader = new SourceBundleReader(
                    index.BundlePath,
                    index.SerializedFileName
                );
                readers.Add(index, reader);
                return reader;
            }

            public void Dispose()
            {
                foreach (var reader in readers.Values)
                    reader.Dispose();
                readers.Clear();
            }
        }

        private sealed class SourceBundleReader : IDisposable
        {
            public SourceBundleReader(
                string bundlePath,
                string serializedFileName
            )
            {
                Manager = new AssetsManager();
                try
                {
                    Bundle = Manager.LoadBundleFile(bundlePath, true);
                    var fileNames = Bundle.file.GetAllFileNames();
                    var fileIndex = Enumerable
                        .Range(0, fileNames.Count)
                        .Where(
                            index =>
                                string.Equals(
                                    fileNames[index],
                                    serializedFileName,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                        .DefaultIfEmpty(-1)
                        .First();
                    if (fileIndex < 0)
                    {
                        throw new InvalidOperationException(
                            $"Could not find serialized file "
                                + $"'{serializedFileName}' inside linked bundle "
                                + $"'{bundlePath}'."
                        );
                    }

                    File = Manager.LoadAssetsFileFromBundle(
                        Bundle,
                        fileIndex
                    );
                }
                catch
                {
                    Manager.UnloadAll(true);
                    throw;
                }
            }

            public AssetsManager Manager { get; }

            public BundleFileInstance Bundle { get; }

            public AssetsFileInstance File { get; }

            public void Dispose()
            {
                Manager.UnloadAll(true);
            }
        }
    }
}
