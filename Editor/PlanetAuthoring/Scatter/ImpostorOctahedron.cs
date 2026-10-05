using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// The octahedral frame layout of a scatter impostor atlas, as pure functions.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>Octahedron_Impostor.cginc</c> term for term, so a frame baked from the direction
    /// and basis given here is the frame the shader samples for that view. The octahedral plane is
    /// object X and Z with Y as the pole, which keeps a scatter instance's surface up at the pole.
    /// </remarks>
    public static class ImpostorOctahedron
    {
        /// <summary>
        /// Offset the shader adds to a frame tangent so the basis stays defined at the poles.
        /// </summary>
        public const float PoleTangentBias = -0.001f;

        /// <summary>
        /// Folds a unit direction onto the octahedral square.
        /// </summary>
        /// <param name="direction">The direction to encode. Need not be normalised.</param>
        /// <returns>The octahedral coordinate, each component in [-1, 1].</returns>
        public static Vector2 Encode(Vector3 direction)
        {
            direction /= Mathf.Abs(direction.x) + Mathf.Abs(direction.y) + Mathf.Abs(direction.z);
            var octahedron = new Vector2(direction.x, direction.z);
            return direction.y <= 0f ? Fold(octahedron) : octahedron;
        }

        /// <summary>
        /// Unfolds an octahedral coordinate into a unit direction.
        /// </summary>
        /// <param name="octahedron">The octahedral coordinate, each component in [-1, 1].</param>
        /// <returns>The unit direction.</returns>
        public static Vector3 Decode(Vector2 octahedron)
        {
            float pole = 1f - Mathf.Abs(octahedron.x) - Mathf.Abs(octahedron.y);
            if (pole < 0f)
                octahedron = Fold(octahedron);

            return new Vector3(octahedron.x, pole, octahedron.y).normalized;
        }

        /// <summary>
        /// Returns the direction, from the object toward the camera, that one atlas frame was baked from.
        /// </summary>
        /// <param name="cellX">The frame column, counted from the left.</param>
        /// <param name="cellY">The frame row, counted from the bottom.</param>
        /// <param name="frames">The number of frames along each side of the atlas.</param>
        /// <returns>The unit view direction of the frame.</returns>
        public static Vector3 FrameDirection(int cellX, int cellY, int frames)
        {
            float scale = 2f / (frames - 1f);
            return Decode(new Vector2(cellX * scale - 1f, cellY * scale - 1f));
        }

        /// <summary>
        /// Returns the continuous grid position of a view direction, whose floor is the nearest lower frame.
        /// </summary>
        /// <param name="direction">The view direction, from the object toward the camera.</param>
        /// <param name="frames">The number of frames along each side of the atlas.</param>
        /// <returns>The grid position, each component in [0, frames - 1].</returns>
        public static Vector2 GridPosition(Vector3 direction, int frames)
        {
            Vector2 octahedron = Encode(direction);
            return (frames - 1f) * new Vector2(octahedron.x * 0.5f + 0.5f, octahedron.y * 0.5f + 0.5f);
        }

        /// <summary>
        /// Returns the screen axes a frame is baked along.
        /// </summary>
        /// <remarks>
        /// The shader maps a point to frame UV by its negated dot products with the frame tangent and
        /// bitangent, so the bake camera's right and up are those two vectors negated. The pole bias
        /// tilts the tangent slightly out of the frame plane. The shader only ever dots it with points
        /// on that plane, so the camera takes the in-plane part, which keeps the basis orthonormal.
        /// </remarks>
        /// <param name="frameDirection">The frame's view direction, from <see cref="FrameDirection" />.</param>
        /// <param name="right">The camera right vector of the frame.</param>
        /// <param name="up">The camera up vector of the frame.</param>
        public static void FrameAxes(Vector3 frameDirection, out Vector3 right, out Vector3 up)
        {
            Vector3 tangent = (Vector3.Cross(Vector3.up, frameDirection) + new Vector3(PoleTangentBias, 0f, 0f)).normalized;
            Vector3 bitangent = Vector3.Cross(tangent, frameDirection);
            right = -Vector3.ProjectOnPlane(tangent, frameDirection).normalized;
            up = -bitangent.normalized;
        }

        private static Vector2 Fold(Vector2 octahedron) =>
            new Vector2(
                (1f - Mathf.Abs(octahedron.y)) * (octahedron.x >= 0f ? 1f : -1f),
                (1f - Mathf.Abs(octahedron.x)) * (octahedron.y >= 0f ? 1f : -1f));
    }
}
