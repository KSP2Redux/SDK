using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Resolves the active planet's <see cref="PQSDecalController" /> and lazily fills its
    /// <see cref="PQSDecalController.CoreCelestialBodyData" /> reference, which the wizard does
    /// not populate.
    /// </summary>
    /// <remarks>
    /// Without this reference, <see cref="PQSDecalInstance.UpdateDecalTransform" /> early-returns
    /// and the decal stays at world origin, hiding inside the planet. The PQS-to-controller
    /// lookup is cached because the auto-baker postprocessors hit Resolve on every postprocess
    /// pass and hierarchy change. On session start, all existing decal instances get one re-snap
    /// pass so legacy bodies whose controllers had no body reference at placement time still land
    /// at the right world position the first time the user enters preview.
    /// </remarks>
    [InitializeOnLoad]
    internal static class DecalControllerHelper
    {
        private static PQS _cachedPqs;
        private static PQSDecalController _cachedController;

        static DecalControllerHelper()
        {
            PlanetPreviewState.ActiveChanged += OnSessionChanged;
        }

        private static void OnSessionChanged()
        {
            var session = PlanetAuthoringSession.Active;
            if (session?.Pqs == null) return;
            var controller = Resolve(session.Pqs);
            if (controller?.PqsDecalInstanceList == null) return;
            // One-time re-snap of decals placed before the wizard's CoreCelestialBodyData wire-up
            // existed. New bodies have this populated at scene-creation time, so this is migration
            // for legacy bodies only.
            foreach (var inst in controller.PqsDecalInstanceList)
            {
                if (inst != null)
                {
                    inst.UpdateDecalTransform();
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="PQSDecalController" /> for the given <paramref name="planet" />, lazily wiring its <see cref="PQSDecalController.CoreCelestialBodyData" /> reference and ensuring its instance buffer is initialized.
        /// </summary>
        /// <param name="planet">The PQS to resolve a controller for.</param>
        /// <returns>The resolved controller, or null if the planet is null or has no controller in its hierarchy.</returns>
        public static PQSDecalController Resolve(PQS planet)
        {
            if (planet == null) return null;
            if (planet != _cachedPqs || _cachedController == null)
            {
                _cachedPqs = planet;
                // Explicit == null instead of ?? so Unity's fake-null sentinel is handled correctly.
                var controller = planet.GetComponent<PQSDecalController>();
                if (controller == null) controller = planet.GetComponentInChildren<PQSDecalController>();
                _cachedController = controller;
                if (_cachedController == null) return null;
                if (_cachedController.CoreCelestialBodyData == null)
                {
                    // The Scaled prefab carries CoreCelestialBodyData and the Local prefab carrying
                    // this controller is a sibling scene root, so a parent walk doesn't find it.
                    // Scan the active scene's roots instead. Authoring scenes hold exactly one body.
                    var body = BodyResolver.FindBodyInScene(planet.gameObject);
                    if (body != null)
                    {
                        _cachedController.CoreCelestialBodyData = body;
                        EditorUtility.SetDirty(_cachedController);
                    }
                }
            }
            // The PQS subdivision job calls GetDecalInstancesAllocatesAndCopy on the first frame
            // and NREs if _decalInstances was never initialized. RefreshDecalInstances allocates
            // it even when the list is empty, so leave this outside the cache gate.
            _cachedController.RefreshDecalInstances();
            return _cachedController;
        }
    }
}
