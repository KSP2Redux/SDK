using KSP;
using KSP.Rendering.Planets;
using KSP.Tools.PQSFreeCamUtils;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Shared ray-against-planet-surface hit utility used by Surface Pick and Place Decal tools.
    /// </summary>
    /// <remarks>
    /// Iteratively refines a ray-sphere bracket against the actual terrain so the hit lands on
    /// displaced ground rather than the smooth radius. Returns world-space hit, body-local lat/lon
    /// in degrees, and altitude above mean radius (positive on mountains, negative in basins).
    /// </remarks>
    public static class PlanetSurfaceHit
    {
        private const int RefinementIterations = 6;
        private const double RefinementToleranceFraction = 1e-5;

        /// <summary>
        /// Casts <paramref name="ray" /> against <paramref name="planet" /> and refines the hit against the displaced terrain.
        /// </summary>
        /// <param name="planet">The PQS to cast against.</param>
        /// <param name="ray">The world-space ray.</param>
        /// <param name="hitWorld">The refined world-space hit point on the displaced surface.</param>
        /// <param name="latLon">The body-local (latitude, longitude) of the hit, in degrees.</param>
        /// <param name="altitudeAboveMean">The hit altitude above the body's mean radius, in meters. Positive on mountains, negative in basins.</param>
        /// <param name="includeDecals">
        /// True to match the visible rendered surface (raised KSC pad and other decals). Decal-
        /// modifying tools pass false so they sample against the underlying terrain rather than
        /// stacking on top of their own decal.
        /// </param>
        /// <returns>True if the ray hit the body, false if it missed or the body has no resolvable radius.</returns>
        public static bool TryHit(PQS planet, Ray ray, out Vector3 hitWorld, out Vector2 latLon, out double altitudeAboveMean, bool includeDecals = true)
        {
            hitWorld = default;
            latLon = default;
            altitudeAboveMean = 0;
            if (planet == null) return false;

            var center = planet.transform.position;
            var radius = BodyResolver.FindBody(planet)?.Data?.radius ?? 0;
            if (radius <= 0) return false;

            Vector3d localRayPos = (Vector3d)(ray.origin - center);
            Vector3d rayDir = (Vector3d)ray.direction;
            var invRot = Quaternion.Inverse(planet.transform.rotation);
            var tolerance = radius * RefinementToleranceFraction;

            if (!PositionUtils.RaycastParametricSphere(radius, localRayPos, rayDir, out var t1, out var t2)) return false;
            var t = t1 > 0 ? t1 : t2;
            if (t <= 0) return false;

            var surfaceDistance = radius;
            var worldDir = Vector3.zero;
            var localDir = Vector3.zero;
            for (var i = 0; i < RefinementIterations; i++)
            {
                var hitGuess = ray.origin + ray.direction * (float)t;
                worldDir = (hitGuess - center).normalized;
                localDir = invRot * worldDir;
                var newSurfaceDistance = planet.GetSurfaceHeight(((Vector3d)localDir).normalized, includeDecals);
                if (newSurfaceDistance <= 0) return false;

                if (!PositionUtils.RaycastParametricSphere(newSurfaceDistance, localRayPos, rayDir, out var nt1, out var nt2))
                {
                    // Ray missed the displaced sphere. Keep the previous valid (t, surfaceDistance) pair so hitWorld stays on the ray. Updating surfaceDistance here would desync it from t.
                    break;
                }
                var newT = nt1 > 0 ? nt1 : nt2;
                if (newT <= 0) break;

                var converged = System.Math.Abs(newSurfaceDistance - surfaceDistance) < tolerance;
                t = newT;
                surfaceDistance = newSurfaceDistance;
                if (converged) break;
            }

            var finalHit = ray.origin + ray.direction * (float)t;
            worldDir = (finalHit - center).normalized;
            localDir = invRot * worldDir;
            hitWorld = center + worldDir * (float)surfaceDistance;
            altitudeAboveMean = surfaceDistance - radius;

            var lonLat = PositionUtils.GetLonLatFromRadialPos((Vector3d)localDir);
            // PositionUtils returns (lon, lat). Standard convention is (lat, lon).
            latLon = new Vector2((float)lonLat.y, (float)lonLat.x);
            return true;
        }
    }
}
