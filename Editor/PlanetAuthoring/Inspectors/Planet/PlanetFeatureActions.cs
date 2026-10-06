using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere;
using Ksp2UnityTools.Editor.PlanetAuthoring.Clouds;
using Ksp2UnityTools.Editor.PlanetAuthoring.Ocean;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Adds, refits and removes a body's atmosphere, clouds, ocean and scatter, keeping a running preview in step.
    /// </summary>
    /// <remarks>
    /// Each method returns the message to show the author. Removal asks for confirmation first, listing what goes.
    /// </remarks>
    public static class PlanetFeatureActions
    {
        /// <summary>
        /// Adds the body's visual atmosphere, or refits the one it has.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show.</returns>
        public static string AddAtmosphere(CoreCelestialBodyData body)
        {
            bool added = AtmosphereSetup.TryAddAtmosphere(body, out _, out string message);

            // A running preview attached its drivers before this body had an atmosphere. Attach is a no-op when the
            // driver is already booted.
            if (added)
            {
                PlanetAuthoringSession.Active?.AtmosphereDriver?.Attach();
            }

            return message;
        }

        /// <summary>
        /// Removes the body's visual atmosphere after confirmation.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show, or null if nothing was removed.</returns>
        public static string RemoveAtmosphere(CoreCelestialBodyData body)
        {
            if (!Confirm("Remove Atmosphere", AtmosphereSetup.DescribeRemoval(body),
                    "Has Atmosphere, Atmosphere Depth and the pressure curves are left as they are."))
                return null;

            // The preview's driver holds the model and its realtime tables, so it lets go first.
            PlanetAuthoringSession.Active?.AtmosphereDriver?.Detach();
            AtmosphereSetup.TryRemoveAtmosphere(body, out string message);
            return message;
        }

        /// <summary>
        /// Adds the body's clouds, or refits the ones it has.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show.</returns>
        public static string AddClouds(CoreCelestialBodyData body)
        {
            bool added = CloudSetup.TryAddClouds(body, out _, out string message);
            if (added)
            {
                PlanetAuthoringSession.Active?.CloudDriver?.Attach();
            }

            return message;
        }

        /// <summary>
        /// Removes the body's clouds after confirmation.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show, or null if nothing was removed.</returns>
        public static string RemoveClouds(CoreCelestialBodyData body)
        {
            if (!Confirm("Remove Clouds", CloudSetup.DescribeRemoval(body), null))
                return null;

            // The preview's driver holds the configuration and the helper's registration, so it lets go first.
            PlanetAuthoringSession.Active?.CloudDriver?.Detach();
            CloudSetup.TryRemoveClouds(body, out string message);
            return message;
        }

        /// <summary>
        /// Adds the body's ocean from stock Kerbin's, or re-wires the one it has.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show.</returns>
        public static string AddOcean(CoreCelestialBodyData body)
        {
            bool added = OceanSetup.TryAddOcean(body, "Kerbin", out string message);
            if (added)
            {
                PlanetAuthoringSession.Active?.OceanDriver?.Attach();
            }

            return message;
        }

        /// <summary>
        /// Removes the body's ocean after confirmation.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show, or null if nothing was removed.</returns>
        public static string RemoveOcean(CoreCelestialBodyData body)
        {
            if (!Confirm("Remove Ocean", OceanSetup.DescribeRemoval(body), null))
                return null;

            // The preview's driver holds the renderer's ocean and the water manager, so it lets go first.
            PlanetAuthoringSession.Active?.OceanDriver?.Detach();
            OceanSetup.TryRemoveOcean(body, out string message);
            return message;
        }

        /// <summary>
        /// Adds and configures the body's scatter system, or repairs the one it has.
        /// </summary>
        /// <param name="body">The body.</param>
        /// <returns>The message to show.</returns>
        public static string ConfigureScatter(CoreCelestialBodyData body)
        {
            PQS pqs = body != null ? BodyResolver.FindPqs(body) : null;
            if (pqs == null)
                return "No PQS on this body. Terrain scatter needs a solid surface.";

            return ScatterSystemLocator.Configure(pqs, body) != null
                ? "Scatter system configured."
                : "Could not configure the scatter system.";
        }

        private static bool Confirm(string title, List<string> removals, string leftAlone) =>
            removals.Count > 0
            && EditorUtility.DisplayDialog(
                title,
                "This removes:\n\n- " + string.Join("\n- ", removals) + "\n\nAssets go to the trash."
                + (string.IsNullOrEmpty(leftAlone) ? string.Empty : " " + leftAlone),
                "Remove",
                "Cancel");
    }
}
