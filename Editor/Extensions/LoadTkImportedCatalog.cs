using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Ksp2UnityTools.Editor.Extensions
{
#if TK_ADDRESSABLE
    [InitializeOnLoad]
#endif
    public class LoadTkImportedCatalog : AssetPostprocessor
    {
        // This must be the same as the output of the ImportKsp2ToEditor pipeline.
        private const string loadTkImportedCatalogPath = "DoNotDistribute/aa/catalog.json";

        // Path must be relative to Assets directory.
        private const string reduxCatalogPath = "../Redux/Addressables/StandaloneWindows64/catalog.json";

        private const string PlaySessionInitializedKey =
            "Ksp2UnityTools.LoadTkImportedCatalog.PlaySessionInitialized";

        private const string BaseGameCatalogProbeKey = "kspFlow.unity";

        private static bool _catalogLoadScheduled;
        private static bool _catalogLoadInProgress;
        private static bool _catalogLoadWaitInProgress;
        private static AsyncOperationHandle<IResourceLocator> _catalogLoadOperation;

#if TK_ADDRESSABLE
        static LoadTkImportedCatalog()
        {
            // The imported catalog locators are normally registered after a domain reload (see
            // OnPostprocessAllAssets). Entering Play Mode re-runs Addressables'
            // FastModeInitializationOperation, which rebuilds the resource locators from the project's
            // AddressableAssetSettings and drops the imported catalog. With Domain Reload enabled the
            // play-mode reload re-fires the postprocessor and re-registers it. With Reload Domain
            // disabled that never happens, so on the second Play Mode enter base-game content registered
            // only in the imported catalog (e.g. the kspFlow.unity scene) becomes unresolvable
            // ("No Location found for Key=kspFlow.unity"). Re-register on entering Play Mode to restore
            // the domain-reload behavior. Unsubscribe first so a domain reload can't double-subscribe.
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        private static void OnBeforeAssemblyReload()
        {
            EditorApplication.delayCall -= BeginImportedCatalogLoad;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // SessionState survives domain reloads. Clear it before every new play
                    // transition so a previous interrupted/hot-reloaded session cannot suppress
                    // catalog registration for the next benchmark process.
                    SessionState.SetBool(PlaySessionInitializedKey, false);
                    return;
                case PlayModeStateChange.ExitingPlayMode:
                    SessionState.SetBool(PlaySessionInitializedKey, false);
                    return;
                case PlayModeStateChange.EnteredEditMode:
                    UnloadLeakedAssetBundles();
                    return;
                // ExitingEditMode prepares Fast Mode and the imported catalogs before runtime startup.
                // Recheck them here so manual and automated transitions share the same idempotent path.
                // SessionState protects against stale duplicate callbacks left behind by a script hot
                // reload.
                case PlayModeStateChange.EnteredPlayMode when SessionState.GetBool(PlaySessionInitializedKey, false):
                    return;
                case PlayModeStateChange.EnteredPlayMode:
                    EnsureImportedCatalogsLoaded();
                    // Mark initialized only after registration succeeds. If catalog loading throws,
                    // a duplicate callback can retry instead of preserving a false-success state.
                    SessionState.SetBool(PlaySessionInitializedKey, true);
                    break;
            }
        }

        // This callback runs after the editor has prepared its Play Mode state but before runtime
        // initialization and scene Start methods. EnteredPlayMode is too late for BootstrapSceneLoader,
        // while ExitingEditMode can be followed by Addressables' own delayed reinitialization.
        [InitializeOnEnterPlayMode]
        private static void PrepareAddressablesForPlayMode()
        {
            SessionState.SetBool(PlaySessionInitializedKey, false);
            ResetAddressablesForPlayMode();
            EnsureImportedCatalogsLoaded();
            SessionState.SetBool(PlaySessionInitializedKey, true);
        }

        // With Reload Domain disabled the Addressables implementation is rebuilt on the next Play
        // Mode enter, but native AssetBundles held by the old
        // implementation are never unloaded. After a dirty play exit (crash or aborted load flow)
        // their refcounts never hit zero, the bundle files stay resident, and the next session's
        // fresh implementation fails to load them again with "another AssetBundle with the same
        // files is already loaded", which cascades into unresolvable base-game content. Sweep them
        // once the editor is fully back in edit mode, after the game's own teardown has released
        // whatever it released cleanly. The "ksp2" BundleKit catalog bundle is excluded because the
        // edit-mode ReduxResourceAdapter keeps using it between play sessions.
        private static void UnloadLeakedAssetBundles()
        {
            foreach (var bundle in AssetBundle.GetAllLoadedAssetBundles().ToArray())
            {
                if (bundle == null || bundle.name == "ksp2")
                {
                    continue;
                }
                bundle.Unload(true);
            }
        }

        /// <summary>
        /// Loads an addressable catalog into a resource locator after domain reload.
        /// This makes the path identifiers in the catalog accessible in the editor,
        /// even if the catalog isn't generated by this project's addressable build system.
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload
        )
        {
            if (!didDomainReload)
            {
                return;
            }

            ScheduleImportedCatalogLoad();
        }

        // Catalog loads use Addressables' editor update loop. Waiting for that loop from an asset
        // import callback can block the main thread indefinitely, so domain reloads start the work
        // after the asset database has returned control to the editor.
        private static void ScheduleImportedCatalogLoad()
        {
            if (_catalogLoadScheduled || _catalogLoadInProgress)
            {
                return;
            }

            _catalogLoadScheduled = true;
            EditorApplication.delayCall += BeginImportedCatalogLoad;
        }

        private static void BeginImportedCatalogLoad()
        {
            EditorApplication.delayCall -= BeginImportedCatalogLoad;
            _catalogLoadScheduled = false;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ScheduleImportedCatalogLoad();
                return;
            }

            string catalogPath = GetNextMissingCatalogPath();
            if (catalogPath == null)
            {
                return;
            }

            EnsureThunderKitInternalIdRedirect();
            _catalogLoadInProgress = true;
            _catalogLoadOperation = Addressables.LoadContentCatalogAsync(catalogPath, false);
            _catalogLoadOperation.Completed += OnImportedCatalogLoaded;
        }

        private static void OnImportedCatalogLoaded(AsyncOperationHandle<IResourceLocator> operation)
        {
            _catalogLoadInProgress = false;
            bool succeeded = operation.Status == AsyncOperationStatus.Succeeded;
            Exception operationException = operation.OperationException;
            if (!_catalogLoadWaitInProgress)
            {
                Addressables.Release(operation);
            }

            if (!succeeded)
            {
                Debug.LogError(
                    $"Failed to load a ThunderKit imported Addressables catalog: {operationException}"
                );
                return;
            }

            BeginImportedCatalogLoad();
        }

        // Registers the ThunderKit-imported content catalog(s) with Addressables if they are not already
        // registered. Idempotent, so it is safe to call on every domain reload and every Play Mode enter.
        private static void EnsureImportedCatalogsLoaded()
        {
            EditorApplication.delayCall -= BeginImportedCatalogLoad;
            _catalogLoadScheduled = false;

            while (_catalogLoadInProgress)
            {
                AsyncOperationHandle<IResourceLocator> operation = _catalogLoadOperation;
                if (!operation.IsValid())
                {
                    break;
                }

                IResourceLocator locator;
                try
                {
                    _catalogLoadWaitInProgress = true;
                    locator = operation.WaitForCompletion();
                }
                finally
                {
                    _catalogLoadWaitInProgress = false;
                }

                Exception operationException = operation.OperationException;
                Addressables.Release(operation);
                if (locator == null)
                {
                    Debug.LogError(
                        $"Failed while waiting for a ThunderKit imported Addressables catalog: {operationException}"
                    );
                    return;
                }
            }

            string catalogPath = GetNextMissingCatalogPath();
            if (catalogPath == null)
            {
                return;
            }

            EnsureThunderKitInternalIdRedirect();
            while (catalogPath != null)
            {
                AsyncOperationHandle<IResourceLocator> operation =
                    Addressables.LoadContentCatalogAsync(catalogPath, false);
                IResourceLocator locator = operation.WaitForCompletion();
                Exception operationException = operation.OperationException;
                Addressables.Release(operation);
                if (locator == null)
                {
                    Debug.LogError(
                        $"Failed to load the ThunderKit imported Addressables catalog at {catalogPath}: {operationException}"
                    );
                    return;
                }

                catalogPath = GetNextMissingCatalogPath();
            }
        }

        private static string GetNextMissingCatalogPath()
        {
            string ksp2CatalogFullPath = Path.Join(Application.dataPath, loadTkImportedCatalogPath);
            if (!File.Exists(ksp2CatalogFullPath))
            {
                return null;
            }

            // BundleKit and other editor integrations can register the stock
            // catalog under its catalog ID instead of this imported file path.
            // Loading it again duplicates every stock location and can leave
            // systems such as cloud rendering with mismatched result lists.
            if (
                !CatalogKeyIsRegistered(BaseGameCatalogProbeKey)
                && !IsCatalogLoaded(ksp2CatalogFullPath)
            )
            {
                return ksp2CatalogFullPath;
            }

            // Load the Redux asset catalog too, if and only if this is the package version of Redux SDK and there is
            // no locator already registerd
            if (Assembly.GetExecutingAssembly().GetName().Name == "ksp2community.ksp2unitytools.editor")
            {
                string reduxCatalogFullPath = Path.Join(Application.dataPath, reduxCatalogPath);
                if (File.Exists(reduxCatalogFullPath) && !IsCatalogLoaded(reduxCatalogFullPath))
                {
                    return reduxCatalogFullPath;
                }
            }

            return null;
        }

        private static bool IsCatalogLoaded(string catalogPath)
        {
            return Addressables.ResourceLocators.Any(locator => locator.LocatorId == catalogPath);
        }

        private static bool CatalogKeyIsRegistered(string key)
        {
            return Addressables.ResourceLocators.Any(
                locator =>
                    locator != null
                    && locator.Keys.OfType<string>().Any(
                        candidate =>
                            string.Equals(candidate, key, StringComparison.Ordinal)
                    )
            );
        }

        // Addressables stores this transform on its internal implementation. With Reload Domain
        // disabled, Addressables replaces that implementation after Play Mode exit, so ThunderKit's
        // redirect (normally installed only on domain load) must be restored before the next session
        // uses an imported catalog location.
        private static void EnsureThunderKitInternalIdRedirect()
        {
            if (Addressables.InternalIdTransformFunc != null)
            {
                return;
            }

            MethodInfo redirectMethod = typeof(ThunderKit.Addressable.Tools.AddressableGraphicsSettings)
                .GetMethod(
                    "RedirectInternalIdsToGameDirectory",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
            if (redirectMethod == null)
            {
                throw new MissingMethodException(
                    typeof(ThunderKit.Addressable.Tools.AddressableGraphicsSettings).FullName,
                    "RedirectInternalIdsToGameDirectory"
                );
            }

            Addressables.InternalIdTransformFunc =
                (Func<IResourceLocation, string>)redirectMethod.CreateDelegate(
                    typeof(Func<IResourceLocation, string>)
                );
        }

        // Addressables normally schedules this reset through EditorApplication.delayCall after
        // leaving Play Mode. An automated Play Mode restart can begin its next transition before that
        // callback runs, leaving a stale ResourceManager and its unloaded bundle resources in place.
        private static void ResetAddressablesForPlayMode()
        {
            // Addressables queues this callback when Play Mode exits. An automated restart can
            // reach ExitingEditMode before the callback runs; if it runs after the fresh instance
            // and imported catalogs are prepared below, the first runtime Addressables access
            // replaces that instance again and loses both the catalog and path redirect.
            MethodInfo delayedReinitializeMethod = typeof(Addressables).GetMethod(
                "EnableReinitializeAddressablesFlag",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            if (delayedReinitializeMethod != null)
            {
                var delayedReinitialize = (EditorApplication.CallbackFunction)
                    delayedReinitializeMethod.CreateDelegate(typeof(EditorApplication.CallbackFunction));
                EditorApplication.delayCall -= delayedReinitialize;
            }

            FieldInfo reinitializeField = typeof(Addressables).GetField(
                "reinitializeAddressables",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            if (reinitializeField == null)
            {
                throw new MissingFieldException(typeof(Addressables).FullName, "reinitializeAddressables");
            }

            reinitializeField.SetValue(null, true);
        }

#endif
    }
}
