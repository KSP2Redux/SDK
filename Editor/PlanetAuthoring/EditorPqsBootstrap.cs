using KSP.Rendering;
using KSP.Rendering.Planets;
#if TK_ADDRESSABLE
using Ksp2UnityTools.Editor.Extensions;
#endif
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Editor-mode resolver for <see cref="PQSGlobalSettings" />.
    /// </summary>
    /// <remarks>
    /// The asset lives inside the base-game Graphics Manager prefab, so the addressable catalog must be registered first.
    /// </remarks>
    public static class EditorPqsBootstrap
    {
        // Same key the runtime uses (GameManager.CreateGraphicsManager, PqsVisSetup.graphicsManagerPath).
        // Resolves once ThunderKit's "Import Ksp2 To Editor" pipeline registers the catalog.
        private const string GRAPHICS_MANAGER_ADDRESSABLE_KEY = "Graphics Manager.prefab";

        private static PQSGlobalSettings? _cachedSettings;
        private static AsyncOperationHandle<GameObject> _graphicsManagerHandle;
        private static bool _warnedMissingCatalog;

        /// <summary>
        /// Gets the shipped <see cref="PQSGlobalSettings" />, or <c>null</c> when the base-game catalog is not registered.
        /// </summary>
        /// <remarks>
        /// Logs an error once per session when the catalog is unavailable. Non-null results are cached until the asset is unloaded, which a play session's teardown does. A null result is not cached, so the next access self-heals once the catalog loads. When the catalog check fails, the imported catalogs are registered on the spot before giving up, which covers the gap between a domain reload and their deferred load.
        /// </remarks>
        public static PQSGlobalSettings? PQSGlobalSettings
        {
            get
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                // The settings were unloaded with the prefab, so the old handle points at a destroyed
                // asset. Keeping it would hand the same destroyed result back to the next load.
                ReleaseGraphicsManager();

#if TK_ADDRESSABLE
                if (!IsCatalogRegistered())
                {
                    LoadTkImportedCatalog.EnsureRegistered();
                }
#endif

                if (!IsCatalogRegistered())
                {
                    WarnMissingCatalogOnce();
                    return null;
                }

                _graphicsManagerHandle = Addressables.LoadAssetAsync<GameObject>(GRAPHICS_MANAGER_ADDRESSABLE_KEY);
                _graphicsManagerHandle.WaitForCompletion();

                // Result == null uses Unity's overloaded operator==, so a destroyed prefab from a
                // load whose asset got unloaded is caught here instead of falling through C#'s ?.
                // operator and tripping MissingReferenceException.
                var prefab = _graphicsManagerHandle.Status == AsyncOperationStatus.Succeeded ? _graphicsManagerHandle.Result : null;
                var graphicsManager = prefab == null ? null : prefab.GetComponent<GraphicsManager>();
                _cachedSettings = graphicsManager == null ? null : graphicsManager.PQSGlobalSettings;

                if (_cachedSettings == null)
                {
                    ReleaseGraphicsManager();
                    WarnMissingCatalogOnce();
                }

                return _cachedSettings;
            }
        }

        private static void ReleaseGraphicsManager()
        {
            if (_graphicsManagerHandle.IsValid())
            {
                Addressables.Release(_graphicsManagerHandle);
            }

            _graphicsManagerHandle = default;
        }

        /// <summary>
        /// Message logged once per domain when the base-game catalog is unavailable.
        /// </summary>
        /// <remarks>
        /// Exposed so tests running without the ThunderKit import can expect this exact error rather
        /// than suppressing log failures wholesale.
        /// </remarks>
        public static string MissingCatalogMessage =>
            $"[EditorPqsBootstrap] '{GRAPHICS_MANAGER_ADDRESSABLE_KEY}' not found. " +
            "Run ThunderKit > Pipelines > Import Ksp2 To Editor and reopen the authoring scene.";

        /// <summary>
        /// Gets a value indicating whether the missing-catalog error has already been logged this domain.
        /// </summary>
        /// <remarks>
        /// The error fires once per domain. A test that expects it has to know whether it is still
        /// pending, because expecting a message that already fired fails just as hard as an
        /// unexpected one.
        /// </remarks>
        public static bool HasWarnedMissingCatalog => _warnedMissingCatalog;

        /// <summary>
        /// Reports whether the base-game addressable catalog carries the Graphics Manager prefab.
        /// </summary>
        /// <returns>True if the key resolves to at least one location, false otherwise.</returns>
        /// <remarks>
        /// Asking for locations is the only way to test a key without Addressables logging an
        /// InvalidKeyException for the miss. That log is not ours to format or suppress, and an
        /// unexpected error entry fails any EditMode test that happens to touch this path.
        /// </remarks>
        public static bool IsCatalogRegistered()
        {
            var locations = Addressables.LoadResourceLocationsAsync(GRAPHICS_MANAGER_ADDRESSABLE_KEY);
            locations.WaitForCompletion();
            bool found = locations.Result is { Count: > 0 };
            Addressables.Release(locations);
            return found;
        }

        private static void WarnMissingCatalogOnce()
        {
            if (_warnedMissingCatalog)
                return;

            _warnedMissingCatalog = true;
            Debug.LogError(MissingCatalogMessage);
        }
    }
}
