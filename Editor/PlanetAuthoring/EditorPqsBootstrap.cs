using KSP.Rendering;
using KSP.Rendering.Planets;
using UnityEngine;
using UnityEngine.AddressableAssets;

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
        private const string GraphicsManagerAddressableKey = "Graphics Manager.prefab";

        private static PQSGlobalSettings? _cachedSettings;
        private static bool _warnedMissingCatalog;

        /// <summary>
        /// Gets the shipped <see cref="PQSGlobalSettings" />, or <c>null</c> when the base-game catalog is not registered.
        /// </summary>
        /// <remarks>
        /// Logs an error once per session when the catalog is unavailable. Non-null results are cached for the lifetime of the domain. A null result is not cached, so the next access self-heals once the catalog loads.
        /// </remarks>
        public static PQSGlobalSettings? PQSGlobalSettings
        {
            get
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                if (!IsCatalogRegistered())
                {
                    WarnMissingCatalogOnce();
                    return null;
                }

                var handle = Addressables.LoadAssetAsync<GameObject>(GraphicsManagerAddressableKey);
                handle.WaitForCompletion();

                // handle.Result == null uses Unity's overloaded operator==, so destroyed
                // GameObjects (from a prior load whose asset got unloaded) are caught here
                // instead of falling through C#'s ?. operator and tripping MissingReferenceException.
                var prefab = handle.Result;
                var graphicsManager = prefab == null ? null : prefab.GetComponent<GraphicsManager>();
                _cachedSettings = graphicsManager == null ? null : graphicsManager.PQSGlobalSettings;

                if (_cachedSettings == null)
                {
                    WarnMissingCatalogOnce();
                }

                return _cachedSettings;
            }
        }

        /// <summary>
        /// Message logged once per domain when the base-game catalog is unavailable.
        /// </summary>
        /// <remarks>
        /// Exposed so tests running without the ThunderKit import can expect this exact error rather
        /// than suppressing log failures wholesale.
        /// </remarks>
        public static string MissingCatalogMessage =>
            $"[EditorPqsBootstrap] '{GraphicsManagerAddressableKey}' not found. " +
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
            var locations = Addressables.LoadResourceLocationsAsync(GraphicsManagerAddressableKey);
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
