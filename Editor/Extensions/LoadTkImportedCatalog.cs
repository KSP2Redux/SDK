using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;

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
                    ArmAddressablesReinitialize();
                    return;
                case PlayModeStateChange.ExitingPlayMode:
                    SessionState.SetBool(PlaySessionInitializedKey, false);
                    return;
                case PlayModeStateChange.EnteredEditMode:
                    UnloadLeakedAssetBundles();
                    return;
                // EnteredPlayMode fires after Addressables' play-mode (FastMode) init but before the game's
                // startup flow loads kspFlow.unity (via GameManager's LoadingFlow), so this is in time.
                // SessionState protects against stale duplicate callbacks left behind by a script hot
                // reload. Addressables was armed at ExitingEditMode so Unity's normal Fast Mode setup,
                // which runs before this callback, owns creation of the fresh implementation.
                case PlayModeStateChange.EnteredPlayMode when SessionState.GetBool(PlaySessionInitializedKey, false):
                    return;
                case PlayModeStateChange.EnteredPlayMode:
                    SessionState.SetBool(PlaySessionInitializedKey, true);
                    EnsureImportedCatalogsLoaded();
                    break;
            }
        }

        // With Reload Domain disabled the Addressables implementation is rebuilt on the next Play
        // Mode enter (see ArmAddressablesReinitialize), but native AssetBundles held by the old
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

        private static void ArmAddressablesReinitialize()
        {
            FieldInfo reinitializeField = typeof(Addressables).GetField(
                "reinitializeAddressables",
                BindingFlags.Static | BindingFlags.NonPublic
            );
            reinitializeField?.SetValue(null, true);
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
#endif
    }
}
