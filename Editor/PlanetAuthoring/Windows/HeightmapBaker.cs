using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Windows
{
    /// <summary>
    /// Editor window that bakes an equirectangular R16 heightmap by raycasting a source mesh from origin outward.
    /// </summary>
    /// <remarks>
    /// BVH over the mesh's triangles + a Burst IJobParallelFor that fires one double-precision ray per
    /// output pixel in equirectangular projection. Hit distances normalize to [0, 1] across the
    /// [min, max] range and write as an R16 PNG. The reported midlevel and strength match Blender's
    /// Displace modifier fields on a unit sphere, so the same numbers reconstruct the original mesh.
    ///
    /// Source mesh needs Read/Write enabled. Last-bake values persist per-mesh in EditorPrefs.
    /// </remarks>
    public class HeightmapBaker : EditorWindow
    {
        private const string UxmlPath = "/Assets/Windows/HeightmapBaker.uxml";
        private const string PrefsPrefix = "Ksp2UnityTools.HeightmapBaker.";
        private const string Title = "Heightmap Baker";

        /// <summary>
        /// Opens or focuses the Heightmap Baker editor window.
        /// </summary>
        [MenuItem(PlanetAuthoringWindows.MenuRoot + "Heightmap Baker")]
        public static void ShowWindow()
        {
            var window = GetWindow<HeightmapBaker>();
            window.titleContent = new GUIContent(Title);
        }

        private ObjectField _mesh;
        private DropdownField _resolution;
        private Button _bake;
        private TextField _path;
        private DoubleField _min;
        private DoubleField _strength;
        private VisualElement _warningSlot;

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SDKConfiguration.BasePath + UxmlPath);
            if (tree == null)
            {
                root.Add(new Label("Failed to load HeightmapBaker.uxml"));
                return;
            }

            tree.CloneTree(root);
            Ksp2UnityToolsStyles.Apply(root);

            _mesh = root.Q<ObjectField>("mesh-field");
            _resolution = root.Q<DropdownField>("resolution-field");
            _bake = root.Q<Button>("bake-button");
            _path = root.Q<TextField>("path-field");
            _min = root.Q<DoubleField>("midlevel-field");
            _strength = root.Q<DoubleField>("strength-field");
            _warningSlot = root.Q<VisualElement>("warning-slot");

            _bake.clicked += Bake;
            _mesh.RegisterValueChangedCallback(OnMeshChanged);
            LoadLastBakeForMesh(_mesh.value as Mesh);
        }

        private void OnDestroy()
        {
            if (_bake != null)
                _bake.clicked -= Bake;
            if (_mesh != null)
                _mesh.UnregisterValueChangedCallback(OnMeshChanged);
        }

        private void OnMeshChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            LoadLastBakeForMesh(evt.newValue as Mesh);
        }

        private void Bake()
        {
            if (_mesh.value == null)
            {
                EditorUtility.DisplayDialog(Title, "Please select a Mesh asset.", "OK");
                return;
            }

            var mesh = (Mesh)_mesh.value;
            if (!mesh.isReadable)
            {
                EditorUtility.DisplayDialog(
                    Title,
                    $"The mesh '{mesh.name}' is not marked Read/Write enabled. " +
                    "Open its importer, tick Read/Write under Model > Meshes, and re-apply, then bake again.",
                    "OK");
                return;
            }

            var resolution = 1024 << _resolution.index;

            Texture2D tex = null;
            try
            {
                EditorUtility.DisplayProgressBar(Title, "Building BVH...", 0f);

                using var meshData = Mesh.AcquireReadOnlyMeshData(mesh);
                using var verts = new NativeArray<float3>(meshData[0].vertexCount, Allocator.TempJob);
                meshData[0].GetVertices(verts.Reinterpret<Vector3>());
                var sm = meshData[0].GetSubMesh(0);
                using var indices = new NativeArray<int>(sm.indexCount, Allocator.TempJob);
                meshData[0].GetIndices(indices, 0);

                BuildBvh(verts, indices, out var bvh, out var triangleIds);
                using var _bvh = bvh;
                using var _triangleIds = triangleIds;
                using var heights = new NativeArray<double>(resolution * resolution, Allocator.TempJob);

                EditorUtility.DisplayProgressBar(Title, "Raycasting mesh...", 1f / 3f);

                var job = new BakeJob
                {
                    Heights = heights,
                    Triangles = triangleIds,
                    Indices = indices,
                    Resolution = resolution,
                    Vertices = verts,
                    Bvh = bvh
                };

                job.Schedule(resolution * resolution, 64).Complete();

                EditorUtility.DisplayProgressBar(Title, "Normalizing output...", 2f / 3f);

                var min = double.PositiveInfinity;
                var max = double.NegativeInfinity;
                var unhitCount = 0;

                foreach (var height in heights)
                {
                    if (double.IsPositiveInfinity(height))
                    {
                        unhitCount++;
                        continue;
                    }
                    min = Math.Min(min, height);
                    max = Math.Max(max, height);
                }

                if (unhitCount == heights.Length)
                {
                    EditorUtility.DisplayDialog(Title,
                        "Every ray missed the mesh. Check that the mesh is centered on the origin and roughly sphere-shaped.",
                        "OK");
                    return;
                }

                var strength = max - min;

                tex = new Texture2D(resolution, resolution, TextureFormat.R16, mipChain: false, linear: true);
                var pixels = tex.GetPixelData<ushort>(0);

                for (var i = 0; i < heights.Length; i++)
                {
                    var height = heights[i];
                    if (double.IsPositiveInfinity(height)) height = min;
                    var offsetHeight = height - min;
                    var normalizedHeight = math.clamp(offsetHeight / strength, 0.0, 1.0);
                    var x = i % resolution;
                    var y = i / resolution;
                    var flippedIndex = (resolution - 1 - y) * resolution + x;
                    pixels[flippedIndex] = (ushort)math.round(normalizedHeight * ushort.MaxValue);
                }

                EditorUtility.ClearProgressBar();

                var path = EditorUtility.SaveFilePanelInProject(
                    "Save Heightmap",
                    $"{mesh.name}_Height",
                    "png",
                    "Choose where to save the baked heightmap.",
                    Path.GetDirectoryName(AssetDatabase.GetAssetPath(mesh)) ?? "Assets"
                );

                if (string.IsNullOrEmpty(path)) return;

                try
                {
                    File.WriteAllBytes(path, tex.EncodeToPNG());
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (importer == null)
                    {
                        EditorUtility.DisplayDialog(Title,
                            $"Heightmap PNG saved to '{path}' but Unity didn't return a TextureImporter for it. " +
                            "Check the file and re-import manually.",
                            "OK");
                        return;
                    }
                    importer.textureType        = TextureImporterType.SingleChannel;
                    importer.sRGBTexture        = false;
                    importer.isReadable         = true;
                    importer.mipmapEnabled      = false;
                    importer.wrapMode           = TextureWrapMode.Repeat;
                    importer.filterMode         = FilterMode.Bilinear;
                    importer.maxTextureSize     = resolution;

                    var settings = new TextureImporterPlatformSettings
                    {
                        name              = "DefaultTexturePlatform",
                        overridden        = true,
                        format            = TextureImporterFormat.R16,
                        textureCompression = TextureImporterCompression.Uncompressed,
                        maxTextureSize     = resolution,
                    };
                    importer.SetPlatformTextureSettings(settings);
                    importer.SaveAndReimport();
                }
                catch (Exception ex)
                {
                    EditorUtility.DisplayDialog(Title,
                        $"Failed to write or import the heightmap PNG at '{path}'.\n\n{ex.GetType().Name}: {ex.Message}",
                        "OK");
                    return;
                }

                var warning = unhitCount > 0
                    ? $"{unhitCount:N0} of {heights.Length:N0} pixels ({100.0 * unhitCount / heights.Length:0.##}%) had no ray hit and were filled with the minimum height. The source mesh likely has holes or doesn't fully cover the sphere."
                    : "";

                _path.value = path;
                _min.value = min;
                _strength.value = strength;
                SetWarning(warning);
                SaveLastBakeForMesh(mesh, path, min, strength, warning);
            }
            finally
            {
                if (tex != null) DestroyImmediate(tex);
                EditorUtility.ClearProgressBar();
            }
        }

        private void LoadLastBakeForMesh(Mesh mesh)
        {
            var guid = GetMeshGuid(mesh);
            var hasGuid = !string.IsNullOrEmpty(guid);
            _path.value = hasGuid ? EditorPrefs.GetString(PrefsKey(guid, "Path"), "") : "";
            _min.value = hasGuid ? ParseDouble(EditorPrefs.GetString(PrefsKey(guid, "MidLevel"), "0")) : 0;
            _strength.value = hasGuid ? ParseDouble(EditorPrefs.GetString(PrefsKey(guid, "Strength"), "0")) : 0;
            SetWarning(hasGuid ? EditorPrefs.GetString(PrefsKey(guid, "Warning"), "") : "");
        }

        private static void SaveLastBakeForMesh(Mesh mesh, string path, double midlevel, double strength, string warning)
        {
            var guid = GetMeshGuid(mesh);
            if (string.IsNullOrEmpty(guid)) return;
            EditorPrefs.SetString(PrefsKey(guid, "Path"), path);
            EditorPrefs.SetString(PrefsKey(guid, "MidLevel"), FormatDouble(midlevel));
            EditorPrefs.SetString(PrefsKey(guid, "Strength"), FormatDouble(strength));
            EditorPrefs.SetString(PrefsKey(guid, "Warning"), warning ?? "");
        }

        private void SetWarning(string message)
        {
            if (_warningSlot == null) return;
            _warningSlot.Clear();
            if (!string.IsNullOrEmpty(message))
                _warningSlot.Add(new HelpBox(message, HelpBoxMessageType.Warning));
        }

        private static string GetMeshGuid(Mesh mesh)
        {
            if (mesh == null) return null;
            var path = AssetDatabase.GetAssetPath(mesh);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
        }

        private static string PrefsKey(string guid, string suffix) => $"{PrefsPrefix}{guid}.{suffix}";

        private static double ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        private static string FormatDouble(double d) =>
            d.ToString("R", CultureInfo.InvariantCulture);

        struct BvhNode
        {
            public float3 BoundsMin;
            public float3 BoundsMax;

            public int Left;
            public int Right;
            // TriCount > 0 marks a leaf node.
            public int TriCount;
        }

        private static void BuildBvh(NativeArray<float3> vertices, NativeArray<int> indices, out NativeArray<BvhNode> bvh, out NativeArray<int> triangles)
        {
            var triCount = indices.Length / 3;

            var centroids = new float3[triCount];
            var triBounds = new (float3 min, float3 max)[triCount];

            for (var i = 0; i < triCount; i++)
            {
                var baseIndex = i * 3;
                var a = vertices[indices[baseIndex + 0]];
                var b = vertices[indices[baseIndex + 1]];
                var c = vertices[indices[baseIndex + 2]];
                centroids[i] = (a + b + c) / 3;
                triBounds[i] = (math.min(a, math.min(b, c)), math.max(a, math.max(b, c)));
            }

            var triIds = new int[triCount];
            for (var i = 0; i < triCount; i++) triIds[i] = i;

            var nodes = new List<BvhNode>();
            BuildRecursive(nodes, triIds, 0, triCount, centroids, triBounds);

            bvh = new NativeArray<BvhNode>(nodes.ToArray(), Allocator.TempJob);
            triangles = new NativeArray<int>(triIds, Allocator.TempJob);
        }

        private const int MaxLeafTris = 4;

        private static int BuildRecursive(List<BvhNode> nodes, int[] triIds, int from, int to, float3[] centroids, (float3 min, float3 max)[] triBounds)
        {
            var self = nodes.Count;
            nodes.Add(default);
            var nodeMin = new float3(float.PositiveInfinity);
            var nodeMax = new float3(float.NegativeInfinity);

            for (var i = from; i < to; i++)
            {
                var bounds = triBounds[triIds[i]];

                nodeMin = math.min(nodeMin, bounds.min);
                nodeMax = math.max(nodeMax, bounds.max);
            }

            var count = to - from;

            if (count <= MaxLeafTris)
            {
                nodes[self] = new BvhNode
                {
                    BoundsMin = nodeMin,
                    BoundsMax = nodeMax,
                    Left = from,
                    Right = 0,
                    TriCount = count,
                };
                return self;
            }

            var extent = nodeMax - nodeMin;
            var axis = extent.x > extent.y ? (extent.x > extent.z ? 0 : 2) : (extent.y > extent.z ? 1 : 2);

            Array.Sort(triIds, from, count, new CentroidComparer(centroids, axis));
            var mid = from + count / 2;

            var leftIdx = BuildRecursive(nodes, triIds, from, mid, centroids, triBounds);
            var rightIdx = BuildRecursive(nodes, triIds, mid, to, centroids, triBounds);

            nodes[self] = new BvhNode
            {
                BoundsMin = nodeMin,
                BoundsMax = nodeMax,
                Left = leftIdx,
                Right = rightIdx,
                TriCount = 0
            };
            return self;
        }


        private class CentroidComparer : IComparer<int>
        {
            private readonly float3[] _centroids;
            private readonly int _axis;

            public CentroidComparer(float3[] centroids, int axis)
            {
                _centroids = centroids;
                _axis = axis;
            }
            public int Compare(int a, int b) => _centroids[a][_axis].CompareTo(_centroids[b][_axis]);
        }


        [BurstCompile]
        struct BakeJob : IJobParallelFor
        {
            public int Resolution;
            [ReadOnly] public NativeArray<float3> Vertices;
            [ReadOnly] public NativeArray<int> Triangles;
            public NativeArray<double> Heights;
            [ReadOnly] public NativeArray<int> Indices;
            [ReadOnly] public NativeArray<BvhNode> Bvh;

            struct Hit
            {
                public double T;
                public int TriId;
            }

            public void Execute(int pixelIndex)
            {
                var x = pixelIndex % Resolution;
                var y = pixelIndex / Resolution;

                var u = (x + 0.5d) / Resolution;
                var v = (y + 0.5d) / Resolution;

                var lon = u * math.PI2_DBL - math.PI_DBL;
                var lat = math.PIHALF_DBL - v * math.PI_DBL;

                var dir = new double3(math.cos(lat) * math.cos(lon), math.sin(lat), math.sin(lon) * math.cos(lat));

                _ = Raycast(0, dir, out var hit);
                Heights[pixelIndex] = hit.T;
            }

            private bool Raycast(double3 ro, double3 rd, out Hit hit)
            {
                hit = default;
                hit.T = double.PositiveInfinity;
                var found = false;

                var inverseDir = 1 / rd;

                // 256 is the BVH traversal depth cap. Pathological inputs could exceed it - sphere-like meshes don't.
                Span<int> stack = stackalloc int[256];
                var sp = 0;
                stack[sp++] = 0;

                while (sp > 0)
                {
                    var idx = stack[--sp];
                    var node = Bvh[idx];
                    if (!RayBoxHit(ro, inverseDir, node.BoundsMin, node.BoundsMax, hit.T)) continue;

                    if (node.TriCount > 0)
                    {
                        var triStart = node.Left;
                        for (var k = 0; k < node.TriCount; k++)
                        {
                            var triangle = Triangles[triStart + k];
                            var i0 = Indices[triangle * 3 + 0];
                            var i1 = Indices[triangle * 3 + 1];
                            var i2 = Indices[triangle * 3 + 2];

                            if (!RayTri(ro, rd, Vertices[i0], Vertices[i1], Vertices[i2], out var t) || !(t < hit.T))
                                continue;
                            hit.T = t;
                            hit.TriId = triangle;
                            found = true;
                        }
                    }
                    else
                    {
                        stack[sp++] = node.Right;
                        stack[sp++] = node.Left;
                    }
                }

                return found;
            }


            private static bool RayBoxHit(double3 ro, double3 invDir, float3 fmin, float3 fmax, double tMax)
            {
                var t1 = ((double3)fmin - ro) * invDir;
                var t2 = ((double3)fmax - ro) * invDir;
                var tmin = math.min(t1, t2);
                var tmax = math.max(t1, t2);
                var tNear = math.max(math.max(tmin.x, tmin.y), tmin.z);
                var tFar  = math.min(math.min(tmax.x, tmax.y), tmax.z);
                return tNear <= tFar && tFar > 0.0 && tNear < tMax;
            }

            private static bool RayTri(double3 ro, double3 rd, float3 fv0, float3 fv1, float3 fv2, out double t)
            {
                t = 0.0;
                double3 v0 = fv0, v1 = fv1, v2 = fv2;
                var e1 = v1 - v0;
                var e2 = v2 - v0;
                var p  = math.cross(rd, e2);
                var det = math.dot(e1, p);
                if (math.abs(det) < 1e-20) return false;
                var inv = 1.0 / det;
                var tv = ro - v0;
                var u = math.dot(tv, p) * inv;
                if (u < 0.0 || u > 1.0) return false;
                var q = math.cross(tv, e1);
                var v = math.dot(rd, q) * inv;
                if (v < 0.0 || u + v > 1.0) return false;
                t = math.dot(e2, q) * inv;
                return t > 1e-9;
            }
        }
    }
}
