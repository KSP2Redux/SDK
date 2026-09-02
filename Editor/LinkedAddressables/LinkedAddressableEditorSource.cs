using Ksp2UnityTools.LinkedAddressables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ThunderKit.Core.Data;
using UnityEngine;

namespace Ksp2UnityTools.Editor.LinkedAddressables
{
    internal static class LinkedAddressableEditorSource
    {
        private static readonly Dictionary<string, AssetBundle> LoadedBundles =
            new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

        public static void BeginMaterialization()
        {
            AssetBundle.UnloadAllAssetBundles(true);
            LoadedBundles.Clear();
        }

        public static void EndMaterialization()
        {
            foreach (
                var bundle in LoadedBundles
                    .Values.Where(bundle => bundle != null)
                    .Distinct()
                    .ToArray()
            )
            {
                bundle.Unload(true);
            }

            LoadedBundles.Clear();
        }

        public static void PrepareForPlayMode()
        {
            EndMaterialization();
            var loadedBundleCount = AssetBundle
                .GetAllLoadedAssetBundles()
                .Count();
            if (loadedBundleCount == 0)
                return;

            AssetBundle.UnloadAllAssetBundles(true);
            Debug.Log(
                $"[KSP2UnityTools.LinkedAddressables] Unloaded "
                    + $"{loadedBundleCount} edit-time AssetBundle(s) before play mode."
            );
        }

        public static UnityEngine.Object LoadMainAsset(
            LinkedAddressableDescriptor descriptor
        )
        {
            if (descriptor == null)
                throw new ArgumentNullException(nameof(descriptor));

            var assetType = Type.GetType(descriptor.AssetType, false);
            if (assetType == null || !typeof(UnityEngine.Object).IsAssignableFrom(assetType))
            {
                throw new InvalidOperationException(
                    $"The linked Addressables type '{descriptor.AssetType}' is not a "
                        + "loadable UnityEngine.Object type."
                );
            }

            var settings = ThunderKitSettings.GetOrCreateSettings<ThunderKitSettings>();
            var bundleDirectory = Path.Combine(
                settings.AddressableAssetsPath,
                "StandaloneWindows64"
            );
            var bundleFileNames = GetBundleFileNames(descriptor).ToArray();
            if (bundleFileNames.Length == 0)
            {
                throw new InvalidOperationException(
                    $"The linked Addressables descriptor for '{descriptor.Address}' "
                        + "does not declare a source bundle."
                );
            }

            // Addressables reports the bundle containing the root first, followed by
            // its dependencies. Native bundle loading must do the opposite so scripts
            // and referenced assets are available when Unity deserializes the root.
            foreach (var bundleFileName in bundleFileNames.Reverse())
            {
                var bundlePath = Path.Combine(bundleDirectory, bundleFileName);
                if (!File.Exists(bundlePath))
                {
                    throw new FileNotFoundException(
                        $"The linked Addressables bundle '{bundleFileName}' is missing.",
                        bundlePath
                    );
                }

                if (
                    LoadedBundles.TryGetValue(bundlePath, out var loadedBundle)
                    && loadedBundle != null
                )
                {
                    continue;
                }

                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                    LoadedBundles[bundlePath] = bundle;
            }

            var rootBundlePath = Path.Combine(bundleDirectory, bundleFileNames[0]);
            if (
                !LoadedBundles.TryGetValue(rootBundlePath, out var rootBundle)
                || rootBundle == null
            )
            {
                throw new InvalidOperationException(
                    $"The root bundle '{bundleFileNames[0]}' for "
                        + $"'{descriptor.Address}' could not be loaded."
                );
            }

            var source = LoadAsset(rootBundle, descriptor, assetType);
            if (source != null)
                return source;

            throw new InvalidOperationException(
                $"Could not load '{descriptor.Address}' as '{assetType.FullName}' from "
                    + $"the external Addressables bundles under '{bundleDirectory}'."
            );
        }

        private static IEnumerable<string> GetBundleFileNames(
            LinkedAddressableDescriptor descriptor
        )
        {
            return (descriptor.Dependencies ?? Array.Empty<LinkedAddressableDependency>())
                .Select(
                    dependency =>
                        Path.GetFileName(dependency.InternalId?.Replace('\\', '/'))
                )
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static UnityEngine.Object LoadAsset(
            AssetBundle bundle,
            LinkedAddressableDescriptor descriptor,
            Type assetType
        )
        {
            foreach (
                var assetName in new[]
                {
                    descriptor.InternalId,
                    descriptor.PrimaryKey,
                    descriptor.Address
                }
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            )
            {
                var asset = bundle.LoadAsset(assetName, assetType);
                if (asset != null)
                    return asset;
            }

            return null;
        }
    }
}
