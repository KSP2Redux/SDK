using System.Collections.Generic;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Packs per-biome and per-(biome, layer) source <see cref="Texture2D" /> assets from <see cref="PQSData" /> into <see cref="Texture2DArray" /> subassets and writes the resulting slice indices to the surface material.
    /// </summary>
    /// <remarks>
    /// The Texture2DArray slice-index abstraction stays out of the inspector for the artist's day-to-day flow:
    /// authors drag a <see cref="Texture2D" /> into a regular field and the packer keeps the array in sync.
    /// The packed arrays land as visible sub-assets on the PQSData so the artist can see what was baked.
    /// </remarks>
    public static class Texture2DArrayPacker
    {
        /// <summary>
        /// Material property and subasset name for the per-biome subzone normal Texture2DArray.
        /// </summary>
        public const string SubzoneNormalsArrayName = "_SubZonesNormalTextureArray";

        /// <summary>
        /// Material property and subasset name for the per-(biome, layer) small albedo Texture2DArray.
        /// </summary>
        public const string SmallAlbedoArrayName = "_SmallAlbedoArray";

        /// <summary>
        /// Material property and subasset name for the per-(biome, layer) small normal Texture2DArray.
        /// </summary>
        public const string SmallNormalArrayName = "_SmallNormalArray";

        /// <summary>
        /// Material property and subasset name for the per-(biome, layer) small metallic Texture2DArray.
        /// </summary>
        public const string SmallMetalArrayName = "_SmallMetalArray";

        // Texture-array creation defaults. The packer-owned arrays are always sampled with
        // these settings. Source Texture2D import settings on the cells don't apply because
        // we copy via Graphics.CopyTexture into a fresh array.
        private const int ArrayAnisoLevel = 8;
        private const FilterMode ArrayFilterMode = FilterMode.Trilinear;
        private const TextureWrapMode ArrayWrapMode = TextureWrapMode.Repeat;

        /// <summary>
        /// Result of a pack pass.
        /// </summary>
        /// <remarks>
        /// <see cref="ErrorMessage" /> is null on success and contains a human-readable
        /// description of why the pack was skipped on failure.
        /// </remarks>
        public readonly struct PackResult
        {
            /// <summary>
            /// True if the pack pass completed, false otherwise.
            /// </summary>
            public readonly bool Success;

            /// <summary>
            /// Human-readable error description when <see cref="Success" /> is false, otherwise null.
            /// </summary>
            public readonly string ErrorMessage;

            /// <summary>
            /// Creates a new <see cref="PackResult" /> with the given success flag and error message.
            /// </summary>
            /// <param name="success">True if the pack pass completed, false otherwise.</param>
            /// <param name="errorMessage">Description of the failure, or null on success.</param>
            public PackResult(bool success, string errorMessage)
            {
                Success = success;
                ErrorMessage = errorMessage;
            }

            /// <summary>
            /// Creates a successful <see cref="PackResult" />.
            /// </summary>
            /// <returns>A <see cref="PackResult" /> with <see cref="Success" /> set to true and a null <see cref="ErrorMessage" />.</returns>
            public static PackResult Ok() => new(true, null);

            /// <summary>
            /// Creates a failed <see cref="PackResult" /> carrying the given error message.
            /// </summary>
            /// <param name="message">The human-readable failure description.</param>
            /// <returns>A <see cref="PackResult" /> with <see cref="Success" /> set to false and the given <see cref="ErrorMessage" />.</returns>
            public static PackResult Fail(string message) => new(false, message);
        }

        /// <summary>
        /// Repacks the 8 per-biome subzone normal textures into a Texture2DArray subasset on <paramref name="pqsData" /> and pushes the array reference and slice indices to <paramref name="surfaceMaterial" />.
        /// </summary>
        /// <remarks>
        /// The 8 sources cover tier 3 (4 textures) and tier 4 (4 textures). Both tiers share a
        /// single <c>_SubZonesNormalTextureArray</c> on the material, with per-tier slice indices
        /// written into separate <c>_Subzone3NormalIndices</c> and <c>_Subzone4NormalIndices</c>
        /// vectors. Compact-packs only the assigned slots. Unassigned slots map to slice index -1,
        /// which the shader treats as "no tier-3/4 normal for this biome".
        /// </remarks>
        /// <param name="pqsData">The <see cref="PQSData" /> hosting the source textures and the array subasset.</param>
        /// <param name="surfaceMaterial">The surface material that receives the packed array reference and slice indices.</param>
        /// <returns>A successful <see cref="PackResult" /> on completion, or a failure result describing why the pack was skipped.</returns>
        public static PackResult RepackSubzoneNormals(PQSData pqsData, Material surfaceMaterial)
        {
            if (pqsData == null || surfaceMaterial == null)
                return PackResult.Fail("PQSData or surface material is null.");

            PQSDataAuthoring authoring = AuthoringSidecars.GetOrCreate(pqsData);
            if (authoring == null)
                return PackResult.Fail("Could not resolve PQSData authoring sidecar.");

            var sources = new Texture2D[8];
            for (var i = 0; i < 4; i++)
                sources[i] = (authoring.subzone3Normals != null && i < authoring.subzone3Normals.Length) ? authoring.subzone3Normals[i] : null;
            for (var i = 0; i < 4; i++)
                sources[4 + i] = (authoring.subzone4Normals != null && i < authoring.subzone4Normals.Length) ? authoring.subzone4Normals[i] : null;

            if (AllNull(sources))
                return PackResult.Ok();

            var packResult = PackIntoArray(pqsData, SubzoneNormalsArrayName, sources, out var indices);
            if (!packResult.Success) return packResult;

            Undo.RecordObject(surfaceMaterial, "Repack subzone normals");
            surfaceMaterial.SetTexture(
                SubzoneNormalsArrayName,
                FindArraySubasset(pqsData, SubzoneNormalsArrayName)
            );
            surfaceMaterial.SetVector(
                "_Subzone3NormalIndices",
                new Vector4(indices[0], indices[1], indices[2], indices[3])
            );
            surfaceMaterial.SetVector(
                "_Subzone4NormalIndices",
                new Vector4(indices[4], indices[5], indices[6], indices[7])
            );

            EditorUtility.SetDirty(surfaceMaterial);
            return PackResult.Ok();
        }

        /// <summary>
        /// Repacks the 16 per-(biome, layer) small-biome tile sources on <paramref name="pqsData" /> into the three parallel Texture2DArray subassets and pushes array references and per-biome slice indices to <paramref name="surfaceMaterial" />.
        /// </summary>
        /// <remarks>
        /// The three subassets are <c>_SmallAlbedoArray</c>, <c>_SmallNormalArray</c>, and
        /// <c>_SmallMetalArray</c>. The per-biome slice indices are written into the
        /// <c>_SmallBiomeR/G/B/A</c> material vectors. Cells use shared slice indices across
        /// the three arrays. A cell is active only when all three sources (albedo, normal, metal)
        /// are assigned. Mixed states such as albedo without normal fail with an actionable error
        /// so the artist can either fill the missing slot or clear the cell. Inactive cells get
        /// slice index -1.
        /// </remarks>
        /// <param name="pqsData">The <see cref="PQSData" /> hosting the source tile arrays and the array subassets.</param>
        /// <param name="surfaceMaterial">The surface material that receives the packed array references and per-biome slice indices.</param>
        /// <returns>A successful <see cref="PackResult" /> on completion, or a failure result describing why the pack was skipped.</returns>
        public static PackResult RepackSmallTiles(PQSData pqsData, Material surfaceMaterial)
        {
            if (pqsData == null || surfaceMaterial == null)
                return PackResult.Fail("PQSData or surface material is null.");

            PQSDataAuthoring authoring = AuthoringSidecars.GetOrCreate(pqsData);
            if (authoring == null)
                return PackResult.Fail("Could not resolve PQSData authoring sidecar.");

            var albedoSources = new Texture2D[16];
            var normalSources = new Texture2D[16];
            var metalSources = new Texture2D[16];
            for (var i = 0; i < 16; i++)
            {
                var slot = (authoring.smallLayerSlots != null && i < authoring.smallLayerSlots.Length) ? authoring.smallLayerSlots[i] : null;
                albedoSources[i] = slot?.EffectiveAlbedoTexture;
                normalSources[i] = slot?.EffectiveNormalTexture;
                metalSources[i] = slot?.EffectiveMetallicTexture;
            }

            for (var cell = 0; cell < 16; cell++)
            {
                var hasAlbedo = albedoSources[cell] != null;
                var hasNormal = normalSources[cell] != null;
                var hasMetal = metalSources[cell] != null;
                var assignedCount = (hasAlbedo ? 1 : 0) + (hasNormal ? 1 : 0) + (hasMetal ? 1 : 0);
                if (assignedCount > 0 && assignedCount < 3)
                {
                    var biome = PlanetAuthoringNaming.BiomeChannels[cell / 4];
                    var layer = (cell % 4) + 1;
                    var missing = new List<string>();
                    if (!hasAlbedo) missing.Add("albedo");
                    if (!hasNormal) missing.Add("normal");
                    if (!hasMetal) missing.Add("metal");
                    return PackResult.Fail(
                        $"Cell (Biome {biome}, Layer {layer}) is missing {string.Join(" + ", missing)}. " +
                        "Fill all three tile slots (albedo, normal, metal) or clear the cell entirely. " +
                        "Mixed assignments are not supported because the shader uses the same slice " +
                        "index across the three arrays."
                    );
                }
            }

            if (AllNull(albedoSources) && AllNull(normalSources) && AllNull(metalSources))
                return PackResult.Ok();

            var albedoResult = PackIntoArray(pqsData, SmallAlbedoArrayName, albedoSources, out var albedoIndices);
            if (!albedoResult.Success) return albedoResult;

            var normalResult = PackIntoArray(pqsData, SmallNormalArrayName, normalSources, out _);
            if (!normalResult.Success) return normalResult;

            var metalResult = PackIntoArray(pqsData, SmallMetalArrayName, metalSources, out _);
            if (!metalResult.Success) return metalResult;

            Undo.RecordObject(surfaceMaterial, "Repack small tiles");
            surfaceMaterial.SetTexture(SmallAlbedoArrayName, FindArraySubasset(pqsData, SmallAlbedoArrayName));
            surfaceMaterial.SetTexture(SmallNormalArrayName, FindArraySubasset(pqsData, SmallNormalArrayName));
            surfaceMaterial.SetTexture(SmallMetalArrayName, FindArraySubasset(pqsData, SmallMetalArrayName));

            for (var b = 0; b < 4; b++)
            {
                surfaceMaterial.SetVector(
                    "_SmallBiome" + PlanetAuthoringNaming.BiomeChannels[b],
                    new Vector4(albedoIndices[b * 4], albedoIndices[b * 4 + 1], albedoIndices[b * 4 + 2], albedoIndices[b * 4 + 3])
                );
            }

            EditorUtility.SetDirty(surfaceMaterial);

            authoring.LastSmallTilesPackFingerprint = ComputeSmallTilesFingerprint(authoring);
            EditorUtility.SetDirty(authoring);

            return PackResult.Ok();
        }

        /// <summary>
        /// Hashes the inputs that <see cref="RepackSmallTiles" /> consumes so the bake-drift validator can detect drift without re-running the pack.
        /// </summary>
        /// <param name="authoring">The PQSData authoring sidecar whose small-tile slots are hashed.</param>
        /// <returns>The hex form of the <see cref="Hash128" /> over the slot inputs.</returns>
        public static string ComputeSmallTilesFingerprint(PQSDataAuthoring authoring)
        {
            var hash = new Hash128();
            if (authoring?.smallLayerSlots == null)
                return hash.ToString();
            for (var i = 0; i < authoring.smallLayerSlots.Length; i++)
            {
                hash.Append(i);
                AppendTextureHash(ref hash, authoring.smallLayerSlots[i]?.EffectiveAlbedoTexture);
                AppendTextureHash(ref hash, authoring.smallLayerSlots[i]?.EffectiveNormalTexture);
                AppendTextureHash(ref hash, authoring.smallLayerSlots[i]?.EffectiveMetallicTexture);
            }
            return hash.ToString();
        }

        private static void AppendTextureHash(ref Hash128 hash, Texture2D tex)
        {
            if (tex == null)
            {
                hash.Append("none");
                return;
            }
            var path = AssetDatabase.GetAssetPath(tex);
            hash.Append(AssetDatabase.AssetPathToGUID(path));
            var importer = !string.IsNullOrEmpty(path) ? AssetImporter.GetAtPath(path) : null;
            if (importer != null)
                hash.Append((float)importer.assetTimeStamp);
        }

        // Packs sources into the named Texture2DArray subasset of pqsData, compact-packing only
        // the non-null sources. indices is filled with the resulting slice per source slot, or
        // -1 for null slots. If all sources are null the array subasset is destroyed.
        private static PackResult PackIntoArray(
            PQSData pqsData,
            string arrayName,
            Texture2D[] sources,
            out int[] indices
        )
        {
            indices = new int[sources.Length];
            for (var i = 0; i < indices.Length; i++) indices[i] = -1;

            var assigned = new List<(int slot, Texture2D tex)>();
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) assigned.Add((i, sources[i]));
            }

            if (assigned.Count == 0)
            {
                DestroyExistingArray(pqsData, arrayName);
                return PackResult.Ok();
            }

            var validation = ValidateSourceConsistency(assigned, arrayName, out var width, out var height, out var format, out var mipCount);
            if (!validation.Success) return validation;

            var array = PrepareArray(pqsData, arrayName, width, height, assigned.Count, format, mipCount);
            CopySlicesIntoArray(array, assigned, indices);

            EditorUtility.SetDirty(array);
            EditorUtility.SetDirty(pqsData);
            AssetDatabase.SaveAssetIfDirty(pqsData);
            return PackResult.Ok();
        }

        // Destroys the named array subasset (no-op if missing). allowDestroyingAssets is true
        // because Texture2DArray subassets are real assets - DestroyImmediate would refuse otherwise.
        private static void DestroyExistingArray(PQSData pqsData, string arrayName)
        {
            var existing = FindArraySubasset(pqsData, arrayName);
            if (existing == null) return;
            Object.DestroyImmediate(existing, true);
            EditorUtility.SetDirty(pqsData);
            AssetDatabase.SaveAssetIfDirty(pqsData);
        }

        private static PackResult ValidateSourceConsistency(
            List<(int slot, Texture2D tex)> assigned,
            string arrayName,
            out int width,
            out int height,
            out GraphicsFormat format,
            out int mipCount
        )
        {
            var first = assigned[0].tex;
            width = first.width;
            height = first.height;
            format = first.graphicsFormat;
            mipCount = first.mipmapCount;

            for (var i = 1; i < assigned.Count; i++)
            {
                var t = assigned[i].tex;
                if (t.width != width || t.height != height)
                    return PackResult.Fail(
                        $"Texture dimensions do not match in {arrayName}. {first.name} is " +
                        $"{width}x{height}, {t.name} is {t.width}x{t.height}. All assigned " +
                        "textures in this array must share the same width, height, format, and mip count."
                    );
                if (t.graphicsFormat != format)
                    return PackResult.Fail(
                        $"Texture formats do not match in {arrayName}. {first.name} is {format}, " +
                        $"{t.name} is {t.graphicsFormat}. All assigned textures in this array " +
                        "must share the same graphics format."
                    );
                if (t.mipmapCount != mipCount)
                    return PackResult.Fail(
                        $"Mipmap counts do not match in {arrayName}. {first.name} has {mipCount} " +
                        $"mips, {t.name} has {t.mipmapCount}."
                    );
            }
            return PackResult.Ok();
        }

        // Returns the existing array if its dimensions/format/depth/mipCount match, otherwise
        // destroys it and creates a fresh one as a subasset of pqsData.
        private static Texture2DArray PrepareArray(
            PQSData pqsData,
            string arrayName,
            int width,
            int height,
            int sliceCount,
            GraphicsFormat format,
            int mipCount
        )
        {
            var array = FindArraySubasset(pqsData, arrayName);
            var canReuse = array != null
                && array.width == width
                && array.height == height
                && array.graphicsFormat == format
                && array.depth == sliceCount
                && array.mipmapCount == mipCount;

            if (canReuse)
                return array;

            if (array != null)
                Object.DestroyImmediate(array, true);

            var flags = mipCount > 1 ? TextureCreationFlags.MipChain : TextureCreationFlags.None;
            array = new Texture2DArray(width, height, sliceCount, format, flags, mipCount)
            {
                name = arrayName,
                wrapMode = ArrayWrapMode,
                filterMode = ArrayFilterMode,
                anisoLevel = ArrayAnisoLevel,
            };
            AssetDatabase.AddObjectToAsset(array, pqsData);
            return array;
        }

        private static void CopySlicesIntoArray(
            Texture2DArray array,
            List<(int slot, Texture2D tex)> assigned,
            int[] indices
        )
        {
            int mipCount = array.mipmapCount;
            for (var i = 0; i < assigned.Count; i++)
            {
                var (slot, tex) = assigned[i];
                for (var m = 0; m < mipCount; m++)
                    Graphics.CopyTexture(tex, 0, m, array, i, m);
                indices[slot] = i;
            }
        }

        private static Texture2DArray FindArraySubasset(PQSData pqsData, string arrayName)
        {
            var path = AssetDatabase.GetAssetPath(pqsData);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub is Texture2DArray a && a.name == arrayName) return a;
            }
            return null;
        }

        private static bool AllNull(Texture2D[] sources)
        {
            if (sources == null) return true;
            foreach (var t in sources)
                if (t != null) return false;
            return true;
        }
    }
}
