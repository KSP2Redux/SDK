using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.Initialization;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace Ksp2UnityTools.LinkedAddressables
{
    public static class LinkedAddressableRuntime
    {
        private const string LogPrefix = "[ReduxSDK.LinkedAddressables]";
        public const string SourceRootPropertyName =
            "ReduxSDK.LinkedAddressables.SourceRoot";

        private static readonly HashSet<string> LoadedStableIds = new HashSet<string>(
            StringComparer.Ordinal
        );
        private static readonly Dictionary<string, string> Failures = new Dictionary<
            string,
            string
        >(StringComparer.Ordinal);
        private static readonly List<AsyncOperationHandle> RootHandles =
            new List<AsyncOperationHandle>();
        private static readonly Dictionary<string, UnityEngine.Object> LoadedRoots =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private static readonly HashSet<string> ExternalBundleFileNames =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static LinkedAddressableRuntimeManifest manifest;
        private static AsyncOperationHandle<IResourceLocator> initializationHandle;
        private static IUpdateReceiver editorCatalogWaiter;
        private static IResourceLocator initialProjectCatalogLocator;
        private static IResourceLocator externalCatalogLocator;
        private static Func<IResourceLocation, string> previousInternalIdTransform;
        private static AssetBundle targetAssetBundle;
        private static AssetBundle targetSceneBundle;
        private static readonly List<AssetBundle> SupportBundles =
            new List<AssetBundle>();
        private static int pendingRootLoads;
        private static bool started;

        public static event Action<string, bool, string> LinkStatusChanged;

        public static bool IsReady { get; private set; }

        public static bool TranslatedSceneLoaded { get; private set; }

        public static string RuntimeFailure { get; private set; }

        public static string ResolvedSourceRoot { get; private set; }

        public static AssetBundle TargetAssetBundle => targetAssetBundle;

        public static bool IsLoaded(string stableId)
        {
            return !string.IsNullOrWhiteSpace(stableId)
                && LoadedStableIds.Contains(stableId);
        }

        public static bool TryGetFailure(string stableId, out string failure)
        {
            return Failures.TryGetValue(stableId, out failure);
        }

        public static bool TryGetLoadedRoot(string stableId, out UnityEngine.Object asset)
        {
            return LoadedRoots.TryGetValue(stableId ?? string.Empty, out asset)
                && asset != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            if (Addressables.InternalIdTransformFunc == RedirectInternalId)
                Addressables.InternalIdTransformFunc = previousInternalIdTransform;

            if (
                externalCatalogLocator != null
                && Addressables.ResourceLocators.Contains(externalCatalogLocator)
            )
            {
                Addressables.RemoveResourceLocator(externalCatalogLocator);
            }

            if (editorCatalogWaiter != null)
            {
                Addressables.ResourceManager.RemoveUpdateReciever(
                    editorCatalogWaiter
                );
                editorCatalogWaiter = null;
            }

            foreach (var handle in RootHandles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }

            RootHandles.Clear();
            LoadedRoots.Clear();
            LoadedStableIds.Clear();
            ExternalBundleFileNames.Clear();
            Failures.Clear();
            manifest = null;
            initializationHandle = default;
            initialProjectCatalogLocator = null;
            externalCatalogLocator = null;
            previousInternalIdTransform = null;
            targetAssetBundle = null;
            targetSceneBundle = null;
            SupportBundles.Clear();
            pendingRootLoads = 0;
            started = false;
            IsReady = false;
            TranslatedSceneLoaded = false;
            RuntimeFailure = null;
            ResolvedSourceRoot = null;
            LinkStatusChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (started)
                return;

            started = true;
            manifest = Resources.Load<LinkedAddressableRuntimeManifest>(
                LinkedAddressableRuntimeManifest.ResourcePath
            );
            if (manifest == null || manifest.Entries == null || manifest.Entries.Length == 0)
            {
                RuntimeFailure = "No linked assets are registered.";
                Debug.Log($"{LogPrefix} {RuntimeFailure}");
                return;
            }

            ResolvedSourceRoot = ResolveSourceRoot(manifest);
            if (!TryValidateSource(manifest, out var sourceError))
            {
                FailAll(sourceError);
                return;
            }
            if (
                !Application.isEditor
                && manifest.UseTranslatedSceneBootstrap
            )
            {
                try
                {
                    LoadTranslatedSupportBundles();
                }
                catch (Exception exception)
                {
                    FailAll(
                        "Could not mount translated support bundles before "
                            + "loading linked Addressables roots: "
                            + exception
                    );
                    return;
                }
            }
            ExternalBundleFileNames.UnionWith(
                manifest
                    .Entries.Where(entry =>
                        entry?.Descriptor?.Dependencies != null
                    )
                    .SelectMany(entry => entry.Descriptor.Dependencies)
                    .Where(dependency => dependency != null)
                    .Select(dependency =>
                        Path.GetFileName(
                            dependency.InternalId?.Replace('\\', '/')
                        )
                    )
                    .Where(fileName =>
                        !string.IsNullOrWhiteSpace(fileName)
                        && fileName.EndsWith(
                            ".bundle",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
            );

            AddressablesRuntimeProperties.SetPropertyValue(
                SourceRootPropertyName,
                ResolvedSourceRoot.Replace('\\', '/')
            );
            previousInternalIdTransform = Addressables.InternalIdTransformFunc;
            Addressables.InternalIdTransformFunc = RedirectInternalId;
            Debug.Log(
                $"{LogPrefix} Using "
                    + $"{(manifest.CopySourceToPlayer && !Application.isEditor ? "player-local" : "external")} "
                    + $"Addressables source '{ResolvedSourceRoot}'."
            );

            if (Application.isEditor)
            {
                // Keep the project's normal Play Mode initialization in the
                // editor. Editor integrations such as the Redux SDK register
                // the imported game catalog alongside it. Initializing from
                // the installed game's settings here would register that same
                // stock catalog a second time under a different locator ID.
                // The editor catalog waiter below falls back to the Redux SDK's
                // staged external catalog when no integration provides one.
                initializationHandle = Addressables.InitializeAsync(false);
            }
            else
            {
                var settingsRoot = GetInitialSettingsRoot(manifest);
                var settingsPath = Path.Combine(
                        settingsRoot,
                        manifest.SettingsFileName ?? "settings.json"
                    )
                    .Replace('\\', '/');

                PlayerPrefs.SetString(
                    Addressables.kAddressablesRuntimeDataPath,
                    settingsPath
                );
                try
                {
                    initializationHandle = Addressables.InitializeAsync(false);
                }
                finally
                {
                    PlayerPrefs.DeleteKey(
                        Addressables.kAddressablesRuntimeDataPath
                    );
                }
            }

            initializationHandle.Completed += OnAddressablesInitialized;
        }

        private static bool TryValidateSource(
            LinkedAddressableRuntimeManifest runtimeManifest,
            out string error
        )
        {
            if (string.IsNullOrWhiteSpace(ResolvedSourceRoot))
            {
                error = "The runtime manifest resolved no Addressables source root.";
                return false;
            }

            if (!Directory.Exists(ResolvedSourceRoot))
            {
                error =
                    $"The resolved Addressables source root is missing: "
                    + $"'{ResolvedSourceRoot}'. "
                    + (
                        runtimeManifest.CopySourceToPlayer && !Application.isEditor
                            ? "Rebuild with source copying enabled or repair the staged player."
                            : "Repair the ThunderKit game path and rebuild the linked-asset manifest."
                    );
                return false;
            }

            foreach (
                var fileName in new[]
                {
                    runtimeManifest.SettingsFileName ?? "settings.json",
                    runtimeManifest.CatalogFileName ?? "catalog.json"
                }
            )
            {
                var path = Path.Combine(ResolvedSourceRoot, fileName);
                if (!File.Exists(path))
                {
                    error = $"The external Addressables file is missing: '{path}'.";
                    return false;
                }
            }
            if (!Application.isEditor)
            {
                var stagedMetadataRoot =
                    GetStagedExternalMetadataRoot(runtimeManifest);
                foreach (
                    var fileName in new[]
                    {
                        runtimeManifest.SettingsFileName ?? "settings.json",
                        runtimeManifest.CatalogFileName ?? "catalog.json"
                    }
                )
                {
                    var path = Path.Combine(stagedMetadataRoot, fileName);
                    if (!File.Exists(path))
                    {
                        error =
                            "The staged external Addressables metadata is "
                            + $"missing: '{path}'.";
                        return false;
                    }
                }
            }

            var missingBundle = runtimeManifest
                .Entries.Where(entry => entry?.Descriptor?.Dependencies != null)
                .SelectMany(entry => entry.Descriptor.Dependencies)
                .Where(dependency => dependency != null)
                .Select(
                    dependency =>
                        Path.GetFileName(dependency.InternalId?.Replace('\\', '/'))
                )
                .Where(fileName => !string.IsNullOrWhiteSpace(fileName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(
                    fileName =>
                        !File.Exists(
                            Path.Combine(ResolvedSourceRoot, "StandaloneWindows64", fileName)
                        )
                );
            if (!string.IsNullOrWhiteSpace(missingBundle))
            {
                error =
                    $"The Addressables bundle '{missingBundle}' is missing under "
                    + $"'{Path.Combine(ResolvedSourceRoot, "StandaloneWindows64")}'.";
                return false;
            }

            if (
                !Application.isEditor
                && runtimeManifest.UseTranslatedSceneBootstrap
            )
            {
                var contentRoot = GetTranslatedContentRoot(runtimeManifest);
                foreach (
                    var fileName in new[]
                    {
                        runtimeManifest.SceneBundleFileName,
                        runtimeManifest.AssetBundleFileName
                    }
                        .Concat(
                            runtimeManifest.SupportBundleFileNames
                                ?? Array.Empty<string>()
                        )
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                )
                {
                    var path = Path.Combine(contentRoot, fileName);
                    if (!File.Exists(path))
                    {
                        error = $"The translated target bundle is missing: '{path}'.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static void OnAddressablesInitialized(
            AsyncOperationHandle<IResourceLocator> operation
        )
        {
            if (operation.Status != AsyncOperationStatus.Succeeded)
            {
                FailAll(
                    "Addressables initialization failed: "
                        + (operation.OperationException?.ToString() ?? "unknown error")
                );
                return;
            }

            if (
                !Application.isEditor
                &&
                !string.IsNullOrWhiteSpace(manifest.CatalogId)
                && !CatalogContainsLinkedRoots(operation.Result)
                && !Addressables.ResourceLocators.Any(
                    locator =>
                        string.Equals(
                            locator.LocatorId,
                            manifest.CatalogId,
                            StringComparison.Ordinal
                        )
                )
            )
            {
                initialProjectCatalogLocator = operation.Result;
                var catalogRoot =
                    GetStagedExternalMetadataRoot(manifest);
                var catalogPath = Path.Combine(
                        catalogRoot,
                        manifest.CatalogFileName ?? "catalog.json"
                    )
                    .Replace('\\', '/');
                var catalogLoad = Addressables.LoadContentCatalogAsync(
                    catalogPath,
                    false,
                    "ReduxSDK.External"
                );
                catalogLoad.Completed += OnCatalogLoaded;
                return;
            }

            RetainInitialProjectCatalog(operation.Result);
            ContinueAfterCatalogReady();
        }

        private static void OnCatalogLoaded(AsyncOperationHandle<IResourceLocator> operation)
        {
            if (operation.Status != AsyncOperationStatus.Succeeded)
            {
                FailAll(
                    "The linked Addressables catalog failed to load: "
                        + (operation.OperationException?.ToString() ?? "unknown error")
                );
                Addressables.Release(operation);
                return;
            }

            if (Application.isEditor)
                externalCatalogLocator = operation.Result;
            RetainInitialProjectCatalog(initialProjectCatalogLocator);
            ContinueAfterCatalogReady();
            Addressables.Release(operation);
        }

        private static bool CatalogContainsLinkedRoots(IResourceLocator locator)
        {
            if (locator == null)
                return false;

            foreach (
                var descriptor in manifest
                    .Entries.Where(entry => entry?.Descriptor != null)
                    .Select(entry => entry.Descriptor)
            )
            {
                var assetType = Type.GetType(descriptor.AssetType, false);
                if (
                    assetType == null
                    || !locator.Locate(
                        descriptor.Address,
                        assetType,
                        out var locations
                    )
                    || locations == null
                    || locations.Count == 0
                )
                {
                    return false;
                }
            }

            return true;
        }

        private static void ContinueAfterCatalogReady()
        {
            if (
                Application.isEditor
                && !RegisteredCatalogContainsLinkedRoots()
            )
            {
                // Editor integrations commonly register an imported game catalog
                // from EnteredPlayMode. Addressables initialization completes
                // while that catalog load is still chained behind it, so loading
                // linked keys from this completion callback races the locator
                // registration. A synchronous LoadContentCatalogAsync wait also
                // pumps ResourceManager updates before its locator is registered,
                // so a one-update grace period can re-enter here and mount a
                // duplicate stock catalog. Give the integration a bounded number
                // of updates to complete before loading the fallback ourselves.
                if (editorCatalogWaiter == null)
                {
                    editorCatalogWaiter = new EditorCatalogWaiter();
                    Addressables.ResourceManager.AddUpdateReceiver(
                        editorCatalogWaiter
                    );
                }
                return;
            }

            if (
                !Application.isEditor
                && !manifest.UseTranslatedSceneBootstrap
            )
            {
                IsReady = true;
                Debug.Log(
                    $"{LogPrefix} External Addressables are configured for "
                        + "on-demand loading. The player continues in its normal "
                        + "build scene."
                );
                return;
            }

            LoadLinkedRoots();
        }

        private static bool RegisteredCatalogContainsLinkedRoots()
        {
            return Addressables.ResourceLocators.Any(
                CatalogContainsLinkedRoots
            );
        }

        private static void ContinueAfterEditorCatalogRegistration()
        {
            if (editorCatalogWaiter != null)
            {
                Addressables.ResourceManager.RemoveUpdateReciever(
                    editorCatalogWaiter
                );
                editorCatalogWaiter = null;
            }

            if (RegisteredCatalogContainsLinkedRoots())
            {
                ContinueAfterCatalogReady();
                return;
            }

            var catalogRoot = GetStagedExternalMetadataRoot(manifest);
            var catalogPath = Path.Combine(
                    catalogRoot,
                    manifest.CatalogFileName ?? "catalog.json"
                )
                .Replace('\\', '/');
            var catalogLoad = Addressables.LoadContentCatalogAsync(
                catalogPath,
                false,
                "ReduxSDK.External"
            );
            catalogLoad.Completed += OnCatalogLoaded;
        }

        private static void RetainInitialProjectCatalog(
            IResourceLocator locator
        )
        {
            if (locator == null)
                return;

            var alreadyRegistered =
                Addressables.ResourceLocators.Contains(locator);
            if (!alreadyRegistered)
                Addressables.AddResourceLocator(locator);
            Debug.Log(
                $"{LogPrefix} Project Addressables catalog "
                    + $"'{locator.LocatorId}' remains available alongside "
                    + $"the external catalog (already registered: "
                    + $"{alreadyRegistered})."
            );
        }

        private static void LoadLinkedRoots()
        {
            var validEntries = manifest
                .Entries.Where(entry => entry?.Descriptor != null)
                .ToArray();
            foreach (var entry in manifest.Entries.Except(validEntries))
            {
                ReportFailure(
                    entry?.Descriptor?.StableId,
                    "The runtime manifest contains a missing descriptor."
                );
            }

            pendingRootLoads = validEntries.Length;
            if (pendingRootLoads == 0)
            {
                FailAll("The runtime manifest contains no valid descriptors.");
                return;
            }

            foreach (var entry in validEntries)
            {
                var descriptor = entry.Descriptor;
                var assetType = Type.GetType(descriptor.AssetType, false);
                if (
                    assetType == null
                    || !typeof(UnityEngine.Object).IsAssignableFrom(assetType)
                )
                {
                    ReportFailure(
                        descriptor.StableId,
                        $"The linked Addressables type '{descriptor.AssetType}' could "
                            + "not be resolved as a UnityEngine.Object type."
                    );
                    OnRootLoadFinished();
                    continue;
                }

                try
                {
                    typeof(LinkedAddressableRuntime)
                        .GetMethod(
                            nameof(StartTypedRootLoad),
                            BindingFlags.NonPublic | BindingFlags.Static
                        )
                        .MakeGenericMethod(assetType)
                        .Invoke(null, new object[] { entry });
                }
                catch (Exception exception)
                {
                    if (
                        exception is TargetInvocationException invocationException
                        && invocationException.InnerException != null
                    )
                    {
                        exception = invocationException.InnerException;
                    }
                    ReportFailure(
                        descriptor.StableId,
                        $"Could not start loading '{descriptor.Address}': {exception}"
                    );
                    OnRootLoadFinished();
                    continue;
                }

            }
        }

        private static void StartTypedRootLoad<T>(
            LinkedAddressableRuntimeEntry entry
        )
            where T : UnityEngine.Object
        {
            var descriptor = entry.Descriptor;
            var load = Addressables.LoadAssetAsync<T>(descriptor.Address);
            RootHandles.Add(load);
            load.Completed += operation =>
            {
                if (
                    operation.Status != AsyncOperationStatus.Succeeded
                    || operation.Result == null
                )
                {
                    ReportFailure(
                        descriptor.StableId,
                        $"Failed to load '{descriptor.Address}' as "
                            + $"'{typeof(T).FullName}': "
                            + (
                                operation.OperationException?.ToString()
                                ?? "the operation returned null"
                            )
                    );
                }
                else
                {
                    LoadedStableIds.Add(descriptor.StableId);
                    LoadedRoots[descriptor.StableId] = operation.Result;
                    Failures.Remove(descriptor.StableId);
                    var message =
                        $"Mounted '{descriptor.Address}' as "
                        + $"'{operation.Result.GetType().FullName}'.";
                    Debug.Log($"{LogPrefix} {message}");
                    LinkStatusChanged?.Invoke(descriptor.StableId, true, message);
                }

                OnRootLoadFinished();
            };
        }

        private sealed class EditorCatalogWaiter : IUpdateReceiver
        {
            private const int GraceUpdateCount = 120;
            private int remainingUpdates = GraceUpdateCount;

            public void Update(float unscaledDeltaTime)
            {
                if (
                    RegisteredCatalogContainsLinkedRoots()
                    || --remainingUpdates <= 0
                )
                {
                    ContinueAfterEditorCatalogRegistration();
                }
            }
        }

        private static void OnRootLoadFinished()
        {
            pendingRootLoads--;
            if (pendingRootLoads != 0)
                return;

            if (Failures.Count > 0)
            {
                RuntimeFailure =
                    $"{Failures.Count} linked Addressables root(s) failed to load.";
                Debug.LogError($"{LogPrefix} {RuntimeFailure}");
                return;
            }

            if (Application.isEditor)
            {
                IsReady = true;
                Debug.Log(
                    $"{LogPrefix} Mounted {LoadedStableIds.Count} external root(s). "
                        + "Editor scenes continue using persistent native linked objects."
                );
                return;
            }

            if (!manifest.UseTranslatedSceneBootstrap)
            {
                IsReady = true;
                Debug.Log(
                    $"{LogPrefix} Mounted {LoadedStableIds.Count} external root(s). "
                        + "The player continues in its normal build scene."
                );
                return;
            }

            LoadTranslatedPlayerContent();
        }

        private static void LoadTranslatedPlayerContent()
        {
            try
            {
                LoadTranslatedSupportBundles();
                var contentRoot = GetTranslatedContentRoot(manifest);

                if (!string.IsNullOrWhiteSpace(manifest.AssetBundleFileName))
                {
                    Debug.Log(
                        $"{LogPrefix} Loading translated asset bundle "
                            + $"'{manifest.AssetBundleFileName}'."
                    );
                    targetAssetBundle = AssetBundle.LoadFromFile(
                        Path.Combine(contentRoot, manifest.AssetBundleFileName)
                    );
                    if (targetAssetBundle == null)
                    {
                        throw new InvalidOperationException(
                            $"Unity could not load translated asset bundle "
                            + $"'{manifest.AssetBundleFileName}'."
                        );
                    }
                    Debug.Log(
                        $"{LogPrefix} Loaded translated asset bundle "
                            + $"'{manifest.AssetBundleFileName}'."
                    );
                }

                Debug.Log(
                    $"{LogPrefix} Loading translated scene bundle "
                        + $"'{manifest.SceneBundleFileName}'."
                );
                targetSceneBundle = AssetBundle.LoadFromFile(
                    Path.Combine(contentRoot, manifest.SceneBundleFileName)
                );
                if (targetSceneBundle == null)
                {
                    throw new InvalidOperationException(
                        $"Unity could not load translated scene bundle "
                        + $"'{manifest.SceneBundleFileName}'."
                    );
                }
                Debug.Log(
                    $"{LogPrefix} Loaded translated scene bundle "
                        + $"'{manifest.SceneBundleFileName}'."
                );

                var scenePaths = targetSceneBundle.GetAllScenePaths();
                var scenePath = scenePaths.FirstOrDefault(
                        candidate =>
                            string.Equals(
                                candidate,
                                manifest.InitialScenePath,
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    ?? scenePaths.FirstOrDefault(
                        candidate =>
                            string.Equals(
                                Path.GetFileName(candidate),
                                Path.GetFileName(manifest.InitialScenePath),
                                StringComparison.OrdinalIgnoreCase
                            )
                    )
                    ?? scenePaths.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(scenePath))
                {
                    throw new InvalidOperationException(
                        "The translated scene bundle contains no scenes."
                    );
                }

                Debug.Log($"{LogPrefix} Starting translated scene load '{scenePath}'.");
                var bootstrapScene = SceneManager.GetActiveScene();
                var sceneLoad = SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive
                );
                if (sceneLoad == null)
                {
                    throw new InvalidOperationException(
                        $"Unity could not start loading translated scene '{scenePath}'."
                    );
                }

                sceneLoad.completed += _ =>
                {
                    var translatedScene = SceneManager.GetSceneByPath(scenePath);
                    if (translatedScene.IsValid() && translatedScene.isLoaded)
                        SceneManager.SetActiveScene(translatedScene);

                    var unloadBootstrap =
                        bootstrapScene.IsValid() && bootstrapScene.isLoaded
                            ? SceneManager.UnloadSceneAsync(bootstrapScene)
                            : null;
                    if (unloadBootstrap == null)
                    {
                        FinishTranslatedSceneLoad(scenePath);
                    }
                    else
                    {
                        unloadBootstrap.completed += __ =>
                            FinishTranslatedSceneLoad(scenePath);
                    }
                };
            }
            catch (Exception exception)
            {
                FailAll($"Could not load translated player content: {exception}");
            }
        }

        private static void LoadTranslatedSupportBundles()
        {
            if (SupportBundles.Count > 0)
                return;

            var contentRoot = GetTranslatedContentRoot(manifest);
            foreach (
                var supportBundleFileName in manifest.SupportBundleFileNames
                    ?? Array.Empty<string>()
            )
            {
                Debug.Log(
                    $"{LogPrefix} Loading translated support bundle "
                        + $"'{supportBundleFileName}'."
                );
                var supportBundle = AssetBundle.LoadFromFile(
                    Path.Combine(contentRoot, supportBundleFileName)
                );
                if (supportBundle == null)
                {
                    throw new InvalidOperationException(
                        $"Unity could not load translated support bundle "
                            + $"'{supportBundleFileName}'."
                    );
                }
                SupportBundles.Add(supportBundle);
            }
        }

        private static void FinishTranslatedSceneLoad(string scenePath)
        {
            TranslatedSceneLoaded = true;
            IsReady = true;
            Debug.Log(
                $"{LogPrefix} Loaded translated scene '{scenePath}' with "
                    + $"{LoadedStableIds.Count} external Addressables root(s) held."
            );
        }

        private static string GetTranslatedContentRoot(
            LinkedAddressableRuntimeManifest runtimeManifest
        )
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                runtimeManifest.ContentDirectoryName
                    ?? "ReduxSDK/LinkedAddressables"
            );
        }

        private static string GetInitialSettingsRoot(
            LinkedAddressableRuntimeManifest runtimeManifest
        )
        {
            if (Application.isEditor)
                return ResolvedSourceRoot;

            var targetAddressablesRoot = Path.Combine(
                Application.streamingAssetsPath,
                "aa"
            );
            if (
                File.Exists(
                    Path.Combine(
                        targetAddressablesRoot,
                        runtimeManifest.SettingsFileName
                            ?? "settings.json"
                    )
                )
            )
            {
                return targetAddressablesRoot;
            }

            return GetStagedExternalMetadataRoot(runtimeManifest);
        }

        private static string GetStagedExternalMetadataRoot(
            LinkedAddressableRuntimeManifest runtimeManifest
        )
        {
            if (Application.isEditor)
                return ResolvedSourceRoot;

            return Path.GetFullPath(
                Path.Combine(
                    Application.streamingAssetsPath,
                    runtimeManifest.StagedExternalMetadataRelativePath
                        ?? "ReduxSDK/ExternalAddressables"
                )
            );
        }

        private static string ResolveSourceRoot(
            LinkedAddressableRuntimeManifest runtimeManifest
        )
        {
            if (!Application.isEditor && runtimeManifest.CopySourceToPlayer)
            {
                if (string.IsNullOrWhiteSpace(runtimeManifest.CopiedSourceRelativePath))
                    return null;

                return Path.GetFullPath(
                    Path.Combine(
                        Application.streamingAssetsPath,
                        runtimeManifest.CopiedSourceRelativePath
                    )
                );
            }

            return string.IsNullOrWhiteSpace(runtimeManifest.SourceRoot)
                ? null
                : Path.GetFullPath(runtimeManifest.SourceRoot);
        }

        private static string RedirectInternalId(IResourceLocation location)
        {
            var evaluated = AddressablesRuntimeProperties
                .EvaluateString(location.InternalId)
                .Replace('\\', '/');
            if (evaluated.Contains("://", StringComparison.Ordinal))
                return evaluated;

            if (
                evaluated.EndsWith(
                    ".bundle",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                var fileName = Path.GetFileName(evaluated);
                var externalPath = Path.Combine(
                        ResolvedSourceRoot,
                        "StandaloneWindows64",
                        fileName
                    );
                if (
                    ExternalBundleFileNames.Contains(fileName)
                    || (
                        !File.Exists(evaluated)
                        && File.Exists(externalPath)
                    )
                )
                {
                    return externalPath.Replace('\\', '/');
                }
            }

            return previousInternalIdTransform?.Invoke(location) ?? evaluated;
        }

        private static void FailAll(string failure)
        {
            RuntimeFailure = failure;
            Debug.LogError($"{LogPrefix} {failure}");
            foreach (var entry in manifest?.Entries ?? Array.Empty<LinkedAddressableRuntimeEntry>())
                ReportFailure(entry?.Descriptor?.StableId, failure, false);
        }

        private static void ReportFailure(
            string stableId,
            string failure,
            bool writeLog = true
        )
        {
            var key = string.IsNullOrWhiteSpace(stableId) ? "<missing-stable-id>" : stableId;
            Failures[key] = failure;
            if (writeLog)
                Debug.LogError($"{LogPrefix} {failure}");
            LinkStatusChanged?.Invoke(key, false, failure);
        }
    }
}
