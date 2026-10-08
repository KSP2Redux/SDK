using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    /// <summary>
    /// Stable external identity for one persistent object materialized by a
    /// <c>.bkaddressable</c> importer.
    /// </summary>
    public readonly struct LinkedAddressableEditorObjectIdentity
        : IEquatable<LinkedAddressableEditorObjectIdentity>
    {
        internal LinkedAddressableEditorObjectIdentity(
            string descriptorPath,
            LinkedAddressableDescriptor descriptor,
            string targetGuid,
            long targetLocalFileId,
            LinkedAddressableSourceObject sourceObject
        )
        {
            DescriptorPath = descriptorPath;
            SourceId = descriptor.SourceId;
            CatalogId = descriptor.CatalogId;
            CatalogHash = descriptor.CatalogHash;
            Address = descriptor.Address;
            AssetType = descriptor.AssetType;
            TargetGuid = targetGuid;
            TargetLocalFileId = targetLocalFileId;
            SourceBundleFileName = sourceObject.SourceBundleFileName;
            SourceSerializedFileName = sourceObject.SourceSerializedFileName;
            SourcePathId = sourceObject.SourcePathId;
            SourceType = sourceObject.SourceType;
        }

        public string DescriptorPath { get; }

        public string SourceId { get; }

        public string CatalogId { get; }

        public string CatalogHash { get; }

        public string Address { get; }

        public string AssetType { get; }

        public string TargetGuid { get; }

        public long TargetLocalFileId { get; }

        public string SourceBundleFileName { get; }

        public string SourceSerializedFileName { get; }

        public long SourcePathId { get; }

        public string SourceType { get; }

        public bool Equals(LinkedAddressableEditorObjectIdentity other)
        {
            return SourcePathId == other.SourcePathId
                && string.Equals(SourceId, other.SourceId, StringComparison.Ordinal)
                && string.Equals(CatalogId, other.CatalogId, StringComparison.Ordinal)
                && string.Equals(Address, other.Address, StringComparison.Ordinal)
                && string.Equals(
                    SourceSerializedFileName,
                    other.SourceSerializedFileName,
                    StringComparison.OrdinalIgnoreCase
                )
                && string.Equals(SourceType, other.SourceType, StringComparison.Ordinal);
        }

        public override bool Equals(object other)
        {
            return other is LinkedAddressableEditorObjectIdentity identity
                && Equals(identity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StringComparer.Ordinal.GetHashCode(SourceId ?? string.Empty);
                hash =
                    (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(CatalogId ?? string.Empty);
                hash =
                    (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(Address ?? string.Empty);
                hash =
                    (hash * 397)
                    ^ StringComparer.OrdinalIgnoreCase.GetHashCode(
                        SourceSerializedFileName ?? string.Empty
                    );
                hash = (hash * 397) ^ SourcePathId.GetHashCode();
                return (hash * 397)
                    ^ StringComparer.Ordinal.GetHashCode(SourceType ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return $"{SourceId}:{CatalogId}:{Address}:"
                + $"{SourceSerializedFileName}:{SourcePathId}:{SourceType}";
        }
    }

    /// <summary>
    /// Public read-only access to linked Addressables editor identities.
    /// </summary>
    public static class LinkedAddressableEditorIdentity
    {
        /// <summary>
        /// Resolves a persistent linked object to its canonical external
        /// CAB/path-ID identity.
        /// </summary>
        public static bool TryGet(
            UnityEngine.Object target,
            out LinkedAddressableEditorObjectIdentity identity
        )
        {
            identity = default;
            if (target == null)
                return false;

            var descriptorPath = AssetDatabase.GetAssetPath(target);
            if (
                string.IsNullOrWhiteSpace(descriptorPath)
                || !descriptorPath.EndsWith(
                    "." + LinkedAddressableAssetImporter.Extension,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return false;
            }

            var sourceMap = LoadSourceMap(descriptorPath);
            if (sourceMap == null)
                return false;

            var sourceObject = sourceMap.Objects.FirstOrDefault(
                entry => ReferenceEquals(entry.Target, target) || entry.Target == target
            );
            if (sourceObject == null)
                return false;

            if (
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    target,
                    out string targetGuid,
                    out long targetLocalFileId
                )
                || string.IsNullOrWhiteSpace(targetGuid)
            )
            {
                return false;
            }

            var descriptor = LinkedAddressableAssetImporter.ReadDescriptor(
                descriptorPath
            );
            identity = new LinkedAddressableEditorObjectIdentity(
                descriptorPath,
                descriptor,
                targetGuid,
                targetLocalFileId,
                sourceObject
            );
            return true;
        }

        /// <summary>
        /// Returns every mapped object identity in one linked descriptor.
        /// </summary>
        public static IReadOnlyList<LinkedAddressableEditorObjectIdentity> GetAll(
            string descriptorPath
        )
        {
            if (string.IsNullOrWhiteSpace(descriptorPath))
                throw new ArgumentException(
                    "A linked descriptor path is required.",
                    nameof(descriptorPath)
                );

            var sourceMap = LoadSourceMap(descriptorPath);
            if (sourceMap == null)
            {
                throw new InvalidOperationException(
                    $"Linked asset '{descriptorPath}' has no source-object map. "
                        + "Reimport it before querying editor identities."
                );
            }

            var descriptor = LinkedAddressableAssetImporter.ReadDescriptor(
                descriptorPath
            );
            var result = new List<LinkedAddressableEditorObjectIdentity>(
                sourceMap.Objects.Count
            );
            foreach (var sourceObject in sourceMap.Objects)
            {
                if (
                    sourceObject.Target == null
                    || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                        sourceObject.Target,
                        out string targetGuid,
                        out long targetLocalFileId
                    )
                    || string.IsNullOrWhiteSpace(targetGuid)
                )
                {
                    throw new InvalidOperationException(
                        $"Source-map target '{sourceObject.Target?.name}' in "
                            + $"'{descriptorPath}' has no persistent target identity."
                    );
                }

                result.Add(
                    new LinkedAddressableEditorObjectIdentity(
                        descriptorPath,
                        descriptor,
                        targetGuid,
                        targetLocalFileId,
                        sourceObject
                    )
                );
            }

            return result;
        }

        private static LinkedAddressableSourceMap LoadSourceMap(
            string descriptorPath
        )
        {
            return AssetDatabase
                .LoadAllAssetsAtPath(descriptorPath)
                .OfType<LinkedAddressableSourceMap>()
                .SingleOrDefault();
        }
    }
}
