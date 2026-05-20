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

                var handle = Addressables.LoadAssetAsync<GameObject>(GraphicsManagerAddressableKey);
                handle.WaitForCompletion();

                // handle.Result == null uses Unity's overloaded operator==, so destroyed
                // GameObjects (from a prior load whose asset got unloaded) are caught here
                // instead of falling through C#'s ?. operator and tripping MissingReferenceException.
                var prefab = handle.Result;
                var graphicsManager = prefab == null ? null : prefab.GetComponent<GraphicsManager>();
                _cachedSettings = graphicsManager == null ? null : graphicsManager.PQSGlobalSettings;

                if (_cachedSettings == null && !_warnedMissingCatalog)
                {
                    _warnedMissingCatalog = true;
                    Debug.LogError(
                        $"[EditorPqsBootstrap] '{GraphicsManagerAddressableKey}' not found. " +
                        "Run ThunderKit > Pipelines > Import Ksp2 To Editor and reopen the authoring scene."
                    );
                }

                return _cachedSettings;
            }
        }
    }
}
