using System;
using KSP.Rendering;
using KSP.Rendering.Planets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// A few basic services for hosting the PQS in edit mode
    /// </summary>
    public static class PlanetAuthoringServices
    {
        // Same key the runtime uses (GameManager.CreateGraphicsManager, PqsVisSetup.graphicsManagerPath).
        // Resolves in edit mode after ThunderKit's LoadTkImportedCatalog registers the base-game catalog.
        private const string GraphicsManagerAddressableKey = "Graphics Manager.prefab";

        private static PQSGlobalSettings? _cachedSettings;

        /// <summary>
        /// Gets the shipped PQSGlobalSettings from the base-game Graphics Manager addressable.
        /// Requires the ThunderKit "Import Ksp2 To Editor" pipeline to have run so the base-game catalog is registered.
        /// </summary>
        public static PQSGlobalSettings PQSGlobalSettings
        {
            get
            {
                if (_cachedSettings != null)
                    return _cachedSettings;

                var handle = Addressables.LoadAssetAsync<GameObject>(GraphicsManagerAddressableKey);
                handle.WaitForCompletion();
                _cachedSettings = handle.Result?.GetComponent<GraphicsManager>()?.PQSGlobalSettings;
                return _cachedSettings;
            }
        }

        public static ReadOnlySpan<Vector3d> InterestPositions => Array.Empty<Vector3d>();

        public static double GetSurfaceHeight(PQS pqs, Vector3d radialDirection, bool includeDecals)
        {
            return pqs == null ? 0.0 : pqs.GetSurfaceHeight(radialDirection, includeDecals);
        }

        /// <summary>
        /// Runs one tick of the PQS update pipeline against the editor SceneView camera, in the same order
        /// the runtime uses. Skips silently if the PQS is unbound or has not been activated.
        /// </summary>
        /// <param name="pqs">The PQS to advance one frame.</param>
        public static void TickPqs(PQS pqs)
        {
            if (pqs == null || pqs.PQSRenderer == null)
                return;

            if (!pqs.HasPrimaryTarget || !pqs.IsRunning())
                return;

            pqs.UpdateSurfaceMaterial();
            pqs.UpdateSphere();
            pqs.PQSRenderer.LateUpdateForEditor();
        }
    }
}
