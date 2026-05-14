using System;
using System.IO;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Bakes a low-poly displaced sphere mesh for a body's scaled-space view, optionally composites
    /// ocean color into the scaled albedo, and wires the body's <c>Celestial.&lt;Body&gt;.Scaled.prefab</c>
    /// to point at the resulting mesh + material.
    /// </summary>
    /// <remarks>
    /// Operates purely against asset references on the body and its PQS. Does not require a live
    /// authoring session or any open scene.
    /// </remarks>
    public static class ScaledSpaceBakerOperation
    {
        /// <summary>
        /// Authored radius (in mesh units) of the scaled-space mesh. Drives the SphereCollider
        /// sized by the wizard so <c>ScaledPlanetaryBodyView.invBaseSizeFactor</c> cancels the
        /// authored size out and world-space scaling lands at the body's actual radius. Map-view
        /// scaling reads this too via the rendered mesh size.
        /// </summary>
        public const float AuthoredRadius = 1500f;

        /// <summary>Per-bake settings.</summary>
        public struct Settings
        {
            /// <summary>0=64x32, 1=128x64, 2=256x128, 3=512x256.</summary>
            public int MeshResolutionIndex;
            /// <summary>When true, ocean color is composited into the scaled albedo and terrain is clamped to sea level.</summary>
            public bool IncludeOcean;
            /// <summary>Color written over ocean pixels in the scaled albedo when <see cref="IncludeOcean"/> is true.</summary>
            public Color OceanColor;
        }

        /// <summary>Result of a bake attempt.</summary>
        public struct Result
        {
            /// <summary>True when the bake completed end to end.</summary>
            public bool Success;
            /// <summary>Error message when <see cref="Success"/> is false.</summary>
            public string Error;
            /// <summary>Asset path of the wired Scaled prefab on success.</summary>
            public string PrefabPath;
            /// <summary>Folder the bake outputs were written into on success.</summary>
            public string ScaledFolder;
        }

        /// <summary>Executes the bake pipeline against <paramref name="body"/> with <paramref name="settings"/>.</summary>
        /// <param name="body">The body whose scaled view to bake. Must have a non-empty bodyName and positive radius.</param>
        /// <param name="settings">Per-bake settings.</param>
        /// <returns>A <see cref="Result"/> describing success or failure.</returns>
        public static Result Bake(CoreCelestialBodyData body, Settings settings)
        {
            if (!TryPrepareContext(body, settings, out var ctx, out var error))
                return Fail(error);

            try
            {
                ProgressBar("Baking mesh...", 0.25f);
                var meshAsset = BakeLodMesh(ctx);

                ProgressBar("Resolving material...", 0.55f);
                var matAsset = ResolveOrCreateMaterial(ctx);

                var finalAlbedo = ApplyOptionalOcean(ctx);
                BindAlbedoToMaterial(matAsset, finalAlbedo);

                FlushBeforePrefabWiring(ref meshAsset, ref matAsset);

                ProgressBar("Wiring prefab...", 0.85f);
                var prefabPath = WirePrefab(ctx.BodyFolder, ctx.BodyName, meshAsset, matAsset);

                FinalizeImports(meshAsset, prefabPath);

                Debug.Log($"[ScaledSpaceBaker] Wrote mesh+material to '{ctx.ScaledFolder}/' and wired prefab '{prefabPath}'.");
                return Succeed(prefabPath, ctx.ScaledFolder);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return Fail($"{ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // Carries the resolved inputs the bake steps consume so step signatures stay small.
        private struct BakeContext
        {
            public string BodyName;
            public float Radius;
            public PQSData PqsData;
            public string BodyFolder;
            public string ScaledFolder;
            public Texture2D SourceAlbedo;
            public Texture2D GlobalHeightMap;
            public float HeightScale;
            public bool HasOcean;
            public float OceanNormalized;
            public Color OceanColor;
            public int MeshResolutionIndex;
        }

        private static bool TryPrepareContext(CoreCelestialBodyData body, Settings settings, out BakeContext ctx, out string error)
        {
            ctx = default;
            if (body == null) { error = "No body provided."; return false; }

            var bodyName = body.Data?.bodyName;
            var radius = (float)(body.Data?.radius ?? 0.0);
            if (string.IsNullOrEmpty(bodyName) || radius <= 0f) { error = "Body needs a non-empty name and a positive radius."; return false; }

            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) { error = "Could not resolve a PQS for the body. Ensure the Local prefab exists and has a PQS, or that the authoring scene contains the body."; return false; }

            var pqsData = pqs.data;
            if (pqsData == null) { error = "PQS has no PQSData asset assigned."; return false; }

            var bodyFolder = ResolveBodyFolder(body, pqsData);
            if (bodyFolder == null) { error = "Could not determine the body's asset folder. Save the body's prefab and PQSData before baking."; return false; }

            var scaledFolder = bodyFolder + "/Scaled";
            if (!AssetDatabase.IsValidFolder(scaledFolder))
                AssetDatabase.CreateFolder(bodyFolder, "Scaled");

            var surfaceMaterial = pqsData.materialSettings?.surfaceMaterial;
            var sourceAlbedo = surfaceMaterial != null ? surfaceMaterial.GetTexture("_AlbedoScaledTex") as Texture2D : null;
            var heightScale = pqsData.heightMapInfo?.heightMapScale ?? 0f;
            var hasOcean = (body.Data?.hasOcean ?? false) && settings.IncludeOcean;
            var oceanAltitude = (float)(body.Data?.oceanAltitude ?? 0);
            // Heightmap is normalized [0,1] over [-heightScale/2, +heightScale/2] around the body
            // radius. Ocean sits at radius - oceanAltitude, so normalized = 0.5 - oceanAltitude / heightScale.
            var oceanNormalized = hasOcean && heightScale > 0 ? 0.5f - oceanAltitude / heightScale : -1f;

            ctx = new BakeContext
            {
                BodyName = bodyName,
                Radius = radius,
                PqsData = pqsData,
                BodyFolder = bodyFolder,
                ScaledFolder = scaledFolder,
                SourceAlbedo = sourceAlbedo,
                GlobalHeightMap = pqsData.heightMapInfo?.globalHeightMap,
                HeightScale = heightScale,
                HasOcean = hasOcean,
                OceanNormalized = oceanNormalized,
                OceanColor = settings.OceanColor,
                MeshResolutionIndex = settings.MeshResolutionIndex,
            };
            error = null;
            return true;
        }

        private static Texture2D ApplyOptionalOcean(BakeContext ctx)
        {
            if (!ctx.HasOcean || ctx.SourceAlbedo == null) return ctx.SourceAlbedo;
            ProgressBar("Compositing ocean...", 0.7f);
            var albedoPath = CompositeOceanAlbedo(ctx.SourceAlbedo, ctx.GlobalHeightMap, ctx.OceanNormalized, ctx.OceanColor, ctx.ScaledFolder, ctx.BodyName);
            if (albedoPath == null) return ctx.SourceAlbedo;
            AssetDatabase.ImportAsset(albedoPath, ImportAssetOptions.ForceUpdate);
            ConfigureAlbedoImporter(albedoPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        }

        private static void BindAlbedoToMaterial(Material matAsset, Texture2D finalAlbedo)
        {
            matAsset.SetTexture(Shader.PropertyToID("_MainTex"), finalAlbedo);
            EditorUtility.SetDirty(matAsset);
        }

        // Flushes both newly-created assets before the prefab references them. Skipping this leaves
        // the prefab's MeshFilter.sharedMesh slot null when loaded from disk - the body renders
        // invisible at runtime with no error.
        private static void FlushBeforePrefabWiring(ref Mesh meshAsset, ref Material matAsset)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            matAsset = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(matAsset));
            meshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GetAssetPath(meshAsset));
        }

        private static void FinalizeImports(Mesh meshAsset, string prefabPath)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            // Synchronous reimport so any open scene picks up the prefab's new structure on its
            // next render tick. Without this, scene-side PrefabInstance views can keep stale
            // "no mesh" state until the scene file is closed and reopened.
            var meshAssetPath = AssetDatabase.GetAssetPath(meshAsset);
            if (!string.IsNullOrEmpty(meshAssetPath))
                AssetDatabase.ImportAsset(meshAssetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ProgressBar(string step, float progress) => EditorUtility.DisplayProgressBar("Scaled Space Bake", step, progress);
        private static Result Fail(string error) => new() { Success = false, Error = error };
        private static Result Succeed(string prefabPath, string scaledFolder) => new() { Success = true, PrefabPath = prefabPath, ScaledFolder = scaledFolder };

        private static string ResolveBodyFolder(CoreCelestialBodyData body, PQSData pqsData)
        {
            var bodyAssetPath = AssetDatabase.GetAssetPath(body);
            if (!string.IsNullOrEmpty(bodyAssetPath))
            {
                var folder = Path.GetDirectoryName(bodyAssetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder)) return folder;
            }
            var pqsAssetPath = AssetDatabase.GetAssetPath(pqsData);
            return string.IsNullOrEmpty(pqsAssetPath) ? null : Path.GetDirectoryName(pqsAssetPath)?.Replace('\\', '/');
        }

        private static Mesh BakeLodMesh(BakeContext ctx)
        {
            var path = $"{ctx.ScaledFolder}/{ctx.BodyName}_scaled_mesh.asset";
            var mainMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool created = mainMesh == null;

            var baseLon = 64 << ctx.MeshResolutionIndex;
            var baseLatBuild = baseLon / 2;
            BuildSphereVertices(baseLon, baseLatBuild, ctx.GlobalHeightMap, ctx.Radius, ctx.HeightScale, ctx.OceanNormalized,
                out var verts, out var normals, out var uvs);

            // Halve resolution per LOD until the coarsest level hits MinLodLon. Higher base
            // resolutions get more LODs so the renderer always has a sparse-enough level for
            // far-distance rendering.
            const int minLodLon = 16;
            int lodCount = 1;
            while ((baseLon >> lodCount) >= minLodLon) lodCount++;

            var indexBuffers = new int[lodCount][];
            int totalIndexCount = 0;
            for (int lod = 0; lod < lodCount; lod++)
            {
                indexBuffers[lod] = BuildSphereIndices(baseLon, baseLatBuild, 1 << lod);
                totalIndexCount += indexBuffers[lod].Length;
            }

            var combined = new int[totalIndexCount];
            var lodRanges = new MeshLodRange[lodCount];
            int writeOffset = 0;
            for (int lod = 0; lod < lodCount; lod++)
            {
                Array.Copy(indexBuffers[lod], 0, combined, writeOffset, indexBuffers[lod].Length);
                lodRanges[lod] = new MeshLodRange { indexStart = (uint)writeOffset, indexCount = (uint)indexBuffers[lod].Length };
                writeOffset += indexBuffers[lod].Length;
            }

            if (mainMesh == null) mainMesh = new Mesh();
            mainMesh.Clear();
            mainMesh.name = $"{ctx.BodyName}_scaled_mesh";
            var indexFormat = verts.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mainMesh.indexFormat = indexFormat;
            mainMesh.SetVertices(verts);
            mainMesh.SetNormals(normals);
            mainMesh.SetUVs(0, uvs);
            mainMesh.SetIndexBufferParams(totalIndexCount, indexFormat);
            if (indexFormat == UnityEngine.Rendering.IndexFormat.UInt16)
            {
                var combinedU16 = new ushort[totalIndexCount];
                for (int i = 0; i < totalIndexCount; i++) combinedU16[i] = (ushort)combined[i];
                mainMesh.SetIndexBufferData(combinedU16, 0, 0, totalIndexCount);
            }
            else
            {
                mainMesh.SetIndexBufferData(combined, 0, 0, totalIndexCount);
            }
            mainMesh.subMeshCount = 1;
            // lodCount must be set before SetLods or SetSubMesh's LOD validation throws.
            mainMesh.lodCount = lodCount;
            mainMesh.SetSubMesh(0, new UnityEngine.Rendering.SubMeshDescriptor(0, totalIndexCount, MeshTopology.Triangles));
            mainMesh.SetLods(lodRanges, submesh: 0, UnityEngine.Rendering.MeshUpdateFlags.Default);

            mainMesh.lodSelectionCurve = new Mesh.LodSelectionCurve(4f, 0.9f);
            mainMesh.RecalculateBounds();
            mainMesh.RecalculateTangents();

            if (created)
                AssetDatabase.CreateAsset(mainMesh, path);
            else
                EditorUtility.SetDirty(mainMesh);
            return mainMesh;
        }

        private static void BuildSphereVertices(int lonDivisions, int latDivisions, Texture2D globalHeightMap, float radius, float heightScale, float oceanNormalized,
            out Vector3[] verts, out Vector3[] normals, out Vector2[] uvs)
        {
            Color[] heightPixels = null;
            int hmW = 0, hmH = 0;
            float dispScale = 0f;
            if (globalHeightMap != null && heightScale > 0f && radius > 0f && globalHeightMap.isReadable)
            {
                heightPixels = globalHeightMap.GetPixels();
                hmW = globalHeightMap.width;
                hmH = globalHeightMap.height;
                dispScale = (heightScale / radius) * AuthoredRadius;
            }

            var vertCount = (lonDivisions + 1) * (latDivisions + 1);
            verts = new Vector3[vertCount];
            normals = new Vector3[vertCount];
            uvs = new Vector2[vertCount];

            for (int y = 0; y <= latDivisions; y++)
            {
                var v = y / (float)latDivisions;
                var theta = v * Mathf.PI;
                var sinTheta = Mathf.Sin(theta);
                var cosTheta = Mathf.Cos(theta);
                for (int x = 0; x <= lonDivisions; x++)
                {
                    var u = x / (float)lonDivisions;
                    var phi = u * Mathf.PI * 2f;
                    var dir = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi));
                    var displacement = 0f;
                    if (heightPixels != null)
                    {
                        var h = SampleBilinear(heightPixels, hmW, hmH, u, 1f - v);
                        if (oceanNormalized >= 0f && h < oceanNormalized)
                            h = oceanNormalized;
                        displacement = (h - 0.5f) * dispScale;
                    }
                    var i = y * (lonDivisions + 1) + x;
                    verts[i] = dir * (AuthoredRadius + displacement);
                    normals[i] = dir;
                    uvs[i] = new Vector2(u, 1f - v);
                }
            }
        }

        // Index buffer that references every `stride`-th vertex along both axes of the LOD0 grid.
        // Lower LODs use larger strides to coarsen tessellation without changing vertex data.
        private static int[] BuildSphereIndices(int baseLon, int baseLat, int stride)
        {
            var lonSteps = Mathf.Max(1, baseLon / stride);
            var latSteps = Mathf.Max(1, baseLat / stride);
            var rowStride = baseLon + 1;
            var tris = new int[lonSteps * latSteps * 6];
            int t = 0;
            for (int y = 0; y < latSteps; y++)
            {
                for (int x = 0; x < lonSteps; x++)
                {
                    var i0 = (y * stride) * rowStride + (x * stride);
                    var i1 = i0 + stride;
                    var i2 = ((y * stride) + stride) * rowStride + (x * stride);
                    var i3 = i2 + stride;
                    tris[t++] = i0; tris[t++] = i1; tris[t++] = i2;
                    tris[t++] = i1; tris[t++] = i3; tris[t++] = i2;
                }
            }
            return tris;
        }

        private static float SampleBilinear(Color[] pixels, int w, int h, float u, float v)
        {
            u -= Mathf.Floor(u);
            v = Mathf.Clamp01(v);
            var fx = u * (w - 1);
            var fy = v * (h - 1);
            var x0 = Mathf.FloorToInt(fx);
            var y0 = Mathf.FloorToInt(fy);
            var x1 = (x0 + 1) % w;
            var y1 = Mathf.Min(y0 + 1, h - 1);
            var tx = fx - x0;
            var ty = fy - y0;
            var c00 = pixels[y0 * w + x0].r;
            var c10 = pixels[y0 * w + x1].r;
            var c01 = pixels[y1 * w + x0].r;
            var c11 = pixels[y1 * w + x1].r;
            var a = Mathf.Lerp(c00, c10, tx);
            var b = Mathf.Lerp(c01, c11, tx);
            return Mathf.Lerp(a, b, ty);
        }

        private static Material ResolveOrCreateMaterial(BakeContext ctx)
        {
            var matName = $"{ctx.BodyName}_Scaled.mat";
            var scaledMatPath = $"{ctx.ScaledFolder}/{matName}";

            // Prefer the artist's existing scaledSpaceMaterial assignment. If it lives at body root
            // move it into Scaled/ so all scaled-space assets sit together. MoveAsset preserves the
            // GUID so prefab refs follow automatically.
            var existing = ctx.PqsData?.materialSettings?.scaledSpaceMaterial;
            if (existing != null)
            {
                var existingPath = AssetDatabase.GetAssetPath(existing);
                if (!string.IsNullOrEmpty(existingPath) && !existingPath.Equals(scaledMatPath, StringComparison.OrdinalIgnoreCase) && !existingPath.StartsWith(ctx.ScaledFolder, StringComparison.OrdinalIgnoreCase))
                {
                    var moveError = AssetDatabase.MoveAsset(existingPath, scaledMatPath);
                    if (!string.IsNullOrEmpty(moveError))
                        Debug.LogWarning($"[ScaledSpaceBaker] Could not move scaledSpaceMaterial into Scaled/: {moveError}");
                }
                return AssetDatabase.LoadAssetAtPath<Material>(scaledMatPath) ?? existing;
            }

            // Fall back to a fresh material in case the artist hasn't assigned one yet.
            var rootMatPath = $"{ctx.BodyFolder}/{matName}";
            if (AssetDatabase.LoadAssetAtPath<Material>(rootMatPath) != null && !rootMatPath.Equals(scaledMatPath, StringComparison.OrdinalIgnoreCase))
                AssetDatabase.MoveAsset(rootMatPath, scaledMatPath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(scaledMatPath);
            if (mat == null)
            {
                var shader = Shader.Find(PlanetAuthoringShaders.Scaled);
                if (shader == null)
                    throw new InvalidOperationException($"Shader '{PlanetAuthoringShaders.Scaled}' not found.");
                mat = new Material(shader) { name = $"{ctx.BodyName}_Scaled" };
                AssetDatabase.CreateAsset(mat, scaledMatPath);
            }
            return mat;
        }

        private static string WirePrefab(string bodyFolder, string bodyName, Mesh mesh, Material mat)
        {
            var prefabPath = $"{bodyFolder}/Celestial.{bodyName}.Scaled.prefab";
            GameObject scratch = null;
            GameObject contents;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (existing == null)
            {
                scratch = new GameObject($"Celestial.{bodyName}.Scaled");
                contents = scratch;
            }
            else
            {
                contents = PrefabUtility.LoadPrefabContents(prefabPath);
            }

            try
            {
                // The renderer picks among the mesh's built-in LOD levels by screen size; no
                // LODGroup or child renderers needed. Tear down any leftover hierarchy from
                // earlier baker versions that DID use LODGroup + child GameObjects.
                CleanupLegacyLodHierarchy(contents);

                var meshFilter = contents.GetOrAddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                var renderer = contents.GetOrAddComponent<MeshRenderer>();
                renderer.sharedMaterial = mat;

                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                if (scratch != null) UnityEngine.Object.DestroyImmediate(scratch);
                else PrefabUtility.UnloadPrefabContents(contents);
            }
            return prefabPath;
        }

        private static void CleanupLegacyLodHierarchy(GameObject root)
        {
            var group = root.GetComponent<LODGroup>();
            if (group != null) UnityEngine.Object.DestroyImmediate(group, allowDestroyingAssets: true);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i);
                if (child.name == "LOD1" || child.name == "LOD2")
                    UnityEngine.Object.DestroyImmediate(child.gameObject, allowDestroyingAssets: true);
            }
        }

        private static string CompositeOceanAlbedo(Texture2D source, Texture2D heightMap, float threshold, Color oceanColor, string scaledFolder, string bodyName)
        {
            if (heightMap == null || !heightMap.isReadable)
            {
                Debug.LogWarning("[ScaledSpaceBaker] Ocean composite skipped: globalHeightMap is missing or not Read/Write enabled.");
                return null;
            }
            var srcPixels = TextureReadback.BlitReadPixels(source, out var w, out var h);
            var hmPixels = heightMap.GetPixels();
            var hmW = heightMap.width;
            var hmH = heightMap.height;

            for (int y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var hy = Mathf.Min(hmH - 1, Mathf.FloorToInt(v * (hmH - 1)));
                for (int x = 0; x < w; x++)
                {
                    var u = x / (float)(w - 1);
                    var hx = Mathf.Min(hmW - 1, Mathf.FloorToInt(u * (hmW - 1)));
                    if (hmPixels[hy * hmW + hx].r <= threshold)
                        srcPixels[y * w + x] = oceanColor;
                }
            }

            var output = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                output.SetPixels(srcPixels);
                output.Apply();
                var path = $"{scaledFolder}/{bodyName}_scaled_d.png";
                File.WriteAllBytes(path, output.EncodeToPNG());
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(output);
            }
        }

        private static void ConfigureAlbedoImporter(string projectPath)
        {
            var importer = AssetImporter.GetAtPath(projectPath) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
        }
    }
}
