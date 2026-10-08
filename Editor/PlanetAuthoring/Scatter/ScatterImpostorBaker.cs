using System;
using System.Collections.Generic;
using System.IO;
using AwesomeTechnologies;
using AwesomeTechnologies.VegetationSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Scatter
{
    /// <summary>
    /// Bakes a scatter prefab into an octahedral impostor the scatter billboard draw can use.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Renders the ImpostorCapture pass of the prefab's scatter stand-in materials from <see cref="Frames" />
    /// by <see cref="Frames" /> directions into one G-buffer atlas, packs it into the four textures
    /// <c>Redux/Environment/Impostor/Octahedron_Impostor</c> reads, and writes those with a card mesh,
    /// a material and a prefab beside the source prefab. The layout and sizes match stock impostors.
    /// </para>
    /// <para>
    /// The capture is ungraded on purpose. Vegetation Studio applies an item's tint, brightness,
    /// contrast and saturation to the billboard material every frame, as it does to the mesh.
    /// </para>
    /// </remarks>
    public static class ScatterImpostorBaker
    {
        /// <summary>
        /// Width and height of each impostor texture in texels.
        /// </summary>
        public const int AtlasSize = 2048;

        /// <summary>
        /// Number of frames along each side of the atlas.
        /// </summary>
        public const int Frames = 16;

        /// <summary>
        /// How many texels colour is grown past the silhouette.
        /// </summary>
        public const int DilationTexels = 32;

        /// <summary>
        /// Most vertices the card mesh may have.
        /// </summary>
        public const int MaxCardVertices = 8;

        /// <summary>
        /// Name of the shader the baked material uses.
        /// </summary>
        public const string ImpostorShaderName = "Redux/Environment/Impostor/Octahedron_Impostor";

        /// <summary>
        /// Folder, beside the source prefab, that baked impostors are written to.
        /// </summary>
        public const string OutputFolderName = "Billboards";

        private const string BAKE_SHADER_NAME = "Hidden/Redux/ImpostorBake";
        // Pass of the Redux/Environment/Scatter stand-ins that writes the surface into the G-buffer atlas.
        private const string CAPTURE_PASS_NAME = "ImpostorCapture";
        private const int PACK_PASS = 0;
        private const int DILATE_PASS = 1;
        private const int GROW_MASK_PASS = 2;
        private const int COVERAGE_PASS = 3;

        private static readonly string[] TEXTURE_SUFFIXES = { "AlbedoAlpha", "NormalDepth", "SpecularSmoothness", "EmissionOcclusion" };
        private static readonly string[] TEXTURE_PROPERTIES = { "_Albedo", "_Normals", "_Specular", "_Emission" };

        /// <summary>
        /// Returns whether Vegetation Studio can ever draw a billboard for an item.
        /// </summary>
        /// <remarks>
        /// Billboards are drawn from billboard cells alone, which hold only trees and large and medium
        /// objects. A texture item has no mesh to bake from.
        /// </remarks>
        /// <param name="item">The item to check.</param>
        /// <returns>True if the item is a mesh item in a billboard cell, false otherwise.</returns>
        public static bool CanBillboard(VegetationItemInfoPro item) => CanBillboard(item.VegetationType, item.PrefabType);

        /// <summary>
        /// Returns whether Vegetation Studio can ever draw a billboard for an item of this type.
        /// </summary>
        /// <param name="type">The item's vegetation type.</param>
        /// <param name="prefabType">The item's prefab type.</param>
        /// <returns>True if the type is a mesh item in a billboard cell, false otherwise.</returns>
        public static bool CanBillboard(VegetationType type, VegetationPrefabType prefabType) =>
            prefabType == VegetationPrefabType.Mesh
            && VegetationSystemPro.VegetationTypeToVegetationCellType(type) == VegetationCellType.Billboard;

        /// <summary>
        /// Rebakes the billboard of every item in a package that uses one.
        /// </summary>
        /// <remarks>
        /// Each distinct prefab is baked once and assigned to every item that spawns it. Shows a
        /// progress bar for the duration.
        /// </remarks>
        /// <param name="package">The package to bake.</param>
        /// <returns>The number of items given a billboard.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a prefab cannot be rendered. Items baked before it keep their billboards.</exception>
        public static int BakePackage(VegetationPackagePro package)
        {
            var itemsByPrefab = new Dictionary<GameObject, List<VegetationItemInfoPro>>();
            foreach (VegetationItemInfoPro item in package.VegetationInfoList)
            {
                if (item == null || !item.UseBillboards || !CanBillboard(item) || item.VegetationPrefab == null)
                    continue;

                if (!itemsByPrefab.TryGetValue(item.VegetationPrefab, out List<VegetationItemInfoPro> items))
                {
                    items = new List<VegetationItemInfoPro>();
                    itemsByPrefab.Add(item.VegetationPrefab, items);
                }

                items.Add(item);
            }

            Undo.RecordObject(package, "Bake Scatter Billboards");
            int baked = 0;
            int prefabIndex = 0;
            try
            {
                foreach (KeyValuePair<GameObject, List<VegetationItemInfoPro>> entry in itemsByPrefab)
                {
                    EditorUtility.DisplayProgressBar(
                        "Baking scatter billboards",
                        entry.Key.name,
                        (float)prefabIndex++ / itemsByPrefab.Count);

                    GameObject impostor = Bake(entry.Key);
                    foreach (VegetationItemInfoPro item in entry.Value)
                    {
                        item.BillboardCustomPrefab = impostor;
                        baked++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(package);
            }

            return baked;
        }

        /// <summary>
        /// Bakes an item's prefab and assigns the result as the item's billboard.
        /// </summary>
        /// <param name="package">The package holding <paramref name="item" />, recorded for undo.</param>
        /// <param name="item">The item to bake.</param>
        /// <returns>The baked impostor prefab.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the item has no prefab or it cannot be rendered.</exception>
        public static GameObject BakeItem(VegetationPackagePro package, VegetationItemInfoPro item)
        {
            if (item.VegetationPrefab == null)
                throw new InvalidOperationException($"Item '{item.Name}' has no prefab to bake.");

            GameObject impostor = Bake(item.VegetationPrefab);
            Undo.RecordObject(package, "Bake Scatter Billboard");
            item.BillboardCustomPrefab = impostor;
            EditorUtility.SetDirty(package);
            return impostor;
        }

        /// <summary>
        /// Bakes a prefab into an impostor prefab, overwriting an earlier bake of it in place.
        /// </summary>
        /// <remarks>
        /// Output is keyed by the source prefab rather than by item, so items sharing a prefab share one
        /// impostor. Rebaking keeps every asset's GUID, so references to the impostor survive.
        /// </remarks>
        /// <param name="sourcePrefab">The prefab asset to bake.</param>
        /// <returns>The impostor prefab asset.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the prefab has nothing the bake can render.</exception>
        public static GameObject Bake(GameObject sourcePrefab)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourcePrefab);
            if (string.IsNullOrEmpty(sourcePath))
                throw new InvalidOperationException($"'{sourcePrefab.name}' is not a prefab asset.");

            var materialCopies = new List<Material>();
            RenderTexture[] gBuffers = null;
            RenderTexture depth = null;
            try
            {
                List<BakeDraw> draws = CollectDraws(sourcePrefab, materialCopies);
                Bounds bounds = MeasureBounds(draws, out float radius);
                float impostorSize = radius * 2f;

                gBuffers = new RenderTexture[4];
                for (int i = 0; i < gBuffers.Length; i++)
                {
                    gBuffers[i] = CreateTarget(RenderTextureFormat.ARGBHalf);
                }

                depth = new RenderTexture(AtlasSize, AtlasSize, 24, RenderTextureFormat.Depth) { name = "ImpostorBakeDepth" };
                depth.Create();

                Capture(draws, gBuffers, depth, bounds.center, impostorSize);
                Texture2D[] textures = Pack(gBuffers, depth, out bool[] coverage);

                string baseName = $"{sourcePrefab.name}_Impostor";
                string folder = EnsureOutputFolder(sourcePath);
                Texture2D[] imported = WriteTextures(textures, folder, baseName);
                foreach (Texture2D texture in textures)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }

                Vector2[] outline = ImpostorOutline.Enclose(coverage, AtlasSize / Frames, MaxCardVertices);
                if (outline.Length < 3)
                    throw new InvalidOperationException($"'{sourcePrefab.name}' rendered nothing in any frame.");

                Mesh mesh = WriteMesh(ImpostorOutline.BuildMesh(outline, impostorSize, bounds), folder, baseName);
                Material material = WriteMaterial(imported, folder, baseName, impostorSize, bounds.center);
                return WritePrefab(mesh, material, folder, baseName);
            }
            finally
            {
                foreach (Material copy in materialCopies)
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }

                if (gBuffers != null)
                {
                    foreach (RenderTexture target in gBuffers)
                    {
                        DestroyTarget(target);
                    }
                }

                DestroyTarget(depth);
            }
        }

        private struct BakeDraw
        {
            public Mesh Mesh;
            public int SubMesh;
            public Material Material;
            public int Pass;
            public Matrix4x4 Matrix;
        }

        // Exactly what Vegetation Studio draws for the prefab, with each material copy kept on its scatter
        // stand-in, whose ImpostorCapture pass writes the surface the game's scatter shader lights. The
        // game's URP scatter shaders have no pass that writes a G-buffer, so the copies are not moved onto
        // them. That is the one mesh SelectMeshObject picks for LOD0, scaled by its object's
        // local scale and nothing else. The billboard draw leaves that scale out of its instance
        // matrices, so the impostor has to carry it.
        // Copies are added to the caller's list as they are made, so a failure part way still frees them.
        private static List<BakeDraw> CollectDraws(GameObject sourcePrefab, List<Material> materialCopies)
        {
            GameObject selected = MeshUtils.SelectMeshObject(sourcePrefab, LODLevel.LOD0);
            var filter = selected != null ? selected.GetComponentInChildren<MeshFilter>() : null;
            var renderer = selected != null ? selected.GetComponentInChildren<MeshRenderer>() : null;
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new InvalidOperationException($"'{sourcePrefab.name}' has no mesh renderer to bake.");

            Matrix4x4 matrix = Matrix4x4.Scale(selected.transform.localScale);
            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = Mathf.Min(materials.Length, filter.sharedMesh.subMeshCount);
            var draws = new List<BakeDraw>();
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                if (materials[subMesh] == null)
                    continue;

                var copy = new Material(materials[subMesh]) { hideFlags = HideFlags.HideAndDontSave };
                materialCopies.Add(copy);
                int pass = copy.FindPass(CAPTURE_PASS_NAME);
                if (pass < 0)
                {
                    throw new InvalidOperationException(
                        $"'{materials[subMesh].name}' on '{sourcePrefab.name}' uses '{copy.shader.name}', which has no {CAPTURE_PASS_NAME} pass. "
                        + "Put it on a Redux/Environment/Scatter stand-in.");
                }

                draws.Add(new BakeDraw { Mesh = filter.sharedMesh, SubMesh = subMesh, Material = copy, Pass = pass, Matrix = matrix });
            }

            if (draws.Count == 0)
                throw new InvalidOperationException($"'{sourcePrefab.name}' has no materials to bake.");

            return draws;
        }

        // The prefab's bounds in its root space, and the radius around their centre that every view fits in.
        private static Bounds MeasureBounds(List<BakeDraw> draws, out float radius)
        {
            var points = new List<Vector3>();
            foreach (BakeDraw draw in draws)
            {
                // Vertices give the tightest fit. A mesh imported without read access only offers
                // its bounds, whose corners still enclose it.
                if (draw.Mesh.isReadable)
                {
                    foreach (Vector3 vertex in draw.Mesh.vertices)
                    {
                        points.Add(draw.Matrix.MultiplyPoint3x4(vertex));
                    }

                    continue;
                }

                Bounds local = draw.Mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                    points.Add(draw.Matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents, sign)));
                }
            }

            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points)
            {
                bounds.Encapsulate(point);
            }

            radius = 0f;
            foreach (Vector3 point in points)
            {
                radius = Mathf.Max(radius, Vector3.Distance(point, bounds.center));
            }

            return bounds;
        }

        // Draws every frame into its cell of the G-buffer atlas.
        private static void Capture(List<BakeDraw> draws, RenderTexture[] gBuffers, RenderTexture depth, Vector3 centre, float impostorSize)
        {
            var colorTargets = new RenderTargetIdentifier[gBuffers.Length];
            for (int i = 0; i < gBuffers.Length; i++)
            {
                colorTargets[i] = gBuffers[i];
            }

            var commands = new CommandBuffer { name = "Scatter Impostor Bake" };
            commands.SetRenderTarget(colorTargets, depth);
            commands.ClearRenderTarget(true, true, Color.clear, 1f);

            float halfSize = impostorSize * 0.5f;
            Matrix4x4 projection = Matrix4x4.Ortho(-halfSize, halfSize, -halfSize, halfSize, 0f, impostorSize);
            int cellSize = AtlasSize / Frames;
            for (int x = 0; x < Frames; x++)
            {
                for (int y = 0; y < Frames; y++)
                {
                    Vector3 direction = ImpostorOctahedron.FrameDirection(x, y, Frames);
                    ImpostorOctahedron.FrameAxes(direction, out Vector3 right, out Vector3 up);

                    // The camera sits half the depth range out along the frame direction, so the
                    // range is centred on the frame plane through the bounds centre.
                    Vector3 cameraPosition = centre + direction * halfSize;
                    commands.SetViewProjectionMatrices(ViewMatrix(cameraPosition, right, up, direction), projection);
                    commands.SetGlobalVector("_WorldSpaceCameraPos", cameraPosition);
                    commands.SetViewport(new Rect(x * cellSize, y * cellSize, cellSize, cellSize));
                    foreach (BakeDraw draw in draws)
                    {
                        commands.DrawMesh(draw.Mesh, draw.Matrix, draw.Material, draw.SubMesh, draw.Pass);
                    }
                }
            }

            Graphics.ExecuteCommandBuffer(commands);
            commands.Release();
        }

        // World to camera for a camera at position looking against backward, Unity's view space
        // convention being camera forward along negative Z.
        private static Matrix4x4 ViewMatrix(Vector3 position, Vector3 right, Vector3 up, Vector3 backward)
        {
            var view = Matrix4x4.identity;
            view.SetRow(0, new Vector4(right.x, right.y, right.z, -Vector3.Dot(right, position)));
            view.SetRow(1, new Vector4(up.x, up.y, up.z, -Vector3.Dot(up, position)));
            view.SetRow(2, new Vector4(backward.x, backward.y, backward.z, -Vector3.Dot(backward, position)));
            return view;
        }

        // Packs, dilates and reads back the four impostor textures. Coverage comes back as the union
        // of every frame's silhouette in one frame's space, for the card outline.
        private static Texture2D[] Pack(RenderTexture[] gBuffers, RenderTexture depth, out bool[] coverage)
        {
            var bakeMaterial = new Material(Shader.Find(BAKE_SHADER_NAME)) { hideFlags = HideFlags.HideAndDontSave };
            var packed = new RenderTexture[TEXTURE_SUFFIXES.Length];
            var scratch = CreateTarget(RenderTextureFormat.ARGB32);
            var mask = CreateTarget(RenderTextureFormat.ARGB32);
            var maskScratch = CreateTarget(RenderTextureFormat.ARGB32);
            try
            {
                bakeMaterial.SetTexture("_GBuffer0", gBuffers[0]);
                bakeMaterial.SetTexture("_GBuffer1", gBuffers[1]);
                bakeMaterial.SetTexture("_GBuffer2", gBuffers[2]);
                bakeMaterial.SetTexture("_GBuffer3", gBuffers[3]);
                bakeMaterial.SetTexture("_BakeDepth", depth);
                for (int i = 0; i < packed.Length; i++)
                {
                    packed[i] = CreateTarget(RenderTextureFormat.ARGB32);
                    bakeMaterial.SetInt("_PackOutput", i);
                    Graphics.Blit(null, packed[i], bakeMaterial, PACK_PASS);
                }

                coverage = ReadCoverage(packed[0]);

                Graphics.Blit(packed[0], mask, bakeMaterial, COVERAGE_PASS);
                for (int step = 0; step < DilationTexels; step++)
                {
                    bakeMaterial.SetTexture("_Coverage", mask);
                    for (int i = 0; i < packed.Length; i++)
                    {
                        // Albedo alpha is the silhouette the shader clips on, so only its colour grows.
                        bakeMaterial.SetFloat("_PreserveAlpha", i == 0 ? 1f : 0f);
                        Graphics.Blit(packed[i], scratch, bakeMaterial, DILATE_PASS);
                        (packed[i], scratch) = (scratch, packed[i]);
                    }

                    Graphics.Blit(mask, maskScratch, bakeMaterial, GROW_MASK_PASS);
                    (mask, maskScratch) = (maskScratch, mask);
                }

                var textures = new Texture2D[packed.Length];
                for (int i = 0; i < packed.Length; i++)
                {
                    textures[i] = ReadBack(packed[i]);
                }

                return textures;
            }
            finally
            {
                // Blit leaves its destination active, and an active target cannot be destroyed cleanly.
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(bakeMaterial);
                foreach (RenderTexture target in packed)
                {
                    DestroyTarget(target);
                }

                DestroyTarget(scratch);
                DestroyTarget(mask);
                DestroyTarget(maskScratch);
            }
        }

        private static bool[] ReadCoverage(RenderTexture albedoAlpha)
        {
            Texture2D readBack = ReadBack(albedoAlpha);
            Color32[] pixels = readBack.GetPixels32();
            UnityEngine.Object.DestroyImmediate(readBack);

            int cellSize = AtlasSize / Frames;
            var coverage = new bool[cellSize * cellSize];
            for (int row = 0; row < AtlasSize; row++)
            {
                for (int column = 0; column < AtlasSize; column++)
                {
                    if (pixels[row * AtlasSize + column].a > 127)
                        coverage[(row % cellSize) * cellSize + column % cellSize] = true;
                }
            }

            return coverage;
        }

        private static RenderTexture CreateTarget(RenderTextureFormat format)
        {
            var target = new RenderTexture(AtlasSize, AtlasSize, 0, format, RenderTextureReadWrite.Linear);
            target.Create();
            return target;
        }

        private static void DestroyTarget(RenderTexture target)
        {
            if (target == null)
                return;

            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }

        private static Texture2D ReadBack(RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false, true);
            texture.ReadPixels(new Rect(0, 0, AtlasSize, AtlasSize), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        private static string EnsureOutputFolder(string sourcePath)
        {
            string parent = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string folder = $"{parent}/{OutputFolderName}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(parent, OutputFolderName);
            return folder;
        }

        private static Texture2D[] WriteTextures(Texture2D[] textures, string folder, string baseName)
        {
            var paths = new string[textures.Length];
            for (int i = 0; i < textures.Length; i++)
            {
                paths[i] = $"{folder}/{baseName}_{TEXTURE_SUFFIXES[i]}.png";
                File.WriteAllBytes(paths[i], textures[i].EncodeToPNG());
            }

            AssetDatabase.Refresh();

            var imported = new Texture2D[textures.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(paths[i]);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = i == 0;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = AtlasSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
                imported[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            }

            return imported;
        }

        private static Mesh WriteMesh(Mesh mesh, string folder, string baseName)
        {
            mesh.name = $"{baseName}_Mesh";
            string path = $"{folder}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Material WriteMaterial(Texture2D[] textures, string folder, string baseName, float impostorSize, Vector3 offset)
        {
            string path = $"{folder}/{baseName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ImpostorShaderName));
                AssetDatabase.CreateAsset(material, path);
            }

            for (int i = 0; i < textures.Length; i++)
            {
                material.SetTexture(TEXTURE_PROPERTIES[i], textures[i]);
            }

            material.SetFloat("_Frames", Frames);
            material.SetFloat("_ImpostorSize", impostorSize);
            material.SetFloat("_DepthSize", impostorSize);
            material.SetVector("_Offset", offset);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject WritePrefab(Mesh mesh, Material material, string folder, string baseName)
        {
            string path = $"{folder}/{baseName}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                AssetDatabase.SaveAssets();
                return existing;
            }

            var instance = new GameObject(baseName);
            try
            {
                instance.AddComponent<MeshFilter>().sharedMesh = mesh;
                instance.AddComponent<MeshRenderer>().sharedMaterial = material;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
