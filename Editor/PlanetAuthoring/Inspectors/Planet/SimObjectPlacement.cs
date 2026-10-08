using System;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors.Planet
{
    /// <summary>
    /// Converts a sim object placed on a body between its stored position and rotation and the latitude, longitude,
    /// altitude and heading an author edits.
    /// </summary>
    /// <remarks>
    /// The position is in the body's Body frame, whose axes are those of <see cref="LatLon.GetRelSurfaceNVector" />.
    /// Altitude is above sea level, the body radius. An upright object has its up along the surface normal, as stock's
    /// KSC does, and its heading is the compass direction of its forward: 0 north, 90 east.
    /// </remarks>
    public static class SimObjectPlacement
    {
        // An object whose up is within this of the surface normal counts as upright.
        private const float UPRIGHT_DOT = 0.9999f;

        /// <summary>
        /// Gets the position for a latitude, longitude and altitude.
        /// </summary>
        /// <param name="latitude">The latitude in degrees.</param>
        /// <param name="longitude">The longitude in degrees.</param>
        /// <param name="altitude">The altitude above sea level in meters.</param>
        /// <param name="radius">The body radius in meters.</param>
        /// <returns>The position in the body's frame, in meters.</returns>
        public static Vector3d ToPosition(double latitude, double longitude, double altitude, double radius) =>
            LatLon.GetRelSurfaceNVector(latitude, longitude) * (radius + altitude);

        /// <summary>
        /// Gets the latitude and longitude of a position, in degrees.
        /// </summary>
        /// <param name="position">The position in the body's frame.</param>
        /// <returns>The latitude and longitude in degrees, zero at the body's center.</returns>
        public static (double Latitude, double Longitude) ToLatLon(Vector3d position)
        {
            double length = position.magnitude;
            if (length <= 0.0)
                return (0.0, 0.0);

            double latitude = Math.Asin(Math.Max(-1.0, Math.Min(1.0, position.y / length))) * 180.0 / Math.PI;
            double longitude = Math.Atan2(position.z, position.x) * 180.0 / Math.PI;
            return (latitude, longitude);
        }

        /// <summary>
        /// Gets the altitude of a position above sea level.
        /// </summary>
        /// <param name="position">The position in the body's frame.</param>
        /// <param name="radius">The body radius in meters.</param>
        /// <returns>The altitude in meters.</returns>
        public static double ToAltitude(Vector3d position, double radius) => position.magnitude - radius;

        /// <summary>
        /// Gets the rotation of an upright object at a point, facing a heading.
        /// </summary>
        /// <param name="position">The object's position in the body's frame.</param>
        /// <param name="heading">The heading in degrees: 0 north, 90 east.</param>
        /// <returns>The rotation in the body's frame.</returns>
        public static Quaternion ToRotation(Vector3d position, double heading)
        {
            (Vector3 up, Vector3 north, Vector3 east) = SurfaceFrame(position);
            double radians = heading * Math.PI / 180.0;
            Vector3 forward = (float)Math.Cos(radians) * north + (float)Math.Sin(radians) * east;
            return Quaternion.LookRotation(forward, up);
        }

        /// <summary>
        /// Gets whether a rotation stands an object upright on the surface at a point.
        /// </summary>
        /// <param name="position">The object's position in the body's frame.</param>
        /// <param name="rotation">The object's rotation in the body's frame.</param>
        /// <returns>True if the object's up is along the surface normal, false otherwise.</returns>
        public static bool IsUpright(Vector3d position, Quaternion rotation) =>
            Vector3.Dot(rotation * Vector3.up, SurfaceFrame(position).Up) >= UPRIGHT_DOT;

        /// <summary>
        /// Gets the heading of an object's forward at a point.
        /// </summary>
        /// <param name="position">The object's position in the body's frame.</param>
        /// <param name="rotation">The object's rotation in the body's frame.</param>
        /// <returns>The heading in degrees, from 0 to 360: 0 north, 90 east.</returns>
        public static double ToHeading(Vector3d position, Quaternion rotation)
        {
            (_, Vector3 north, Vector3 east) = SurfaceFrame(position);
            Vector3 forward = rotation * Vector3.forward;
            double heading = Math.Atan2(Vector3.Dot(forward, east), Vector3.Dot(forward, north)) * 180.0 / Math.PI;
            return heading < 0.0 ? heading + 360.0 : heading;
        }

        // Up is the surface normal, north points along the surface toward the +Y pole, and east completes them. At a
        // pole, where north has no direction, +Z stands in for it.
        private static (Vector3 Up, Vector3 North, Vector3 East) SurfaceFrame(Vector3d position)
        {
            Vector3 up = ((Vector3)position).normalized;
            if (up == Vector3.zero)
            {
                up = Vector3.up;
            }

            Vector3 north = Vector3.up - up * Vector3.Dot(Vector3.up, up);
            north = north.sqrMagnitude < 1e-10f ? Vector3.forward : north.normalized;
            Vector3 east = Vector3.Cross(up, north);
            return (up, north, east);
        }
    }
}
