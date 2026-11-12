using System.Collections.Generic;
using System.IO;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Biomes
{
    /// <summary>
    /// Bakes a <see cref="BiomeLookupHashTable" /> from an authored grid or from the body's terrain mask and channel mappings.
    /// </summary>
    /// <remarks>
    /// A direct grid carries the original color indices. The terrain mask path assigns color
    /// index zero and adds a stub palette when the body has no palette.
    /// </remarks>
    internal static class BiomeLookupBaker
    {
        /// <summary>
        /// Filename suffix appended to the body key for the baked hash table asset.
        /// </summary>
        public const string HashTableSuffix = "_biome_lookup";

        /// <summary>
        /// Filename suffix appended to the body key for the stub color LUT asset.
        /// </summary>
        public const string ColorLutSuffix = "_biome_color_lut";

        private const int HashTableDimension = BiomeLookupHashTable.BiomeSamplingHashTableDimension;
        private const int CellDimension = BiomeLookupHashTable.CellDimension;
        private const int GRID_DIMENSION = HashTableDimension * CellDimension;
        private const ushort COVERED = 0x8000;
        private const ushort RESERVED_BITS = 0x7800;

        /// <summary>
        /// Outcome of a <see cref="Bake" /> call.
        /// </summary>
        public readonly struct BakeResult
        {
            /// <summary>
            /// True if the bake produced a hash table, false if validation failed.
            /// </summary>
            public readonly bool Success;

            /// <summary>
            /// Asset path of the written hash table on success, or null on failure.
            /// </summary>
            public readonly string HashTablePath;

            /// <summary>
            /// Human-readable status message describing the outcome.
            /// </summary>
            public readonly string Message;

            /// <summary>
            /// Creates a new <see cref="BakeResult" />.
            /// </summary>
            /// <param name="success">True if the bake produced a hash table.</param>
            /// <param name="hashTablePath">Asset path of the written hash table, or null on failure.</param>
            /// <param name="message">Human-readable status message.</param>
            public BakeResult(bool success, string hashTablePath, string message)
            {
                Success = success;
                HashTablePath = hashTablePath;
                Message = message;
            }
        }

        /// <summary>
        /// Validates inputs and runs the bake.
        /// </summary>
        /// <remarks>
        /// Updates the explicitly bound lookup asset in place or writes a new subasset on PQSData.
        /// </remarks>
        /// <param name="data">The PQSData asset whose biome lookup is baked.</param>
        /// <returns>A <see cref="BakeResult" /> describing the outcome.</returns>
        public static BakeResult Bake(PQSData data)
        {
            if (data == null) return Fail("PQSData is null.");

            PQSDataAuthoring sidecar = AuthoringSidecars.Find(data);
            if (sidecar != null && sidecar.BiomeLookupSource != null)
                return BakeGrid(data, sidecar.BiomeLookupSource, sidecar.BiomeLookupBakeTarget);

            var biomeMask = data.heightMapInfo?.mask;
            if (biomeMask == null) return Fail("Biome mask texture is unassigned. Set _BiomeMaskTex / heightMapInfo.mask before baking.");
            if (!biomeMask.isReadable) return Fail($"Biome mask '{biomeMask.name}' is not Read/Write enabled. Toggle it in the texture importer.");

            var subzones = data.heightMapInfo.subZonesEnabled;
            var subzoneMask = data.heightMapInfo.subZoneMask;
            if (subzones)
            {
                if (subzoneMask == null) return Fail("Subzones are enabled but the subzone mask texture is unassigned.");
                if (!subzoneMask.isReadable) return Fail($"Subzone mask '{subzoneMask.name}' is not Read/Write enabled.");
            }

            if (sidecar == null)
                sidecar = AuthoringSidecars.GetOrCreate(data);
            if (sidecar == null) return Fail("Could not resolve the PQSDataAuthoring sidecar for this PQSData.");

            var mappedChannels = 0;
            foreach (var mapping in sidecar.biomeChannelMapping)
            {
                if (mapping != PQSData.KSP2BiomeType.NONE)
                    mappedChannels++;
            }
            if (mappedChannels == 0) return Fail("No biome channels are mapped to a KSP2BiomeType. Bake would emit an empty hash table.");

            var hashTable = BuildHashTable(biomeMask, subzones ? subzoneMask : null, sidecar, subzones, default);
            return SaveSubAsset(data, hashTable, addStubPalette: true);
        }

        private static BakeResult BakeGrid(PQSData data, Texture2D source, BiomeLookupHashTable target)
        {
            if (source.width != GRID_DIMENSION || source.height != GRID_DIMENSION || source.format != TextureFormat.R16
                || source.isDataSRGB || !source.isReadable || source.mipmapCount != 1)
                return Fail($"The biome grid must be a readable linear {GRID_DIMENSION} by {GRID_DIMENSION} R16 texture with no mipmaps.");

            string targetPath = target == null ? string.Empty : AssetDatabase.GetAssetPath(target);
            if (target != null && (target != data.PlanetBiomeHashTable || string.IsNullOrEmpty(targetPath)))
                return Fail("The biome bake target must be the saved lookup table assigned to this body.");
            if (target == null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(data)))
                return Fail("Save the PQSData asset before baking a biome grid.");

            var words = source.GetPixelData<ushort>(0);
            if (words.Length != GRID_DIMENSION * GRID_DIMENSION)
                return Fail("The biome grid pixel buffer does not match its dimensions.");

            for (int i = 0; i < words.Length; i++)
            {
                ushort word = words[i];
                if (word != 0 && ((word & COVERED) == 0 || (word & RESERVED_BITS) != 0))
                    return Fail($"The biome grid has an invalid value at pixel {i}.");
            }

            var hashTable = BuildHashTable(null, null, null, false, words);
            if (target == null)
                return SaveSubAsset(data, hashTable, addStubPalette: false);

            target.Cells = hashTable.Cells;
            Object.DestroyImmediate(hashTable);
            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssetIfDirty(target);
            return new BakeResult(true, targetPath, $"Baked biome lookup into {targetPath}");
        }

        private static BakeResult SaveSubAsset(PQSData data, BiomeLookupHashTable hashTable, bool addStubPalette)
        {
            string bodyKey = ResolveBodyKey(data);
            string pqsDataPath = AssetDatabase.GetAssetPath(data);
            data.PlanetBiomeHashTable = WriteHashTableAsSubAsset(hashTable, data, bodyKey);
            if (addStubPalette && data.BiomeColorLookupTable == null)
                data.BiomeColorLookupTable = WriteStubColorLutAsSubAsset(data, bodyKey);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            AssetDatabase.Refresh();

            return new BakeResult(true, pqsDataPath, $"Baked biome lookup into {pqsDataPath}");
        }

        private static BakeResult Fail(string message)
        {
            Debug.LogWarning($"[BiomeLookupBaker] {message}");
            return new BakeResult(false, null, message);
        }

        // Body key matches the science region baker's output convention: the parent folder name
        // (the wizard creates a folder per body using the body's display name) lowercased. Falls
        // back to the asset name with _PQS / PQSData suffixes stripped if the folder isn't useful
        // (e.g. PQSData stored at the project root during ad-hoc testing).
        private static string ResolveBodyKey(PQSData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            if (!string.IsNullOrEmpty(path))
            {
                var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
                var folderName = Path.GetFileName(folder);
                if (!string.IsNullOrEmpty(folderName) && folderName != "Assets")
                {
                    return folderName.ToLowerInvariant();
                }
            }

            var assetName = data.name ?? string.Empty;
            if (assetName.EndsWith("PQSData", System.StringComparison.OrdinalIgnoreCase))
            {
                assetName = assetName.Substring(0, assetName.Length - "PQSData".Length);
            }
            else if (assetName.EndsWith("_PQS", System.StringComparison.OrdinalIgnoreCase))
            {
                assetName = assetName.Substring(0, assetName.Length - "_PQS".Length);
            }
            return assetName.ToLowerInvariant();
        }

        private static BiomeLookupHashTable BuildHashTable(
            Texture2D biomeMask, Texture2D subzoneMask, PQSDataAuthoring sidecar, bool subzones, NativeArray<ushort> words)
        {
            var hashTable = ScriptableObject.CreateInstance<BiomeLookupHashTable>();
            var total = HashTableDimension * HashTableDimension;

            // Reuse scratch space across cells and copy each cell's packed chunks once.
            var subCellWords = new ushort[CellDimension, CellDimension];
            var claimed = new bool[CellDimension, CellDimension];
            var packBuffer = new List<int>(CellDimension * CellDimension);

            for (var cellIndex = 0; cellIndex < total; cellIndex++)
            {
                var cellX = cellIndex % HashTableDimension;
                var cellY = cellIndex / HashTableDimension;
                if (words.IsCreated)
                    CopySubCellGrid(subCellWords, words, cellX, cellY);
                else
                    FillSubCellGrid(subCellWords, biomeMask, subzoneMask, sidecar, subzones, cellX, cellY);
                packBuffer.Clear();
                System.Array.Clear(claimed, 0, claimed.Length);
                PartitionAndPackInto(subCellWords, claimed, packBuffer);
                hashTable.Cells[cellIndex] = new BiomeLookupHashCell
                {
                    BiomeChunks = new List<int>(packBuffer),
                };
            }
            return hashTable;
        }

        private static void FillSubCellGrid(
            ushort[,] dst,
            Texture2D biomeMask,
            Texture2D subzoneMask,
            PQSDataAuthoring sidecar,
            bool subzones,
            int cellX, int cellY)
        {
            for (var sy = 0; sy < CellDimension; sy++)
            for (var sx = 0; sx < CellDimension; sx++)
            {
                var u = (cellX * CellDimension + sx + 0.5f) / (HashTableDimension * CellDimension);
                var v = (cellY * CellDimension + sy + 0.5f) / (HashTableDimension * CellDimension);
                var biome = DominantChannel(biomeMask.GetPixelBilinear(u, v));
                var type = sidecar.biomeChannelMapping[biome];
                if (subzones)
                {
                    var subzone = DominantChannel(subzoneMask.GetPixelBilinear(u, v));
                    var subzoneType = sidecar.biomeSubzoneMapping[PlanetAuthoringNaming.CellIndex(biome, subzone)];
                    if (subzoneType != PQSData.KSP2BiomeType.NONE)
                    {
                        type = subzoneType;
                    }
                }
                dst[sy, sx] = (ushort)(COVERED | (int)type);
            }
        }

        private static void CopySubCellGrid(ushort[,] destination, NativeArray<ushort> words, int cellX, int cellY)
        {
            for (int y = 0; y < CellDimension; y++)
            {
                int rowStart = (cellY * CellDimension + y) * GRID_DIMENSION + cellX * CellDimension;
                for (int x = 0; x < CellDimension; x++)
                {
                    destination[y, x] = words[rowStart + x];
                }
            }
        }

        private static int DominantChannel(Color c)
        {
            float r = c.r, g = c.g, b = c.b, a = c.a;
            var idx = 0;
            var max = r;
            if (g > max) { max = g; idx = 1; }
            if (b > max) { max = b; idx = 2; }
            if (a > max) { idx = 3; }
            return idx;
        }

        // Greedy axis-aligned rectangle partition of the 16x16 type grid. Picks the topmost-leftmost
        // unclaimed cell, extends right while the type matches, then extends down while every row
        // in the candidate rectangle matches. Mirrors the legacy utility's algorithm so the chunk
        // count per cell stays comparable.
        private static void PartitionAndPackInto(ushort[,] grid, bool[,] claimed, List<int> packed)
        {
            for (var y = 0; y < CellDimension; y++)
            for (var x = 0; x < CellDimension; x++)
            {
                ushort word = grid[y, x];
                if (claimed[y, x] || word == 0) continue;
                var maxX = x;
                while (maxX + 1 < CellDimension && !claimed[y, maxX + 1] && grid[y, maxX + 1] == word) maxX++;
                var maxY = y;
                while (maxY + 1 < CellDimension && RowMatches(grid, claimed, word, x, maxX, maxY + 1)) maxY++;
                for (var yy = y; yy <= maxY; yy++)
                {
                    for (var xx = x; xx <= maxX; xx++)
                    {
                        claimed[yy, xx] = true;
                    }
                }
                packed.Add(BiomeLookupHashTable.PackBiomeChunkData(x, maxX, y, maxY, word & 31, (word >> 5) & 63));
            }
        }

        private static bool RowMatches(ushort[,] grid, bool[,] claimed, ushort word, int x0, int x1, int y)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (claimed[y, x] || grid[y, x] != word) return false;
            }
            return true;
        }

        // Stored as a sub-asset of PQSData so the bundle includes it automatically through PQSData's
        // PlanetBiomeHashTable field. Nothing in the runtime loads it by addressables label or key,
        // so it doesn't need a standalone GUID.
        private static BiomeLookupHashTable WriteHashTableAsSubAsset(BiomeLookupHashTable hashTable, PQSData host, string bodyKey)
        {
            var fileStem = bodyKey + HashTableSuffix;
            hashTable.name = fileStem;
            RemoveExistingSubAssets<BiomeLookupHashTable>(host);
            AssetDatabase.AddObjectToAsset(hashTable, host);
            return hashTable;
        }

        // Gameplay never reads BiomeSurfaceData.color, but PQS logs a one-time error when the LUT
        // reference is null. Ship a 1-entry stub so the field is satisfied and the error stays
        // silent. The single entry uses Color.black, matching the fallback path inside
        // BiomeLookupHashTable.GetBiomeDataAtUV when colorTable is null.
        private static BiomeTextureColorLookupTable WriteStubColorLutAsSubAsset(PQSData host, string bodyKey)
        {
            var fileStem = bodyKey + ColorLutSuffix;
            RemoveExistingSubAssets<BiomeTextureColorLookupTable>(host);
            var lut = ScriptableObject.CreateInstance<BiomeTextureColorLookupTable>();
            lut.name = fileStem;
            lut.BiomeLookupPairs = new List<BiomeLookupEditorPair>
            {
                new() { name = "stub", color = Color.black },
            };
            AssetDatabase.AddObjectToAsset(lut, host);
            return lut;
        }

        private static void RemoveExistingSubAssets<T>(PQSData host) where T : UnityEngine.Object
        {
            var hostPath = AssetDatabase.GetAssetPath(host);
            if (string.IsNullOrEmpty(hostPath)) return;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(hostPath))
            {
                if (o is T t && !AssetDatabase.IsMainAsset(t))
                {
                    AssetDatabase.RemoveObjectFromAsset(t);
                    UnityEngine.Object.DestroyImmediate(t, allowDestroyingAssets: true);
                }
            }
        }
    }
}
