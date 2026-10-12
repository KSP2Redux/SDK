using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Builds the trimmed card a scatter impostor is drawn on.
    /// </summary>
    /// <remarks>
    /// The impostor shader places each vertex from its UV alone, so the card is a polygon in frame UV
    /// space. A tight polygon around the silhouette every frame shares saves the fill cost of the
    /// transparent corners of a full quad, which adds up across a field of billboards. Stock trims to
    /// eight vertices.
    /// </remarks>
    public static class ImpostorOutline
    {
        /// <summary>
        /// Returns a convex polygon, clockwise in UV space, enclosing every covered texel of a mask.
        /// </summary>
        /// <remarks>
        /// Clockwise with V up is the winding Unity treats as front facing once the shader lays the
        /// card out along the camera's right and up.
        /// </remarks>
        /// <param name="coverage">The union of every frame's coverage, row major from the bottom row.</param>
        /// <param name="size">The width and height of the mask in texels.</param>
        /// <param name="maxVertices">The most vertices the polygon may have. At least 3.</param>
        /// <returns>The polygon's vertices in frame UV space, or an empty array when nothing is covered.</returns>
        public static Vector2[] Enclose(bool[] coverage, int size, int maxVertices)
        {
            var corners = new List<Vector2>();
            for (int row = 0; row < size; row++)
            {
                int first = -1;
                int last = -1;
                for (int column = 0; column < size; column++)
                {
                    if (!coverage[row * size + column])
                        continue;

                    if (first < 0)
                        first = column;
                    last = column;
                }

                if (first < 0)
                    continue;

                // The outer corners of the row's end texels, so the polygon covers whole texels.
                corners.Add(new Vector2(first, row) / size);
                corners.Add(new Vector2(first, row + 1) / size);
                corners.Add(new Vector2(last + 1, row) / size);
                corners.Add(new Vector2(last + 1, row + 1) / size);
            }

            if (corners.Count == 0)
                return new Vector2[0];

            List<Vector2> polygon = ConvexHull(corners);
            Reduce(polygon, Mathf.Max(3, maxVertices));
            for (int i = 0; i < polygon.Count; i++)
            {
                polygon[i] = new Vector2(Mathf.Clamp01(polygon[i].x), Mathf.Clamp01(polygon[i].y));
            }

            // Clamping can pull neighbours onto one point or line, so the hull is taken again.
            polygon = ConvexHull(polygon);
            polygon.Reverse();
            return polygon.ToArray();
        }

        /// <summary>
        /// Builds the card mesh for a polygon from <see cref="Enclose" />.
        /// </summary>
        /// <param name="outline">The polygon's vertices in frame UV space, clockwise.</param>
        /// <param name="impostorSize">The world size one frame spans.</param>
        /// <param name="bounds">The bounds of the object the impostor stands in for.</param>
        /// <returns>The card mesh, a fan over the polygon.</returns>
        public static Mesh BuildMesh(Vector2[] outline, float impostorSize, Bounds bounds)
        {
            var vertices = new Vector3[outline.Length];
            for (int i = 0; i < outline.Length; i++)
            {
                vertices[i] = new Vector3((outline[i].x - 0.5f) * impostorSize, (outline[i].y - 0.5f) * impostorSize, 0f);
            }

            var triangles = new int[(outline.Length - 2) * 3];
            for (int i = 0; i < outline.Length - 2; i++)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }

            var mesh = new Mesh
            {
                vertices = vertices,
                uv = outline,
                triangles = triangles,
            };
            mesh.RecalculateNormals();

            // The shader moves every vertex, so the bounds are the object's rather than the card's.
            mesh.bounds = bounds;
            return mesh;
        }

        /// <summary>
        /// Returns the convex hull of a point set, counter-clockwise, by Andrew's monotone chain.
        /// </summary>
        /// <param name="points">The points to enclose.</param>
        /// <returns>The hull vertices, counter-clockwise, with no repeated or collinear points.</returns>
        public static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var hull = new List<Vector2>();
            foreach (Vector2 point in sorted)
            {
                while (hull.Count >= 2 && Turn(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(point);
            }

            int lowerCount = hull.Count + 1;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                while (hull.Count >= lowerCount && Turn(hull[hull.Count - 2], hull[hull.Count - 1], sorted[i]) <= 0f)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(sorted[i]);
            }

            // The last point closes the loop back onto the first.
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        /// <summary>
        /// Cuts a counter-clockwise convex polygon down to a vertex budget without shrinking it.
        /// </summary>
        /// <remarks>
        /// Each step drops one edge by extending its two neighbouring edges until they meet, which
        /// grows the polygon by the triangle between the dropped edge and that meeting point. The edge
        /// whose triangle is smallest goes first. An edge whose neighbours diverge cannot be dropped.
        /// </remarks>
        /// <param name="polygon">The polygon, counter-clockwise. Modified in place.</param>
        /// <param name="maxVertices">The vertex budget.</param>
        public static void Reduce(List<Vector2> polygon, int maxVertices)
        {
            while (polygon.Count > maxVertices)
            {
                int bestEdge = -1;
                float bestArea = float.MaxValue;
                Vector2 bestPoint = default;
                for (int i = 0; i < polygon.Count; i++)
                {
                    if (!TryExtendEdges(polygon, i, out Vector2 meeting))
                        continue;

                    Vector2 start = polygon[i];
                    Vector2 end = polygon[(i + 1) % polygon.Count];
                    float area = Mathf.Abs(Turn(start, meeting, end)) * 0.5f;
                    if (area >= bestArea)
                        continue;

                    bestArea = area;
                    bestEdge = i;
                    bestPoint = meeting;
                }

                if (bestEdge < 0)
                    return;

                int endIndex = (bestEdge + 1) % polygon.Count;
                polygon[bestEdge] = bestPoint;
                polygon.RemoveAt(endIndex);
            }
        }

        // Where the edges either side of edge i meet, if they meet beyond it.
        private static bool TryExtendEdges(List<Vector2> polygon, int edge, out Vector2 meeting)
        {
            int count = polygon.Count;
            Vector2 before = polygon[(edge - 1 + count) % count];
            Vector2 start = polygon[edge];
            Vector2 end = polygon[(edge + 1) % count];
            Vector2 after = polygon[(edge + 2) % count];

            Vector2 incoming = start - before;
            Vector2 outgoing = end - after;
            float denominator = Cross(incoming, outgoing);
            meeting = default;
            if (Mathf.Abs(denominator) < 1e-9f)
                return false;

            // start + incoming * t meets end + outgoing * s, and both must run forward past the edge.
            Vector2 gap = end - start;
            float t = Cross(gap, outgoing) / denominator;
            float s = Cross(gap, incoming) / denominator;
            if (t <= 0f || s <= 0f)
                return false;

            meeting = start + incoming * t;
            return true;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        // Positive when a, b, c turn counter-clockwise.
        private static float Turn(Vector2 a, Vector2 b, Vector2 c) => Cross(b - a, c - a);
    }
}
