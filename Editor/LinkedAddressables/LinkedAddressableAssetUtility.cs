using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    public static class LinkedAddressableAssetUtility
    {
        public const string DefaultOutputDirectory =
            "Assets/KSP2UnityTools/LinkedAddressables";

        public static string CreateLink(string address)
        {
            return CreateLink(address, DefaultOutputDirectory);
        }

        public static string CreateLink(string address, Type assetType)
        {
            return CreateLink(address, DefaultOutputDirectory, assetType);
        }

        public static string CreateLink(string address, string outputDirectory)
        {
            return CreateLink(address, outputDirectory, null);
        }

        public static string CreateLink(
            string address,
            string outputDirectory,
            Type assetType
        )
        {
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException("An Addressables address is required.", nameof(address));
            if (
                assetType != null
                && !typeof(UnityEngine.Object).IsAssignableFrom(assetType)
            )
            {
                throw new ArgumentException(
                    $"Linked Addressables assets must derive from "
                        + $"'{typeof(UnityEngine.Object).FullName}'.",
                    nameof(assetType)
                );
            }

            var matches = FindLocations(address, assetType);
            if (matches.Count == 0)
            {
                var typeSuffix = assetType == null
                    ? string.Empty
                    : $" as '{assetType.FullName}'";
                throw new InvalidOperationException(
                    $"No Addressables resource location was found for '{address}'"
                        + $"{typeSuffix}."
                );
            }

            if (matches.Count > 1)
            {
                var identities = string.Join(
                    ", ",
                    matches.Select(
                        match =>
                            $"{match.CatalogId}:{match.Location.ResourceType?.FullName}:"
                            + $"{match.Location.InternalId}"
                    )
                );
                throw new InvalidOperationException(
                    $"Address '{address}' is ambiguous across the loaded catalogs: {identities}"
                );
            }

            var match = matches[0];
            var descriptor = CreateDescriptor(match.CatalogId, address, match.Location);
            return WriteDescriptor(descriptor, outputDirectory);
        }

        public static string CreateLink(
            LinkedAddressableCatalogEntry entry,
            string outputDirectory
        )
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var descriptor = CreateDescriptor(
                entry.CatalogId,
                entry.Address,
                entry.Location
            );
            return WriteDescriptor(descriptor, outputDirectory);
        }

        public static LinkedAddressableDescriptor CreateDescriptor(
            string catalogId,
            string address,
            IResourceLocation location
        )
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var resourceType = location.ResourceType;
            if (resourceType == null)
                throw new InvalidOperationException(
                    $"Addressables location '{address}' does not declare a runtime type."
                );

            var thunderKitSettings =
                ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var sourceRoot = thunderKitSettings.AddressableAssetsPath;
            var sourceId = thunderKitSettings.PackageName;
            var catalogPath = FindCatalogPath(sourceRoot);
            var settingsPath = Path.Combine(sourceRoot, "settings.json");
            var hashPath = Path.ChangeExtension(catalogPath, ".hash");
            var catalogHash = File.Exists(hashPath) ? File.ReadAllText(hashPath).Trim() : string.Empty;
            var stableId = ComputeStableId(
                sourceId,
                catalogId,
                address,
                location.InternalId,
                resourceType.AssemblyQualifiedName
            );

            return new LinkedAddressableDescriptor
            {
                SchemaVersion = LinkedAddressableDescriptor.CurrentSchemaVersion,
                StableId = stableId,
                DisplayName = Path.GetFileNameWithoutExtension(address),
                SourceId = sourceId,
                CatalogId = catalogId,
                CatalogHash = catalogHash,
                CatalogFileName = Path.GetFileName(catalogPath),
                SettingsFileName = File.Exists(settingsPath)
                    ? Path.GetFileName(settingsPath)
                    : "settings.json",
                Address = address,
                PrimaryKey = location.PrimaryKey,
                InternalId = location.InternalId,
                ProviderId = location.ProviderId,
                AssetType = resourceType.AssemblyQualifiedName,
                Dependencies = location.Dependencies == null
                    ? Array.Empty<LinkedAddressableDependency>()
                    : location
                        .Dependencies.Select(
                            dependency =>
                                new LinkedAddressableDependency
                                {
                                    PrimaryKey = dependency.PrimaryKey,
                                    InternalId = dependency.InternalId,
                                    ProviderId = dependency.ProviderId,
                                    ResourceType = dependency.ResourceType?.AssemblyQualifiedName
                                }
                        )
                        .ToArray()
            };
        }

        public static string WriteDescriptor(
            LinkedAddressableDescriptor descriptor,
            string outputDirectory
        )
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("An output directory is required.", nameof(outputDirectory));
            if (!outputDirectory.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException(
                    "Linked Addressables assets must be created under the project's Assets directory.",
                    nameof(outputDirectory)
                );

            Directory.CreateDirectory(outputDirectory);
            var displayName = MakeSafeFileName(descriptor.DisplayName);
            var assetPath =
                $"{outputDirectory}/{displayName}-{descriptor.StableId.Substring(0, 12)}."
                + LinkedAddressableAssetImporter.Extension;
            var json = JsonUtility.ToJson(descriptor, true);
            if (
                File.Exists(assetPath)
                && string.Equals(
                    File.ReadAllText(assetPath),
                    json,
                    StringComparison.Ordinal
                )
                && AssetDatabase.LoadMainAssetAtPath(assetPath) != null
            )
            {
                LinkedAddressableRuntimeManifestBuilder.Rebuild(false);
                return assetPath;
            }

            File.WriteAllText(assetPath, json, Encoding.UTF8);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            LinkedAddressableRuntimeManifestBuilder.Rebuild(false);
            return assetPath;
        }

        public static string ComputeStableId(
            string sourceId,
            string catalogId,
            string address,
            string internalId,
            string assetType
        )
        {
            var identity = string.Join(
                "\n",
                sourceId ?? string.Empty,
                catalogId ?? string.Empty,
                address ?? string.Empty,
                internalId ?? string.Empty,
                assetType ?? string.Empty
            );
            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity));
                return string.Concat(digest.Select(value => value.ToString("x2")));
            }
        }

        private static List<LocationMatch> FindLocations(
            string address,
            Type assetType
        )
        {
            return LinkedAddressableEditorCatalog
                .Find(address, assetType)
                .Select(
                    entry =>
                        new LocationMatch(
                            entry.CatalogId,
                            entry.Location
                        )
                )
                .GroupBy(
                    match =>
                        $"{match.CatalogId}\n{match.Location.InternalId}\n"
                        + match.Location.ResourceType?.AssemblyQualifiedName,
                    StringComparer.Ordinal
                )
                .Select(group => group.First())
                .ToList();
        }

        private static string FindCatalogPath(string sourceRoot)
        {
            var jsonPath = Path.Combine(sourceRoot, "catalog.json");
            if (File.Exists(jsonPath))
                return jsonPath;

            var binaryPath = Path.Combine(sourceRoot, "catalog.bin");
            if (File.Exists(binaryPath))
                return binaryPath;

            throw new FileNotFoundException(
                $"No catalog.json or catalog.bin exists under '{sourceRoot}'."
            );
        }

        private static string MakeSafeFileName(string value)
        {
            var source = string.IsNullOrWhiteSpace(value) ? "linked-addressable" : value;
            var invalidCharacters = Path.GetInvalidFileNameChars();
            var safeCharacters = source.Select(
                character => invalidCharacters.Contains(character) ? '-' : character
            );
            return string.Concat(safeCharacters);
        }

        private sealed class LocationMatch
        {
            public LocationMatch(string catalogId, IResourceLocation location)
            {
                CatalogId = catalogId;
                Location = location;
            }

            public string CatalogId { get; }
            public IResourceLocation Location { get; }
        }
    }
}
