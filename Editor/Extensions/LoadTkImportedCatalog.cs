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
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // Prepare the fresh Addressables implementation before any runtime object can
                    // load imported KSP2 content. EnteredPlayMode is too late for some startup
                    // paths when Reload Domain is disabled.
                    ResetAddressablesForPlayMode();
                    EnsureImportedCatalogsLoaded();
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
                    SessionState.SetBool(PlaySessionInitializedKey, true);
                    EnsureImportedCatalogsLoaded();
                    break;
            }
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

            EnsureImportedCatalogsLoaded();
        }

        // Registers the ThunderKit-imported content catalog(s) with Addressables if they are not already
        // registered. Idempotent, so it is safe to call on every domain reload and every Play Mode enter.
        private static void EnsureImportedCatalogsLoaded()
        {
            string ksp2CatalogFullPath = Path.Join(Application.dataPath, loadTkImportedCatalogPath);
            if (!File.Exists(ksp2CatalogFullPath))
            {
                return;
            }

            EnsureThunderKitInternalIdRedirect();

            if (!Addressables.ResourceLocators.Select(rl => rl.LocatorId).Contains(ksp2CatalogFullPath))
            {
                AsyncOperationHandle<IResourceLocator> loadKspCatalogTask =
                    Addressables.LoadContentCatalogAsync(ksp2CatalogFullPath, true);
                loadKspCatalogTask.WaitForCompletion();
            }

            // Load the Redux asset catalog too, if and only if this is the package version of Redux SDK and there is
            // no locator already registerd
            if (Assembly.GetExecutingAssembly().FullName == "ksp2community.ksp2unitytools.editor" && !Addressables
                .ResourceLocators
                .Select(rl => rl.LocatorId)
                .Where(id => id.Contains(reduxCatalogPath))
                .Any())
            {
                string reduxCatalogFullPath = Path.Join(Application.dataPath, reduxCatalogPath);
                AsyncOperationHandle<IResourceLocator> loadKspCatalogTask =
                    Addressables.LoadContentCatalogAsync(reduxCatalogFullPath, true);
                loadKspCatalogTask.WaitForCompletion();
            }
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
