using KSP.Rendering.Planets;
using KSP.Tools.PQSFreeCamUtils;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Scene-view handles for surface-attached transforms (<see cref="PrefabSpawner" />,
    /// <see cref="SurfaceLandmark" />, and friends).
    /// </summary>
    /// <remarks>
    /// The position handle re-samples the body surface under the mouse on drag and snaps the
    /// transform back onto the surface at the dropped lat/lon, preserving a caller-supplied
    /// altitude offset. The yaw handle rotates around the live surface normal. Both keep
    /// <c>transform.up</c> aligned to the surface so the prefab / decal continues to face out.
    /// </remarks>
    internal static class SurfaceTransformHandles
    {
        /// <summary>
        /// Draws a free-move sphere handle that, on drag, ray-casts the body surface and re-positions
        /// <paramref name="t" /> at the dropped lat/lon. Altitude (above terrain) is preserved.
        /// </summary>
        /// <param name="t">The transform to move.</param>
        /// <param name="pqs">The body whose surface is sampled.</param>
        /// <param name="altitude">Altitude in meters above the terrain to maintain.</param>
        /// <param name="undoLabel">Undo group label.</param>
        /// <returns>True when the user dragged and a valid surface hit was applied.</returns>
        public static bool DrawSurfaceMoveHandle(Transform t, PQS pqs, float altitude, string undoLabel, out Vector2 newLatLon)
        {
            newLatLon = default;
            if (t == null || pqs == null) return false;
            var refSize = HandleUtility.GetHandleSize(t.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            _ = Handles.FreeMoveHandle(t.position, refSize, Vector3.zero, Handles.SphereHandleCap);
            if (!EditorGUI.EndChangeCheck()) return false;

            // PlanetSurfaceHit.TryHit already refines against the displaced terrain and returns the
            // world-space surface point and lat/lon, so we use both directly instead of paying for
            // a second GetSurfaceHeight pass to recompute the radius at the picked lat/lon. The
            // altitude offset is applied along the outward normal from the PQS center.
            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (!PlanetSurfaceHit.TryHit(pqs, ray, out var hitWorld, out var hitLatLon, out _, includeDecals: true)) return false;

            Undo.RecordObject(t, undoLabel);
            var surfaceUpWorld = (hitWorld - pqs.transform.position).normalized;
            t.position = hitWorld + surfaceUpWorld * altitude;
            // Preserve the existing yaw by composing the up-axis correction onto the current
            // rotation. Quaternion.FromToRotation(oldUp, newUp) is the smallest rotation that maps
            // one to the other, so applying it left-multiplied to the existing rotation tilts the
            // transform to face out while the yaw stays intact.
            t.rotation = Quaternion.FromToRotation(t.up, surfaceUpWorld) * t.rotation;
            newLatLon = hitLatLon;
            return true;
        }

        /// <summary>
        /// Draws a disc handle that rotates <paramref name="t" /> around its current surface normal.
        /// </summary>
        /// <param name="t">The transform to rotate.</param>
        /// <param name="undoLabel">Undo group label.</param>
        /// <returns>True when the user rotated the handle.</returns>
        public static bool DrawSurfaceYawHandle(Transform t, string undoLabel)
        {
            if (t == null) return false;
            var surfaceUp = t.up;
            var refSize = HandleUtility.GetHandleSize(t.position) * 0.06f;
            EditorGUI.BeginChangeCheck();
            var startRot = t.rotation;
            var newRot = Handles.Disc(startRot, t.position, surfaceUp, refSize * 6f, false, 5f);
            if (!EditorGUI.EndChangeCheck()) return false;

            var oldDir = startRot * Vector3.forward;
            var newDir = newRot * Vector3.forward;
            var deltaDeg = Vector3.SignedAngle(oldDir, newDir, surfaceUp);
            Undo.RecordObject(t, undoLabel);
            t.rotation = Quaternion.AngleAxis(deltaDeg, surfaceUp) * t.rotation;
            return true;
        }

    }
}
