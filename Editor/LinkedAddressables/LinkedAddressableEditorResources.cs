using AssetsTools.NET.Extra;
using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal static class LinkedAddressableEditorResources
    {
        private const string ArchivePrefix = "archive:/";

        public static void RewriteExternalStreams(
            AssetImportContext context,
            LinkedAddressableDescriptor descriptor,
            LinkedAddressableMaterializedGraph graph,
            LinkedAddressableSourceMap sourceMap
        )
        {
            var mirroredResources = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var asset in EnumerateOwnedObjects(graph))
            {
                SerializedObject serializedObject;
                try
                {
                    serializedObject = new SerializedObject(asset);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                var changed = false;
                var property = serializedObject.GetIterator();
                var enterChildren = true;
                while (property.Next(enterChildren))
                {
                    enterChildren = true;
                    if (property.propertyType != SerializedPropertyType.String)
                        continue;
                    if (!IsStreamResourceProperty(serializedObject, property))
                        continue;

                    var source = property.stringValue;
                    if (
                        string.IsNullOrWhiteSpace(source)
                        || !source.StartsWith(
                            ArchivePrefix,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        continue;
                    }

                    var sourceBundleFileName = GetSourceBundleFileName(
                        sourceMap,
                        asset
                    );
                    var mirrorKey = sourceBundleFileName + "\n" + source;
                    if (
                        !mirroredResources.TryGetValue(
                            mirrorKey,
                            out var artifactPath
                        )
                    )
                    {
                        artifactPath = MirrorResource(
                            context,
                            descriptor,
                            source,
                            sourceBundleFileName
                        );
                        mirroredResources.Add(mirrorKey, artifactPath);
                    }

                    property.stringValue = artifactPath;
                    changed = true;
                }

                if (changed)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static bool IsStreamResourceProperty(
            SerializedObject serializedObject,
            SerializedProperty property
        )
        {
            const string SourceSuffix = ".m_Source";
            const string PathSuffix = ".path";
            var propertyPath = property.propertyPath;
            if (
                propertyPath.EndsWith(
                    SourceSuffix,
                    StringComparison.Ordinal
                )
            )
            {
                var parentPath = propertyPath.Substring(
                    0,
                    propertyPath.Length - SourceSuffix.Length
                );
                return serializedObject.FindProperty(parentPath + ".m_Size") != null;
            }

            if (propertyPath.EndsWith(PathSuffix, StringComparison.Ordinal))
            {
                var parentPath = propertyPath.Substring(
                    0,
                    propertyPath.Length - PathSuffix.Length
                );
                return serializedObject.FindProperty(parentPath + ".size") != null;
            }

            return false;
        }

        private static IEnumerable<UnityEngine.Object> EnumerateOwnedObjects(
            LinkedAddressableMaterializedGraph graph
        )
        {
            var roots = graph.SubAssets
                .Select(subAsset => subAsset.Asset)
                .Prepend(graph.MainAsset);
            foreach (var root in roots)
            {
                if (root is GameObject gameObject)
                {
                    foreach (
                        var transform in gameObject.GetComponentsInChildren<Transform>(true)
                    )
                    {
                        yield return transform.gameObject;
                        foreach (var component in transform.GetComponents<Component>())
                        {
                            if (component != null)
                                yield return component;
                        }
                    }
                }
                else
                {
                    yield return root;
                }
            }
        }

        private static string MirrorResource(
            AssetImportContext context,
            LinkedAddressableDescriptor descriptor,
            string archivePath,
            string sourceBundleFileName
        )
        {
            var resourceName = Path.GetFileName(archivePath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(resourceName))
            {
                throw new InvalidOperationException(
                    $"Linked asset '{descriptor.Address}' has an invalid external "
                        + $"resource path '{archivePath}'."
                );
            }

            var settings = ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var bundleDirectory = Path.Combine(
                settings.AddressableAssetsPath,
                "StandaloneWindows64"
            );
            foreach (
                var bundleFileName in GetBundleFileNames(
                    descriptor,
                    sourceBundleFileName
                )
            )
            {
                var bundlePath = Path.Combine(bundleDirectory, bundleFileName);
                if (!File.Exists(bundlePath))
                    continue;

                var manager = new AssetsManager();
                try
                {
                    var bundle = manager.LoadBundleFile(bundlePath, true);
                    var resourceIndex = bundle.file.GetFileIndex(resourceName);
                    if (resourceIndex < 0)
                        continue;

                    bundle.file.GetFileRange(
                        resourceIndex,
                        out var resourceOffset,
                        out var resourceLength
                    );
                    if (resourceOffset < 0 || resourceLength <= 0)
                    {
                        throw new InvalidOperationException(
                            $"External resource '{resourceName}' in '{bundlePath}' "
                                + "has an invalid byte range."
                        );
                    }

                    var artifactName =
                        $".bklinked-{GetStableSuffix(bundleFileName, archivePath)}.resource";
                    RegisterArtifactResource(
                        context,
                        artifactName,
                        bundle.file.DataReader,
                        resourceOffset,
                        resourceLength
                    );

                    var assetGuid = AssetDatabase.AssetPathToGUID(context.assetPath);
                    if (string.IsNullOrWhiteSpace(assetGuid))
                    {
                        throw new InvalidOperationException(
                            $"Linked asset '{context.assetPath}' has no AssetDatabase GUID."
                        );
                    }

                    return $"VirtualArtifacts/Primary/{assetGuid}{artifactName}";
                }
                finally
                {
                    manager.UnloadAll(true);
                }
            }

            throw new FileNotFoundException(
                $"Could not find external resource '{resourceName}' for linked asset "
                    + $"'{descriptor.Address}' in any declared Addressables bundle."
            );
        }

        private static IEnumerable<string> GetBundleFileNames(
            LinkedAddressableDescriptor descriptor,
            string preferredBundleFileName
        )
        {
            var declared = (
                descriptor.Dependencies
                ?? Array.Empty<LinkedAddressableDependency>()
            )
                .Select(
                    dependency =>
                        Path.GetFileName(dependency.InternalId?.Replace('\\', '/'))
                )
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(preferredBundleFileName))
                return declared;

            return declared
                .Where(
                    fileName =>
                        string.Equals(
                            fileName,
                            preferredBundleFileName,
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Concat(
                    declared.Where(
                        fileName =>
                            !string.Equals(
                                fileName,
                                preferredBundleFileName,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                );
        }

        private static string GetSourceBundleFileName(
            LinkedAddressableSourceMap sourceMap,
            UnityEngine.Object asset
        )
        {
            return sourceMap?.Objects
                .FirstOrDefault(sourceObject => sourceObject.Target == asset)
                ?.SourceBundleFileName;
        }

        private static string GetStableSuffix(
            string bundleFileName,
            string archivePath
        )
        {
            using (var algorithm = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(
                    bundleFileName + "\n" + archivePath
                );
                return string.Concat(
                    algorithm
                        .ComputeHash(bytes)
                        .Take(8)
                        .Select(value => value.ToString("x2"))
                );
            }
        }

        private static void RegisterArtifactResource(
            AssetImportContext context,
            string artifactName,
            AssetsTools.NET.AssetsFileReader reader,
            long sourceOffset,
            long sourceLength
        )
        {
            var temporaryPath = Path.GetTempFileName();
            try
            {
                reader.Position = sourceOffset;
                using (
                    var destination = new FileStream(
                        temporaryPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read
                    )
                )
                {
                    var buffer = new byte[81920];
                    var remaining = sourceLength;
                    while (remaining > 0)
                    {
                        var requested = (int)Math.Min(buffer.Length, remaining);
                        var read = reader.Read(buffer, 0, requested);
                        if (read <= 0)
                        {
                            throw new EndOfStreamException(
                                $"Only copied {sourceLength - remaining} of "
                                    + $"{sourceLength} bytes from an external "
                                    + "Addressables resource."
                            );
                        }

                        destination.Write(buffer, 0, read);
                        remaining -= read;
                    }
                }

                context.SetOutputArtifactFile(artifactName, temporaryPath);
            }
            finally
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
