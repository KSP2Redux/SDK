using System.Collections.Generic;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// SceneView framing convention. Selects which world axis the camera sits on relative to the body.
    /// </summary>
    public enum SceneFramingMode
    {
        /// <summary>
        /// Camera at -Z from body, looking +Z with +Y up. Body lat/lon faces -Z.
        /// </summary>
        Side,

        /// <summary>
        /// Camera at +Y above body, looking +Z with +Y up. Body lat/lon faces +Y (under camera).
        /// </summary>
        Surface,
    }

    /// <summary>
    /// Single source of truth for SceneView framing against a celestial body.
    /// </summary>
    /// <remarks>
    /// The camera frame is fixed (forward=+Z, up=+Y) and the PQS transform rotates to bring the
    /// chosen lat/lon under the camera. All framing math anchors on the PQS transform, since that
    /// is what parents the rendered terrain in the editor authoring scene. The radical rework:
    /// jumps used to move the camera around the body, which left SceneView nav disoriented after
    /// every jump. Now the camera stays in a stable world frame and the PQS (and the artist's sun
    /// light, via SunCoupling) rotates as a rigid block. PlanetAuthoringSession snapshots and
    /// restores PQS and sun rotation so the scene stays clean when preview ends.
    /// </remarks>
    public static class SceneViewFraming
    {
        // PQS instance ID -> last (lat, lon) framed by a lat/lon-bearing call. SessionInitialFraming
        // reads this on session start so re-entering preview returns to the artist's last view
        // instead of snapping back to (0, 0). In-memory only - lost on domain reload, which is acceptable.
        private static readonly Dictionary<int, (double lat, double lon)> LastLatLon = new();

        /// <summary>
        /// Reads the last (lat, lon) framed for this PQS via a lat/lon-bearing call.
        /// </summary>
        /// <param name="pqs">The PQS whose framing record to look up.</param>
        /// <param name="lat">The last latitude in degrees, or 0 if no record exists.</param>
        /// <param name="lon">The last longitude in degrees, or 0 if no record exists.</param>
        /// <returns>True if a record exists for this PQS, false otherwise.</returns>
        public static bool TryGetLastLatLon(PQS pqs, out double lat, out double lon)
        {
            if (pqs != null && LastLatLon.TryGetValue(pqs.GetInstanceID(), out var entry))
            {
                lat = entry.lat;
                lon = entry.lon;
                return true;
            }
            lat = 0;
            lon = 0;
            return false;
        }

        /// <summary>
        /// Frames the SceneView on the given lat/lon at the camera's current altitude.
        /// </summary>
        /// <param name="planet">The body to frame.</param>
        /// <param name="latitudeDegrees">Target latitude in degrees.</param>
        /// <param name="longitudeDegrees">Target longitude in degrees.</param>
        /// <param name="mode">Framing mode controlling camera placement and body rotation.</param>
        public static void FrameAtLatLon(PQS planet, double latitudeDegrees, double longitudeDegrees, SceneFramingMode mode = SceneFramingMode.Side)
        {
            if (!Resolve(planet, out var ctx)) return;
            var localDir = LatLon.GetRelSurfaceNVector(latitudeDegrees, longitudeDegrees);
            var distanceFromCenter = CurrentDistanceFromCenter(ctx, mode);
            ApplyPqsRotation(ctx, localDir, mode);
            PositionCameraAtDistance(ctx, mode, distanceFromCenter);
            LastLatLon[ctx.Pqs.GetInstanceID()] = (latitudeDegrees, longitudeDegrees);
        }

        /// <summary>
        /// Frames the SceneView on the given lat/lon at the requested altitude above the surface.
        /// </summary>
        /// <param name="planet">The body to frame.</param>
        /// <param name="latitudeDegrees">Target latitude in degrees.</param>
        /// <param name="longitudeDegrees">Target longitude in degrees.</param>
        /// <param name="altitudeAboveSurfaceMeters">Altitude above the sampled surface, in meters.</param>
        /// <param name="mode">Framing mode controlling camera placement and body rotation.</param>
        public static void FrameAtLatLonAndAltitude(PQS planet, double latitudeDegrees, double longitudeDegrees, double altitudeAboveSurfaceMeters, SceneFramingMode mode = SceneFramingMode.Side)
        {
            if (!Resolve(planet, out var ctx)) return;
            var localDir = LatLon.GetRelSurfaceNVector(latitudeDegrees, longitudeDegrees);
            ApplyPqsRotation(ctx, localDir, mode);
            PositionCameraAtAltitude(ctx, mode, localDir, altitudeAboveSurfaceMeters);
            LastLatLon[ctx.Pqs.GetInstanceID()] = (latitudeDegrees, longitudeDegrees);
        }

        /// <summary>
        /// Frames the SceneView on the given body-local position, treating it as a direction from body center.
        /// </summary>
        /// <param name="planet">The body to frame.</param>
        /// <param name="bodyLocalPosition">Direction in body-local space. Normalized internally.</param>
        /// <param name="mode">Framing mode controlling camera placement and body rotation.</param>
        public static void FrameAtBodyLocalPosition(PQS planet, Vector3d bodyLocalPosition, SceneFramingMode mode = SceneFramingMode.Side)
        {
            if (!Resolve(planet, out var ctx)) return;
            if (bodyLocalPosition.sqrMagnitude < 1e-6) return;
            var localDir = bodyLocalPosition.normalized;
            var distanceFromCenter = CurrentDistanceFromCenter(ctx, mode);
            ApplyPqsRotation(ctx, localDir, mode);
            PositionCameraAtDistance(ctx, mode, distanceFromCenter);
        }

        /// <summary>
        /// Jumps to the requested altitude while keeping the lat/lon currently faced by the camera.
        /// </summary>
        /// <remarks>
        /// Used by Preview Controls' jump-to-altitude buttons. Detecting "what is being looked at"
        /// from the SceneView camera position means orbit gestures aren't blown away by a jump, and
        /// mode switches (Side -> Surface, etc.) rotate the body so the same point stays under the
        /// camera.
        /// </remarks>
        /// <param name="planet">The body to frame.</param>
        /// <param name="altitudeAboveSurfaceMeters">Target altitude above the sampled surface, in meters.</param>
        /// <param name="mode">Framing mode controlling camera placement and body rotation.</param>
        public static void FrameAtAltitude(PQS planet, double altitudeAboveSurfaceMeters, SceneFramingMode mode = SceneFramingMode.Side)
        {
            if (!Resolve(planet, out var ctx)) return;
            var fromPqsToCam = ctx.Sv.camera.transform.position - ctx.Pqs.transform.position;
            // Camera-at-PQS-center degenerates the lookup. Fall back to PQS-local +Z so the call
            // becomes a no-op rotation instead of corrupting orientation with an arbitrary axis.
            var localFocusDir = fromPqsToCam.sqrMagnitude > 1e-6f
                ? (Vector3d)(Quaternion.Inverse(ctx.Pqs.transform.rotation) * fromPqsToCam.normalized)
                : (Vector3d)Vector3.forward;
            ApplyPqsRotation(ctx, localFocusDir, mode);
            PositionCameraAtAltitude(ctx, mode, localFocusDir, altitudeAboveSurfaceMeters);
        }

        /// <summary>
        /// Rotates the body so the world-space direction <paramref name="worldOutwardToFaceCamera" /> points toward the camera.
        /// </summary>
        /// <remarks>
        /// Used by Preview Controls' Day/Night buttons.
        /// </remarks>
        /// <param name="planet">The body to frame.</param>
        /// <param name="worldOutwardToFaceCamera">World-space outward direction to bring under the camera.</param>
        /// <param name="mode">Framing mode controlling camera placement and body rotation.</param>
        public static void FrameAtDirection(PQS planet, Vector3 worldOutwardToFaceCamera, SceneFramingMode mode = SceneFramingMode.Side)
        {
            if (!Resolve(planet, out var ctx)) return;
            if (worldOutwardToFaceCamera.sqrMagnitude < 1e-6f) return;
            worldOutwardToFaceCamera.Normalize();
            Vector3d localDir = Quaternion.Inverse(ctx.Pqs.transform.rotation) * worldOutwardToFaceCamera;
            var distanceFromCenter = CurrentDistanceFromCenter(ctx, mode);
            ApplyPqsRotation(ctx, localDir, mode);
            PositionCameraAtDistance(ctx, mode, distanceFromCenter);
        }

        // ----- internals ----------------------------------------------------

        private struct FramingContext
        {
            public SceneView Sv;
            public PQS Pqs;
            public double Radius;
        }

        private static bool Resolve(PQS planet, out FramingContext ctx)
        {
            ctx = default;
            if (planet == null) return false;
            // Radius is the only datum we need from CoreCelestialBodyData. Everything else (position,
            // rotation, framing math) anchors on the PQS transform, since that is what parents the
            // rendered terrain in the editor authoring scene.
            var radius = planet.CoreCelestialBodyData?.Data?.radius ?? 0;
            if (radius <= 0) return false;
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return false;
            ctx = new FramingContext { Sv = sv, Pqs = planet, Radius = radius };
            return true;
        }

        private static (Vector3 forward, Vector3 up) WorldFrameFor(SceneFramingMode mode) => mode switch
        {
            // Surface: camera at +Y above body. Lat/lon faces +Y, north tangent projects to +Z so
            // forward (camera-forward = +Z world) walks north along the body.
            SceneFramingMode.Surface => (Vector3.up, Vector3.forward),
            // Side: camera at -Z from body. Lat/lon faces -Z, north tangent projects to +Y (screen up).
            _ => (-Vector3.forward, Vector3.up),
        };

        // Direction from body center to camera (the outward axis the camera sits on).
        private static Vector3 CameraOffsetDirectionFor(SceneFramingMode mode) => mode switch
        {
            SceneFramingMode.Surface => Vector3.up,
            _ => -Vector3.forward,
        };

        private static void ApplyPqsRotation(in FramingContext ctx, Vector3d localDir, SceneFramingMode mode)
        {
            // PQS rotation is transient framing state, not authoring intent. Skipping Undo.RecordObject
            // here keeps jumps out of the artist's undo stack so Ctrl+Z can't replay framing rotations
            // after the session ends.
            var (targetWorldDir, worldUpHint) = WorldFrameFor(mode);
            var newRot = ComputePqsRotation((Vector3)localDir.normalized, targetWorldDir, worldUpHint);
            var oldRot = ctx.Pqs.transform.rotation;
            var delta = newRot * Quaternion.Inverse(oldRot);
            ctx.Pqs.transform.rotation = newRot;
            SunCoupling.ApplyRotationDelta(delta);
        }

        // Builds a rotation that aligns PQS-local localDir with targetWorldDir AND projects PQS-
        // local +Y (north pole axis in LatLon convention) onto worldUpHint. Quaternion.LookRotation
        // composition handles the orthogonal-basis math. The inverse takes the local "look" to
        // identity, then the world LookRotation re-aims it at the desired world axes.
        private static Quaternion ComputePqsRotation(Vector3 localDir, Vector3 targetWorldDir, Vector3 worldUpHint)
        {
            var localNorth = Vector3.up;
            var localNorthTangent = localNorth - Vector3.Dot(localNorth, localDir) * localDir;
            if (localNorthTangent.sqrMagnitude < 1e-6f)
            {
                // At a pole - pick any tangent so LookRotation has a valid second axis.
                localNorthTangent = Vector3.Cross(localDir, Vector3.right);
                if (localNorthTangent.sqrMagnitude < 1e-6f)
                    localNorthTangent = Vector3.Cross(localDir, Vector3.forward);
            }
            localNorthTangent.Normalize();

            var worldLook = Quaternion.LookRotation(targetWorldDir, worldUpHint);
            var localLook = Quaternion.LookRotation(localDir, localNorthTangent);
            return worldLook * Quaternion.Inverse(localLook);
        }

        // Camera distance from PQS center along the current outward axis. Preserved across framing
        // calls so repeated Look At clicks on the same lat/lon are a no-op instead of drifting.
        private static double CurrentDistanceFromCenter(in FramingContext ctx, SceneFramingMode mode)
        {
            var outward = CameraOffsetDirectionFor(mode);
            double currentDistFromCenter = Vector3.Dot(ctx.Sv.camera.transform.position - ctx.Pqs.transform.position, outward);
            // Camera inside the planet (or on the wrong side of the outward axis) - kick it out to a
            // safe default so the next placement isn't below the surface.
            if (currentDistFromCenter <= ctx.Radius)
                currentDistFromCenter = ctx.Radius * 1.5;
            return currentDistFromCenter;
        }

        // Places the camera at a precise distance from the PQS center along the mode's outward axis.
        // Used by callers that want to preserve the artist's current zoom across a Look At / mode
        // switch without going through an altitude-above-terrain intermediate.
        private static void PositionCameraAtDistance(in FramingContext ctx, SceneFramingMode mode, double distanceFromCenter)
        {
            ApplyCameraTransform(ctx, mode, distanceFromCenter);
        }

        // Places the camera at terrainDist(lat,lon) + altitude. Used by jump-to-altitude buttons
        // where the artist asks for "10 m above the surface" and expects to clear raised decals
        // (KSC pad) and mountains rather than 10 m above the mean-radius sphere.
        private static void PositionCameraAtAltitude(in FramingContext ctx, SceneFramingMode mode, Vector3d localFacingDir, double altitudeAboveSurface)
        {
            var terrainDist = ctx.Pqs.GetSurfaceHeight(localFacingDir, true);
            if (terrainDist <= 0) terrainDist = ctx.Radius;
            ApplyCameraTransform(ctx, mode, terrainDist + altitudeAboveSurface);
        }

        private static void ApplyCameraTransform(in FramingContext ctx, SceneFramingMode mode, double distanceFromCenter)
        {
            // Camera rotation is always forward=+Z, up=+Y (Unity world). Position depends on mode:
            //   Side    -> pqs + (-Z) * distanceFromCenter. Body fills the screen in front.
            //   Surface -> pqs + (+Y) * distanceFromCenter. Body sits below, horizon extends in +Z.
            var camFwdWorld = Vector3.forward; // +Z
            var camUpWorld = Vector3.up; // +Y
            var cameraOffsetDir = CameraOffsetDirectionFor(mode);
            var cameraPos = ctx.Pqs.transform.position + cameraOffsetDir * (float)distanceFromCenter;
            // SceneView orbits around its pivot; placing the pivot at the mean-radius sphere surface
            // means orbit gestures spin you around the planet rather than around a point in mid-air.
            var pivotAhead = (float)System.Math.Max(distanceFromCenter - ctx.Radius, 1.0);
            var pivot = cameraPos + camFwdWorld * pivotAhead;

            var rotation = Quaternion.LookRotation(camFwdWorld, camUpWorld);
            var halfFov = ctx.Sv.camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            var size = pivotAhead * Mathf.Sin(halfFov);
            ctx.Sv.LookAt(pivot, rotation, size, ctx.Sv.orthographic, instant: true);
            ApplyClipPlanes(ctx.Sv, ctx.Radius, pivotAhead);
        }

        private static void ApplyClipPlanes(SceneView sv, double radius, double pivotAhead)
        {
            sv.cameraSettings.dynamicClip = false;
            sv.cameraSettings.nearClip = Mathf.Max(0.05f, (float)(pivotAhead * 0.1));
            sv.cameraSettings.farClip = (float)((radius + pivotAhead) * 4.0);
        }
    }
}
