using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Atmosphere
{
    /// <summary>
    /// Resolves the shared geodesic sphere the outer atmosphere shell draws with.
    /// </summary>
    /// <remarks>
    /// Stock shells use a frequency 40 geosphere. Unity's builtin sphere has too few faces at atmosphere scale, and
    /// its facets show along the limb. Created at <see cref="ASSET_PATH" /> on first use if missing.
    /// </remarks>
    public static class AtmosphereShellMesh
    {
        /// <summary>
        /// The subdivision frequency of the shell, matching stock's Geosphere40.
        /// </summary>
        public const int FREQUENCY = 40;

        /// <summary>
        /// The asset path of the shared shell mesh.
        /// </summary>
        public const string ASSET_PATH = SDKConfiguration.AUTHORING_ASSETS_PATH + "/Meshes/AtmosphereGeosphere40.asset";

        private static readonly int[] IcosahedronFaces =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private static Mesh _cached;

        /// <summary>
        /// Returns the shared shell mesh, creating it on disk if absent.
        /// </summary>
        /// <returns>The shell mesh asset.</returns>
        public static Mesh Get()
        {
            if (_cached != null)
                return _cached;

            _cached = AssetDatabase.LoadAssetAtPath<Mesh>(ASSET_PATH);
            if (_cached != null)
                return _cached;

            string directory = Path.GetDirectoryName(ASSET_PATH);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _cached = Build(FREQUENCY);
            AssetDatabase.CreateAsset(_cached, ASSET_PATH);
            AssetDatabase.SaveAssets();
            return _cached;
        }

        /// <summary>
        /// Builds a closed unit geodesic sphere by splitting each icosahedron face into a triangular grid.
        /// </summary>
        /// <remarks>
        /// Vertices on shared edges are welded, giving 10 * frequency^2 + 2 vertices and 20 * frequency^2 outward
        /// facing triangles. Normals are the vertex directions.
        /// </remarks>
        /// <param name="frequency">The number of segments each icosahedron edge is split into.</param>
        /// <returns>The new mesh, not saved as an asset.</returns>
        public static Mesh Build(int frequency)
        {
            Vector3d[] corners = IcosahedronCorners();
            var vertices = new List<Vector3>();
            var lookup = new Dictionary<Vector3, int>();
            var triangles = new List<int>(IcosahedronFaces.Length * frequency * frequency);
            var faceGrid = new int[frequency + 1, frequency + 1];

            for (int face = 0; face < IcosahedronFaces.Length; face += 3)
            {
                Vector3d a = corners[IcosahedronFaces[face]];
                Vector3d b = corners[IcosahedronFaces[face + 1]];
                Vector3d c = corners[IcosahedronFaces[face + 2]];

                for (int i = 0; i <= frequency; i++)
                {
                    for (int j = 0; j <= frequency - i; j++)
                    {
                        // Weighted sums rather than stepped edges, so a point on an edge two faces share comes out
                        // bit-identical from both and welds by exact lookup.
                        Vector3d point = a * (frequency - i - j) + b * i + c * j;
                        var direction = (Vector3)point.normalized;
                        if (!lookup.TryGetValue(direction, out int index))
                        {
                            index = vertices.Count;
                            vertices.Add(direction);
                            lookup.Add(direction, index);
                        }

                        faceGrid[i, j] = index;
                    }
                }

                for (int i = 0; i < frequency; i++)
                {
                    for (int j = 0; j < frequency - i; j++)
                    {
                        AddOutwardTriangle(triangles, vertices, faceGrid[i, j], faceGrid[i + 1, j], faceGrid[i, j + 1]);
                        if (j < frequency - i - 1)
                        {
                            AddOutwardTriangle(triangles, vertices, faceGrid[i + 1, j], faceGrid[i + 1, j + 1], faceGrid[i, j + 1]);
                        }
                    }
                }
            }

            var mesh = new Mesh { name = $"AtmosphereGeosphere{frequency}" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(vertices);
            mesh.SetTriangles(triangles, 0);

            // The shell is scaled by its bounds' largest extent, and no vertex lands exactly on an axis, so the
            // bounds are pinned to the unit sphere they approximate.
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return mesh;
        }

        // Unity's front face winds clockwise seen from outside, which puts the edge cross product along the
        // outward direction.
        private static void AddOutwardTriangle(List<int> triangles, List<Vector3> vertices, int first, int second, int third)
        {
            Vector3 a = vertices[first];
            Vector3 b = vertices[second];
            Vector3 c = vertices[third];
            bool isOutward = Vector3.Dot(Vector3.Cross(b - a, c - a), a + b + c) > 0f;
            triangles.Add(first);
            triangles.Add(isOutward ? second : third);
            triangles.Add(isOutward ? third : second);
        }

        private static Vector3d[] IcosahedronCorners()
        {
            double t = (1.0 + System.Math.Sqrt(5.0)) / 2.0;
            Vector3d[] corners =
            {
                new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0),
                new(0, -1, t), new(0, 1, t), new(0, -1, -t), new(0, 1, -t),
                new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = corners[i].normalized;
            }

            return corners;
        }
    }
}
