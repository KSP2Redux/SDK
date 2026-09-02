using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Core.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    /// <summary>
    /// Owns the editor's connection to the external Addressables catalog.
    /// Unlike the runtime manifest, this can bootstrap when no linked assets exist.
    /// </summary>
    public static class LinkedAddressableEditorCatalog
    {
        private static readonly List<LinkedAddressableCatalogEntry> entries =
            new List<LinkedAddressableCatalogEntry>();

        private static IResourceLocator catalogLocator;
        private static Func<IResourceLocation, string> previousInternalIdTransform;
        private static string sourceRoot;
        private static bool isLoading;

        public static event Action Changed;

        public static IReadOnlyList<LinkedAddressableCatalogEntry> Entries => entries;

        public static bool IsLoaded =>
            catalogLocator != null
            && Addressables.ResourceLocators.Contains(catalogLocator);

        public static string SourceRoot => sourceRoot;

        public static string CatalogId => catalogLocator?.LocatorId;

        public static string LastError { get; private set; }

        public static void EnsureLoaded()
        {
            Load(false);
        }

        public static void Refresh()
        {
            Load(true);
        }

        internal static IEnumerable<LinkedAddressableCatalogEntry> Find(
            string address,
            Type assetType
        )
        {
            EnsureLoaded();
            return entries.Where(
                entry =>
                    string.Equals(
                        entry.Address,
                        address,
                        StringComparison.Ordinal
                    )
                    && (
                        assetType == null
                        || assetType.IsAssignableFrom(entry.AssetType)
                    )
            );
        }

        private static void Load(bool force)
        {
            if (isLoading)
                throw new InvalidOperationException(
                    "The external Addressables catalog is already loading."
                );
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Refresh or create linked Addressables assets in edit mode. "
                        + "The runtime owns the catalog while entering or running "
                        + "play mode."
                );
            }

            var settings =
                ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var configuredSourceRoot = Path.GetFullPath(
                settings.AddressableAssetsPath
            );
            if (
                !force
                && IsLoaded
                && string.Equals(
                    sourceRoot,
                    configuredSourceRoot,
                    StringComparison.OrdinalIgnoreCase
                )
                && entries.Count > 0
            )
            {
                return;
            }

            isLoading = true;
            try
            {
                LastError = null;
                if (
                    catalogLocator != null
                    && (
                        force
                        || !string.Equals(
                            sourceRoot,
                            configuredSourceRoot,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                )
                {
                    Addressables.RemoveResourceLocator(catalogLocator);
                    catalogLocator = null;
                }

                sourceRoot = configuredSourceRoot;
                var settingsPath = Path.Combine(sourceRoot, "settings.json");
                var catalogPath = FindCatalogPath(sourceRoot);
                if (!File.Exists(settingsPath))
                {
                    throw new FileNotFoundException(
                        $"The external Addressables settings file is missing: "
                            + $"'{settingsPath}'.",
                        settingsPath
                    );
                }

                InstallInternalIdTransform();
                InitializeAddressables(settingsPath);

                catalogLocator = Addressables.ResourceLocators.FirstOrDefault(
                    locator => PathsEqual(locator.LocatorId, catalogPath)
                );
                if (catalogLocator == null)
                    catalogLocator = LoadCatalog(catalogPath);

                entries.Clear();
                entries.AddRange(EnumerateEntries(catalogLocator));
                if (entries.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The external catalog '{catalogPath}' contains no "
                            + "linkable UnityEngine.Object locations."
                    );
                }

                Changed?.Invoke();
            }
            catch (Exception exception)
            {
                entries.Clear();
                LastError = exception.Message;
                Changed?.Invoke();
                throw;
            }
            finally
            {
                isLoading = false;
            }
        }

        private static void InitializeAddressables(string settingsPath)
        {
            if (Addressables.ResourceLocators.Any())
                return;

            var hadRuntimePath = PlayerPrefs.HasKey(
                Addressables.kAddressablesRuntimeDataPath
            );
            var previousRuntimePath = hadRuntimePath
                ? PlayerPrefs.GetString(Addressables.kAddressablesRuntimeDataPath)
                : null;
            AsyncOperationHandle<IResourceLocator> initialization = default;
            try
            {
                PlayerPrefs.SetString(
                    Addressables.kAddressablesRuntimeDataPath,
                    settingsPath.Replace('\\', '/')
                );
                initialization = Addressables.InitializeAsync(false);
                initialization.WaitForCompletion();
                if (
                    initialization.Status
                    != AsyncOperationStatus.Succeeded
                )
                {
                    throw new InvalidOperationException(
                        "Addressables initialization failed for the external "
                            + $"source '{sourceRoot}': "
                            + (
                                initialization.OperationException?.ToString()
                                ?? "unknown error"
                            )
                    );
                }
            }
            catch (ArgumentOutOfRangeException)
                when (
                    !Addressables.ResourceLocators.Any()
                    && Addressables.ResourceManager.ResourceProviders.Any()
                )
            {
                // Addressables 2.9.1 indexes its first locator when
                // InitializeAsync is called after initialization has completed
                // and every locator was later removed. The ResourceManager and
                // providers remain initialized in that state, so LoadCatalog
                // can repopulate the locator list without reinitializing.
            }
            finally
            {
                if (hadRuntimePath)
                {
                    PlayerPrefs.SetString(
                        Addressables.kAddressablesRuntimeDataPath,
                        previousRuntimePath
                    );
                }
                else
                {
                    PlayerPrefs.DeleteKey(
                        Addressables.kAddressablesRuntimeDataPath
                    );
                }

                if (initialization.IsValid())
                    Addressables.Release(initialization);
            }
        }

        private static IResourceLocator LoadCatalog(string catalogPath)
        {
            var operation = Addressables.LoadContentCatalogAsync(
                catalogPath.Replace('\\', '/'),
                false
            );
            try
            {
                operation.WaitForCompletion();
                if (
                    operation.Status != AsyncOperationStatus.Succeeded
                    || operation.Result == null
                )
                {
                    throw new InvalidOperationException(
                        $"Could not load external Addressables catalog "
                            + $"'{catalogPath}': "
                            + (
                                operation.OperationException?.ToString()
                                ?? "the operation returned no resource locator"
                            )
                    );
                }

                return operation.Result;
            }
            finally
            {
                if (operation.IsValid())
                    Addressables.Release(operation);
            }
        }

        private static IEnumerable<LinkedAddressableCatalogEntry>
            EnumerateEntries(IResourceLocator locator)
        {
            IEnumerable<IResourceLocation> locations;
            var labelsByLocation =
                new Dictionary<string, HashSet<string>>(
                    StringComparer.Ordinal
                );
            if (locator is ResourceLocationMap locationMap)
            {
                locations = locationMap.Locations.SelectMany(
                    pair => pair.Value
                );
                foreach (var pair in locationMap.Locations)
                {
                    if (
                        !(pair.Key is string key)
                        || string.IsNullOrWhiteSpace(key)
                        || IsAssetGuid(key)
                    )
                    {
                        continue;
                    }

                    foreach (var location in pair.Value.Where(IsLinkable))
                    {
                        if (
                            string.Equals(
                                key,
                                location.PrimaryKey,
                                StringComparison.Ordinal
                            )
                        )
                        {
                            continue;
                        }

                        var identity = GetLocationIdentity(location);
                        if (
                            !labelsByLocation.TryGetValue(
                                identity,
                                out var labels
                            )
                        )
                        {
                            labels = new HashSet<string>(
                                StringComparer.Ordinal
                            );
                            labelsByLocation.Add(identity, labels);
                        }

                        labels.Add(key);
                    }
                }
            }
            else
            {
                locations = locator.Keys.SelectMany(
                    key =>
                        locator.Locate(key, null, out var located)
                            ? located
                            : Array.Empty<IResourceLocation>()
                );
            }

            return locations
                .Where(IsLinkable)
                .GroupBy(
                    GetLocationIdentity,
                    StringComparer.Ordinal
                )
                .Select(
                    group =>
                        new LinkedAddressableCatalogEntry(
                            locator.LocatorId,
                            group.First(),
                            labelsByLocation.TryGetValue(
                                group.Key,
                                out var labels
                            )
                                ? labels.OrderBy(
                                        label => label,
                                        StringComparer.OrdinalIgnoreCase
                                    )
                                    .ToArray()
                                : Array.Empty<string>()
                        )
                )
                .OrderBy(
                    entry => entry.Address,
                    StringComparer.OrdinalIgnoreCase
                )
                .ThenBy(
                    entry => entry.AssetType.FullName,
                    StringComparer.Ordinal
                );
        }

        private static bool IsLinkable(IResourceLocation location)
        {
            return location != null
                && !string.IsNullOrWhiteSpace(location.PrimaryKey)
                && location.ResourceType != null
                && typeof(UnityEngine.Object).IsAssignableFrom(
                    location.ResourceType
                );
        }

        private static string GetLocationIdentity(
            IResourceLocation location
        )
        {
            return $"{location.PrimaryKey}\n{location.InternalId}\n"
                + location.ResourceType.AssemblyQualifiedName;
        }

        private static bool IsAssetGuid(string key)
        {
            return key.Length == 32 && key.All(Uri.IsHexDigit);
        }

        private static void InstallInternalIdTransform()
        {
            if (Addressables.InternalIdTransformFunc == RedirectInternalId)
                return;

            previousInternalIdTransform =
                Addressables.InternalIdTransformFunc;
            Addressables.InternalIdTransformFunc = RedirectInternalId;
        }

        private static string RedirectInternalId(IResourceLocation location)
        {
            var evaluated = AddressablesRuntimeProperties
                .EvaluateString(location.InternalId)
                .Replace('\\', '/');
            if (evaluated.Contains("://", StringComparison.Ordinal))
                return evaluated;

            var fileName = Path.GetFileName(evaluated);
            if (
                fileName.Equals(
                    "catalog.json",
                    StringComparison.OrdinalIgnoreCase
                )
                || fileName.Equals(
                    "catalog.bin",
                    StringComparison.OrdinalIgnoreCase
                )
                || fileName.Equals(
                    "catalog.hash",
                    StringComparison.OrdinalIgnoreCase
                )
                || fileName.Equals(
                    "settings.json",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return Path.Combine(sourceRoot, fileName).Replace('\\', '/');
            }

            if (
                evaluated.EndsWith(
                    ".bundle",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return Path.Combine(
                        sourceRoot,
                        "StandaloneWindows64",
                        fileName
                    )
                    .Replace('\\', '/');
            }

            return previousInternalIdTransform?.Invoke(location) ?? evaluated;
        }

        private static string FindCatalogPath(string root)
        {
            var jsonPath = Path.Combine(root, "catalog.json");
            if (File.Exists(jsonPath))
                return jsonPath;

            var binaryPath = Path.Combine(root, "catalog.bin");
            if (File.Exists(binaryPath))
                return binaryPath;

            throw new FileNotFoundException(
                $"No catalog.json or catalog.bin exists under '{root}'."
            );
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left))
                return false;

            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase
                );
            }
            catch
            {
                return string.Equals(
                    left.Replace('\\', '/'),
                    right.Replace('\\', '/'),
                    StringComparison.OrdinalIgnoreCase
                );
            }
        }
    }

    public sealed class LinkedAddressableCatalogEntry
    {
        internal LinkedAddressableCatalogEntry(
            string catalogId,
            IResourceLocation location,
            IReadOnlyList<string> labels
        )
        {
            CatalogId = catalogId;
            Location = location;
            Labels = labels ?? Array.Empty<string>();
        }

        public string CatalogId { get; }

        public IResourceLocation Location { get; }

        public string Address => Location.PrimaryKey;

        public Type AssetType => Location.ResourceType;

        public string ProviderId => Location.ProviderId;

        public string InternalId => Location.InternalId;

        /// <summary>
        /// Non-primary, non-GUID string keys associated with this location.
        /// Addressables runtime catalogs do not preserve explicit key semantics,
        /// so these are the catalog keys that represent labels in standard builds.
        /// </summary>
        public IReadOnlyList<string> Labels { get; }

        public string Directory
        {
            get
            {
                var forwardSlash = Address.LastIndexOf('/');
                var backSlash = Address.LastIndexOf('\\');
                var separator = Math.Max(forwardSlash, backSlash);
                return separator > 0
                    ? Address.Substring(0, separator)
                    : "Assorted";
            }
        }
    }
}
