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

        // The bundles this source opened itself, as opposed to ones it found already open.
        private static readonly HashSet<AssetBundle> OwnedBundles = new HashSet<AssetBundle>();

        public static void BeginMaterialization()
        {
            // Bundles someone else already has open, such as Addressables loads made by an editor preview, are
            // reused rather than unloaded. Unloading them destroyed the assets their owners still held.
            LoadedBundles.Clear();
            OwnedBundles.Clear();
        }

        public static void EndMaterialization()
        {
            foreach (
                var bundle in OwnedBundles
                    .Where(bundle => bundle != null)
                    .ToArray()
            )
            {
                bundle.Unload(true);
            }

            LoadedBundles.Clear();
            OwnedBundles.Clear();
        }

        public static void PrepareForPlayMode()
        {
            EndMaterialization();
            var loadedBundles = AssetBundle.GetAllLoadedAssetBundles().ToArray();
            if (loadedBundles.Length == 0)
                return;

            // Play mode stopped while a bundle still had an asset load in flight leaves that bundle
            // half unloaded: Unity marks it unloaded but keeps its files open until the editor
            // restarts. UnloadAllAssetBundles crashed the editor on such a bundle, while unloading
            // it on its own only throws, so each bundle is unloaded separately.
            var stuckBundles = new List<string>();
            foreach (var bundle in loadedBundles)
            {
                try
                {
                    bundle.Unload(true);
                }
                catch (InvalidOperationException)
                {
                    stuckBundles.Add(bundle.name);
                }
            }

            Debug.Log(
                $"[ReduxSDK.LinkedAddressables] Unloaded "
                    + $"{loadedBundles.Length - stuckBundles.Count} edit-time AssetBundle(s) before play mode."
            );
            if (stuckBundles.Count > 0)
            {
                Debug.LogWarning(
                    $"[ReduxSDK.LinkedAddressables] {stuckBundles.Count} AssetBundle(s) are stuck "
                        + "from a play session stopped while it was loading, and game content in them "
                        + "will fail to load until the editor restarts: "
                        + string.Join(", ", stuckBundles)
                );
            }
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

            // A bundle already open elsewhere, such as an Addressables load made by an editor
            // preview, cannot be opened a second time. Addressables opens a root's dependencies
            // before the root, so a root found open needs nothing else loaded.
            var openSource = FindInOpenBundles(descriptor, assetType);
            if (openSource != null)
                return openSource;

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

                // A dependency already open elsewhere fails to open here, which leaves it in
                // place for the root to use.
                var bundle = AssetBundle.LoadFromFile(bundlePath);
                if (bundle != null)
                {
                    OwnedBundles.Add(bundle);
                    LoadedBundles[bundlePath] = bundle;
                }
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

        private static UnityEngine.Object FindInOpenBundles(
            LinkedAddressableDescriptor descriptor,
            Type assetType
        )
        {
            var rootFileName = GetBundleFileNames(descriptor).FirstOrDefault();
            var rootBundleName = Path.GetFileNameWithoutExtension(rootFileName);
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                if (
                    bundle == null
                    || bundle.isStreamedSceneAssetBundle
                    || (!string.Equals(bundle.name, rootBundleName, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(bundle.name, rootFileName, StringComparison.OrdinalIgnoreCase))
                )
                    continue;

                // Numeric internal IDs are local to a bundle and can identify unrelated assets in another bundle.
                var asset = LoadAsset(bundle, descriptor, assetType);
                if (asset != null)
                    return asset;
            }

            return null;
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
