using System;
using KSP;
using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Per-session readout of the SceneView camera's spatial relationship to the previewed body.
    /// </summary>
    /// <remarks>
    /// Owned by <see cref="PlanetAuthoringSession" />. Authoring widgets (trapezoid windows, fade
    /// curves) subscribe to <see cref="ActiveChanged" /> so they can paint a "you are here"
    /// reference line on their graphs.
    ///
    /// Each tick samples the camera's radial direction and reads the terrain elevation at that
    /// direction directly from the PQS heightmap via <c>PQS.GetSurfaceHeight</c>. Working off
    /// heightmap data avoids any dependency on collider creation, prevents snagging on decal or
    /// marker colliders in the scene, and stays consistent with the runtime's own elevation queries.
    ///
    /// Slope is intentionally not exposed: the shader computes slope from per-layer height-map
    /// gradient magnitudes (Prepass.cginc:215, <c>length(grad) * 90</c>) with per-layer
    /// <c>GradMapWeights</c> mixing the contributions, so there is no single value to display.
    ///
    /// The static <see cref="Active" /> getter and <see cref="ActiveChanged" /> event are thin
    /// forwarders to the active session's instance, so subscribers do not need to track session
    /// lifecycle themselves.
    /// </remarks>
    public sealed class PlanetPreviewState
    {
        // Below this the change is sub-pixel at any reasonable trapezoid/fade graph width and not
        // worth a repaint. Sub-epsilon drift still fires eventually because we accumulate the
        // signed delta since the last fire instead of comparing to the last sample.
        private const float DistanceEpsilon = 0.5f;

        /// <summary>The active session's preview state, or <c>null</c> if no session is running.</summary>
        public static PlanetPreviewState Active => PlanetAuthoringSession.Active?.PreviewState;

        /// <summary>
        /// Fires when <see cref="Active" /> changes or any of its tracked values cross <see cref="DistanceEpsilon" />.
        /// </summary>
        /// <remarks>
        /// Subscribers should null-check <see cref="Active" /> in the handler since the event also fires when a session ends.
        /// </remarks>
        public static event Action ActiveChanged;

        internal static void RaiseActiveChanged() => ActiveChanged?.Invoke();

        /// <summary>The body this state belongs to.</summary>
        public CoreCelestialBodyData Body { get; }

        /// <summary>The PQS feeding terrain samples.</summary>
        public PQS Pqs { get; }

        /// <summary>True when the latest sample read a valid terrain elevation from the heightmap.</summary>
        public bool HasTerrainSample { get; private set; }

        /// <summary>Camera-to-terrain-surface distance in meters.</summary>
        public float CameraDistanceFromSurface { get; private set; }

        /// <summary>Terrain elevation in meters above the body's mean radius, at the camera's radial direction.</summary>
        public float TerrainElevationAtCamera { get; private set; }

        private float _lastSampledDistance;
        private float _lastSampledElevation;
        private float _accumulatedDistanceDelta;
        private float _accumulatedElevationDelta;

        /// <summary>
        /// Creates a preview state bound to the given body and PQS.
        /// </summary>
        /// <param name="body">The body the state belongs to.</param>
        /// <param name="pqs">The PQS feeding terrain samples.</param>
        public PlanetPreviewState(CoreCelestialBodyData body, PQS pqs)
        {
            Body = body;
            Pqs = pqs;
        }

        /// <summary>
        /// Resamples camera-relative terrain state.
        /// </summary>
        /// <param name="camera">The camera to sample against.</param>
        /// <returns>True if any tracked value changed enough to be worth notifying subscribers, false otherwise.</returns>
        public bool Update(Camera camera)
        {
            bool newHasSample = false;
            float newDistance = 0f;
            float newElevation = 0f;

            if (camera != null && Body != null && Pqs != null && Pqs.IsRunning())
            {
                Vector3 bodyToCam = camera.transform.position - Body.transform.position;
                float camDistFromCenter = bodyToCam.magnitude;
                float radius = (float)(Body.Core?.data?.radius ?? 0.0);

                if (radius > 0f && camDistFromCenter > 0f)
                {
                    Vector3 radial = bodyToCam / camDistFromCenter;
                    var radialD = new Vector3d(radial.x, radial.y, radial.z);
                    float surfaceFromCenter = (float)Pqs.GetSurfaceHeight(radialD, includeDecals: true);
                    newElevation = surfaceFromCenter - radius;
                    newDistance = Mathf.Max(0f, camDistFromCenter - surfaceFromCenter);
                    newHasSample = true;
                }
            }

            // Track the signed delta since the last fire. Equivalent to comparing against the
            // last-fired value, but explicit about the accumulation so a slow camera drift below
            // the per-tick epsilon still triggers a repaint once the cumulative motion crosses it.
            _accumulatedDistanceDelta += newDistance - _lastSampledDistance;
            _accumulatedElevationDelta += newElevation - _lastSampledElevation;
            _lastSampledDistance = newDistance;
            _lastSampledElevation = newElevation;

            bool changed =
                newHasSample != HasTerrainSample ||
                Mathf.Abs(_accumulatedDistanceDelta) > DistanceEpsilon ||
                Mathf.Abs(_accumulatedElevationDelta) > DistanceEpsilon;

            if (!changed)
                return false;

            HasTerrainSample = newHasSample;
            CameraDistanceFromSurface = newDistance;
            TerrainElevationAtCamera = newElevation;
            _accumulatedDistanceDelta = 0f;
            _accumulatedElevationDelta = 0f;
            return true;
        }

        /// <summary>
        /// Resets all tracked values.
        /// </summary>
        /// <returns>True if anything was non-default before the reset, false otherwise.</returns>
        public bool Clear()
        {
            bool wasNonDefault =
                HasTerrainSample ||
                CameraDistanceFromSurface != 0f ||
                TerrainElevationAtCamera != 0f;

            HasTerrainSample = false;
            CameraDistanceFromSurface = 0f;
            TerrainElevationAtCamera = 0f;
            _lastSampledDistance = 0f;
            _lastSampledElevation = 0f;
            _accumulatedDistanceDelta = 0f;
            _accumulatedElevationDelta = 0f;
            return wasNonDefault;
        }
    }
}
