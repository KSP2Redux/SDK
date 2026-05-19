using System;
using System.IO;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Orchestrator that bakes a body's full scaled-space surface cascade and wires the resulting prefab.
    /// </summary>
    /// <remarks>
    /// Produces per-biome Mid and Large normal maps from the small-tile stack and the equirect gradience
    /// heightmaps, an analytic equirect albedo / normal / packed / emission set, and a low-poly displaced
    /// sphere mesh, then points <c>Celestial.&lt;Body&gt;.Scaled.prefab</c> at the resulting mesh and material.
    /// Operates purely against asset references on the body and its PQS, so no live authoring session or
    /// open scene is required.
    /// </remarks>
    public static class BodySurfaceBakerOperation
    {
        /// <summary>
        /// Authored radius (in mesh units) of the scaled-space mesh.
        /// </summary>
        /// <remarks>
        /// The wizard sizes the SphereCollider to this value, and
        /// <c>ScaledPlanetaryBodyView.CalculateNormalizationFactorFromCollider</c> uses the
        /// collider's bounds as <c>baseSizeFactor</c>, so the runtime scales the mesh by
        /// <c>body.radius * 2 / (2 * AuthoredRadius) = body.radius / AuthoredRadius</c>. The mesh
        /// baker writes vertex distances of
        /// <c>AuthoredRadius + h * heightScale * (AuthoredRadius / body.radius)</c>, so a vertex at
        /// the body's nominal radius (h = 0) lands at world radius <c>body.radius</c> after scaling.
        /// </remarks>
        public const float AuthoredRadius = 1000f;

        /// <summary>
        /// Per-bake settings.
        /// </summary>
        public struct Settings
        {
            /// <summary>
            /// Mesh resolution index where 0=64x32, 1=128x64, 2=256x128, 3=512x256.
            /// </summary>
            public int MeshResolutionIndex;
            /// <summary>
            /// When true, ocean color is composited into the scaled albedo and terrain is clamped to sea level.
            /// </summary>
            public bool IncludeOcean;
            /// <summary>
            /// Color written over ocean pixels in the scaled albedo when <see cref="IncludeOcean" /> is true.
            /// </summary>
            public Color OceanColor;
        }

        /// <summary>
        /// Result of a bake attempt.
        /// </summary>
        public struct Result
        {
            /// <summary>
            /// True when the bake completed end to end.
            /// </summary>
            public bool Success;
            /// <summary>
            /// Error message when <see cref="Success" /> is false.
            /// </summary>
            public string Error;
            /// <summary>
            /// Asset path of the wired Scaled prefab on success.
            /// </summary>
            public string PrefabPath;
            /// <summary>
            /// Folder the bake outputs were written into on success.
            /// </summary>
            public string ScaledFolder;
        }

        /// <summary>
        /// Executes the bake pipeline against <paramref name="body" /> with <paramref name="settings" />.
        /// </summary>
        /// <param name="body">The body whose scaled view to bake. Must have a non-empty bodyName and positive radius.</param>
        /// <param name="settings">Per-bake settings.</param>
        /// <returns>A <see cref="Result" /> describing success or failure.</returns>
        public static Result Bake(CoreCelestialBodyData body, Settings settings)
        {
            if (!TryPrepareContext(body, settings, out var ctx, out var error))
                return Fail(error);

            try
            {
                // Per-biome Mid and Large normals bake first. Their outputs feed the surface
                // material's _MidNormal*/_LargeNormal* slots; the existing scaled bake below is
                // unchanged in scope and runs independently.
                ProgressBar("Baking per-biome mid/large normals...", 0.05f);
                var biomeBake = BiomeNormalBaker.Bake(ctx.PqsData, ctx.Radius);
                if (!biomeBake.Skipped)
                {
                    ProgressBar("Writing per-biome normals...", 0.10f);
                    WriteAndBindBiomeNormals(ctx, biomeBake);
                }
                else
                {
                    Debug.Log($"[BodySurfaceBaker] Per-biome normal bake skipped: {biomeBake.SkipReason}");
                }

                ProgressBar("Baking textures (analytic)...", 0.15f);
                var textures = AnalyticScaledSpaceSampler.Sample(ctx.PqsData, ctx.Radius, AnalyticScaledSpaceSampler.DefaultSettings());
                try
                {
                    ProgressBar("Compositing ocean / polar blend...", 0.45f);
                    ApplyOptionalOceanInPlace(ctx, textures.Albedo);
                    AnalyticScaledSpaceSampler.BlendPolarNormals(textures.Normal, totalLines: 32, blendLines: 24);
                    AnalyticScaledSpaceSampler.BlendPolarRowsTowardRowMean(textures.Albedo,   totalLines: 32, blendLines: 24);
                    AnalyticScaledSpaceSampler.BlendPolarRowsTowardRowMean(textures.Packed,   totalLines: 32, blendLines: 24);
                    AnalyticScaledSpaceSampler.BlendPolarRowsTowardRowMean(textures.Emission, totalLines: 32, blendLines: 24);
                    AnalyticScaledSpaceSampler.EnforceSeamTileability(textures.Albedo);
                    AnalyticScaledSpaceSampler.EnforceSeamTileability(textures.Normal);
                    AnalyticScaledSpaceSampler.EnforceSeamTileability(textures.Packed);
                    AnalyticScaledSpaceSampler.EnforceSeamTileability(textures.Emission);

                    ProgressBar("Writing scaled-space textures...", 0.6f);
                    var albedoAsset = WriteAndImportPng(textures.Albedo,   $"{ctx.ScaledFolder}/{ctx.BodyName}_scaled_d.png",  ConfigureSrgbImporter);
                    var normalAsset = WriteAndImportPng(textures.Normal,   $"{ctx.ScaledFolder}/{ctx.BodyName}_scaled_n.png",  ConfigureNormalImporter);
                    var packedAsset = WriteAndImportPng(textures.Packed,   $"{ctx.ScaledFolder}/{ctx.BodyName}_scaled_pk.png", ConfigureLinearImporter);
                    var emissionAsset = WriteAndImportPng(textures.Emission, $"{ctx.ScaledFolder}/{ctx.BodyName}_scaled_e.png",  ConfigureSrgbImporter);

                    ProgressBar("Resolving material...", 0.7f);
                    var matAsset = ResolveOrCreateMaterial(ctx);
                    BindBakedTexturesToMaterial(matAsset, albedoAsset, normalAsset, packedAsset, emissionAsset);

                    // Same outputs also feed the PQS surface material's scaled-tex slots so the
                    // local-view distance crossfade samples the baked maps.
                    BindBakedTexturesToSurfaceMaterial(ctx.PqsData, albedoAsset, normalAsset, packedAsset, emissionAsset);

                    ProgressBar("Baking mesh...", 0.8f);
                    var meshAsset = BakeLodMesh(ctx);

                    FlushBeforePrefabWiring(ref meshAsset, ref matAsset);

                    ProgressBar("Wiring prefab...", 0.9f);
                    var prefabPath = WirePrefab(ctx.BodyFolder, ctx.BodyName, meshAsset, matAsset);

                    FinalizeImports(meshAsset, prefabPath);

                    Debug.Log($"[BodySurfaceBaker] Wrote mesh+material+textures to '{ctx.ScaledFolder}/' and wired prefab '{prefabPath}'.");
                    return Succeed(prefabPath, ctx.ScaledFolder);
                }
                finally
                {
                    if (textures.Albedo != null)   UnityEngine.Object.DestroyImmediate(textures.Albedo);
                    if (textures.Normal != null)   UnityEngine.Object.DestroyImmediate(textures.Normal);
                    if (textures.Packed != null)   UnityEngine.Object.DestroyImmediate(textures.Packed);
                    if (textures.Emission != null) UnityEngine.Object.DestroyImmediate(textures.Emission);
                }
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

            var hmInfoCheck = pqsData.heightMapInfo;
            if (hmInfoCheck == null) { error = "PQSData has no heightMapInfo. Open the PQS inspector and assign a global heightmap before baking."; return false; }

            var heightScale = hmInfoCheck.heightMapScale;
            var hasOcean = (body.Data?.hasOcean ?? false) && settings.IncludeOcean;
            var oceanAltitude = (float)(body.Data?.oceanAltitude ?? 0);
            // Heightmap is normalized [0,1] mapped to altitudes [0, heightScale] above the body
            // radius (h * heightScale - matches PQSJobUtil.HeightSample). Ocean sits at altitude
            // oceanAltitude, so normalized = oceanAltitude / heightScale.
            var oceanNormalized = hasOcean && heightScale > 0 ? oceanAltitude / heightScale : -1f;

            var hmInfo = hmInfoCheck;
            ctx = new BakeContext
            {
                BodyName = bodyName,
                Radius = radius,
                PqsData = pqsData,
                BodyFolder = bodyFolder,
                ScaledFolder = scaledFolder,
                GlobalHeightMap = hmInfo?.globalHeightMap,
                HeightScale = heightScale,
                HasOcean = hasOcean,
                OceanNormalized = oceanNormalized,
                OceanColor = settings.OceanColor,
                MeshResolutionIndex = settings.MeshResolutionIndex,
            };
            error = null;
            return true;
        }

        // Composites ocean color over the analytic-baked albedo where the global heightmap is below
        // sea level. Mutates the in-memory texture before it is written to disk; no PNG round-trip.
        private static void ApplyOptionalOceanInPlace(BakeContext ctx, Texture2D albedo)
        {
            if (!ctx.HasOcean || albedo == null) return;
            if (ctx.GlobalHeightMap == null || !ctx.GlobalHeightMap.isReadable)
            {
                Debug.LogWarning("[BodySurfaceBaker] Ocean composite skipped: globalHeightMap is missing or not Read/Write enabled.");
                return;
            }

            var albedoPixels = albedo.GetPixels();
            var hmPixels = ctx.GlobalHeightMap.GetPixels();
            int w = albedo.width, h = albedo.height;
            int hmW = ctx.GlobalHeightMap.width, hmH = ctx.GlobalHeightMap.height;
            float threshold = ctx.OceanNormalized;
            var oceanColor = ctx.OceanColor;

            for (int y = 0; y < h; y++)
            {
                var v = y / (float)(h - 1);
                var hy = Mathf.Min(hmH - 1, Mathf.FloorToInt(v * (hmH - 1)));
                for (int x = 0; x < w; x++)
                {
                    var u = x / (float)(w - 1);
                    var hx = Mathf.Min(hmW - 1, Mathf.FloorToInt(u * (hmW - 1)));
                    if (hmPixels[hy * hmW + hx].r <= threshold)
                        albedoPixels[y * w + x] = oceanColor;
                }
            }

            albedo.SetPixels(albedoPixels);
            albedo.Apply();
        }

        private static void BindBakedTexturesToMaterial(Material matAsset, Texture2D albedo, Texture2D normal, Texture2D packed, Texture2D emission)
        {
            matAsset.SetTexture(Shader.PropertyToID("_MainTex"), albedo);
            matAsset.SetTexture(Shader.PropertyToID("_NormalMap"), normal);
            matAsset.SetTexture(Shader.PropertyToID("_PackedMap"), packed);
            matAsset.SetTexture(Shader.PropertyToID("_EmissionTex"), emission);
            EditorUtility.SetDirty(matAsset);
        }

        // Wires the same baked outputs into the PQS surface material's _AlbedoScaledTex /
        // _NormalScaledTex / _PackedScaledTex / _EmissionScaledTex slots so the local-view
        // distance crossfade samples them at orbit-equivalent ranges.
        private static void BindBakedTexturesToSurfaceMaterial(PQSData pqsData, Texture2D albedo, Texture2D normal, Texture2D packed, Texture2D emission)
        {
            var surfaceMat = pqsData?.materialSettings?.surfaceMaterial;
            if (surfaceMat == null) return;
            surfaceMat.SetTexture(Shader.PropertyToID("_AlbedoScaledTex"),  albedo);
            surfaceMat.SetTexture(Shader.PropertyToID("_NormalScaledTex"), normal);
            surfaceMat.SetTexture(Shader.PropertyToID("_PackedScaledTex"), packed);
            surfaceMat.SetTexture(Shader.PropertyToID("_EmissionScaledTex"), emission);
            EditorUtility.SetDirty(surfaceMat);
        }

        // Writes the 8 per-biome normal PNGs (4 mid + 4 large) into BiomeNormals/ next to the
        // body and binds them onto the surface material's _MidNormal*/_LargeNormal* slots. Null
        // entries in the result (empty biomes) are skipped - their slots keep whatever artist-
        // authored binding was there. The temp in-memory textures get disposed after writing.
        private static void WriteAndBindBiomeNormals(BakeContext ctx, BiomeNormalBaker.Result biomeBake)
        {
            string biomeFolder = ctx.BodyFolder + "/BiomeNormals";
            if (!AssetDatabase.IsValidFolder(biomeFolder))
                AssetDatabase.CreateFolder(ctx.BodyFolder, "BiomeNormals");

            var surfaceMat = ctx.PqsData?.materialSettings?.surfaceMaterial;
            var biomeNames = PlanetAuthoringNaming.BiomeChannels;

            try
            {
                if (biomeBake.MidPerBiome != null)
                {
                    for (int b = 0; b < 4; b++)
                    {
                        if (biomeBake.MidPerBiome[b] == null) continue;
                        string path = $"{biomeFolder}/{ctx.BodyName}_MidNormal{biomeNames[b]}.png";
                        var asset = WriteAndImportPng(biomeBake.MidPerBiome[b], path, ConfigureNormalImporter);
                        if (surfaceMat != null && asset != null)
                            surfaceMat.SetTexture(Shader.PropertyToID($"_MidNormal{biomeNames[b]}"), asset);
                    }
                }
                if (biomeBake.LargePerBiome != null)
                {
                    // Match the runtime tile rate to the bake's authoring rate. The bake produces
                    // one heightmap-tile's worth of content per output tile, so the runtime needs
                    // (heightmapUvScale/2, heightmapUvScale, 0, 0) to make the face-UV sampler
                    // repeat at the same equirect rate. The /2 accounts for faceUV = (2*uv.x, uv.y).
                    var hmi = ctx.PqsData?.heightMapInfo;
                    for (int b = 0; b < 4; b++)
                    {
                        if (biomeBake.LargePerBiome[b] == null) continue;
                        string path = $"{biomeFolder}/{ctx.BodyName}_LargeNormal{biomeNames[b]}.png";
                        var asset = WriteAndImportPng(biomeBake.LargePerBiome[b], path, ConfigureNormalImporter);
                        if (surfaceMat != null && asset != null)
                        {
                            surfaceMat.SetTexture(Shader.PropertyToID($"_LargeNormal{biomeNames[b]}"), asset);

                            float uvScale = BiomeNormalBaker.LargeUvScaleForBiome(hmi, b);
                            surfaceMat.SetVector(
                                Shader.PropertyToID($"_LargeNormal{biomeNames[b]}UVParams"),
                                new Vector4(uvScale * 0.5f, uvScale, 0f, 0f));
                        }
                    }
                }
                if (surfaceMat != null)
                    EditorUtility.SetDirty(surfaceMat);
            }
            finally
            {
                if (biomeBake.MidPerBiome != null)
                {
                    foreach (var t in biomeBake.MidPerBiome)
                        if (t != null) UnityEngine.Object.DestroyImmediate(t);
                }
                if (biomeBake.LargePerBiome != null)
                {
                    foreach (var t in biomeBake.LargePerBiome)
                        if (t != null) UnityEngine.Object.DestroyImmediate(t);
                }
            }
        }

        // Encodes a Texture2D to PNG, writes to the project, imports with the supplied importer
        // configuration, and returns the imported asset.
        private static Texture2D WriteAndImportPng(Texture2D source, string projectPath, Action<TextureImporter> configureImporter)
        {
            var bytes = source.EncodeToPNG();
            File.WriteAllBytes(projectPath, bytes);
            AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(projectPath) is TextureImporter importer)
            {
                configureImporter(importer);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(projectPath);
        }

        private static void ConfigureSrgbImporter(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
        }

        private static void ConfigureLinearImporter(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
        }

        private static void ConfigureNormalImporter(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.maxTextureSize = 4096;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
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

        private static void ProgressBar(string step, float progress) => EditorUtility.DisplayProgressBar("Body Surface Bake", step, progress);
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
            BuildSphereVertices(baseLon, baseLatBuild, ctx, out var verts, out var normals, out var uvs, out var tangents);

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
            mainMesh.SetTangents(tangents);
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

            if (created)
                AssetDatabase.CreateAsset(mainMesh, path);
            else
                EditorUtility.SetDirty(mainMesh);
            return mainMesh;
        }

        private static void BuildSphereVertices(int lonDivisions, int latDivisions, BakeContext ctx,
            out Vector3[] verts, out Vector3[] normals, out Vector2[] uvs, out Vector4[] tangents)
        {
            // Mesh displacement uses the global heightmap only - per-biome large + mid gradience
            // sit below the scaled mesh's vertex spacing on most bodies and would only contribute
            // sub-vertex noise to the silhouette. The analytic albedo bake still folds them into
            // the macro normal and AO so artist-authored gradience features stay visible at
            // distance through shading rather than geometry.
            var globalPixels = TryReadPixels(ctx.GlobalHeightMap, out var hmGW, out var hmGH);

            float metersToMesh = ctx.Radius > 0f ? AuthoredRadius / ctx.Radius : 0f;

            var vertCount = (lonDivisions + 1) * (latDivisions + 1);
            verts = new Vector3[vertCount];
            normals = new Vector3[vertCount];
            uvs = new Vector2[vertCount];
            tangents = new Vector4[vertCount];

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
                    var sinPhi = Mathf.Sin(phi);
                    var cosPhi = Mathf.Cos(phi);
                    var dir = new Vector3(sinTheta * cosPhi, cosTheta, sinTheta * sinPhi);
                    var displacement = 0f;
                    if (globalPixels != null && metersToMesh > 0f)
                    {
                        var sampleU = u;
                        var sampleV = 1f - v;
                        var globalH = SampleBilinear(globalPixels, hmGW, hmGH, sampleU, sampleV);
                        if (ctx.OceanNormalized >= 0f && globalH < ctx.OceanNormalized)
                            globalH = ctx.OceanNormalized;

                        displacement = globalH * ctx.HeightScale * metersToMesh;
                    }
                    var i = y * (lonDivisions + 1) + x;
                    verts[i] = dir * (AuthoredRadius + displacement);
                    normals[i] = dir;
                    uvs[i] = new Vector2(u, 1f - v);
                    // Analytic +U tangent. For phi = u * 2pi the unit east vector is (-sin(phi), 0, cos(phi))
                    // at every theta. Continuous across u=0 / u=1, so the seam doesn't drop a tangent
                    // discontinuity that Mesh.RecalculateTangents would otherwise produce from the UV jump.
                    tangents[i] = new Vector4(-sinPhi, 0f, cosPhi, 1f);
                }
            }
        }

        private static Color[] TryReadPixels(Texture2D tex, out int w, out int h)
        {
            if (tex == null || !tex.isReadable)
            {
                w = 0; h = 0;
                return null;
            }
            w = tex.width;
            h = tex.height;
            return tex.GetPixels();
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
                        Debug.LogWarning($"[BodySurfaceBaker] Could not move scaledSpaceMaterial into Scaled/: {moveError}");
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

                // Force the SphereCollider radius to AuthoredRadius. ScaledPlanetaryBodyView's
                // baseSizeFactor reads the collider's bounds, so any drift between what's saved
                // in the prefab and AuthoredRadius mis-scales the body at runtime.
                var sphereCollider = contents.GetOrAddComponent<SphereCollider>();
                if (!Mathf.Approximately(sphereCollider.radius, AuthoredRadius))
                    sphereCollider.radius = AuthoredRadius;

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

    }
}
