using System.Collections.Generic;
using System.IO;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.API;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Biomes
{
    /// <summary>
    /// Bakes a <see cref="BiomeLookupHashTable" /> (and a stub <see cref="BiomeTextureColorLookupTable" />) from <c>_BiomeMaskTex</c> plus the per-channel <see cref="PQSData.KSP2BiomeType" /> mappings stored on <see cref="PQSDataAuthoring" />.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy color-keyed BiomeLookupUtility flow: artists already authored the
    /// shader's R/G/B/A biome mask, so the lookup table derives from that instead of a separate
    /// color-coded image. Gameplay only reads <c>BiomeSurfaceData.type</c>, never the color, so
    /// every packed chunk uses colorIndex 0. The stub color LUT exists only to silence the
    /// one-time "missing BiomeColorLookupTable" error logged by <see cref="PQS" />.
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
        /// On success, writes the hash table asset, the stub color LUT, registers both as
        /// addressables, and auto-wires the result onto PQSData.
        /// </remarks>
        /// <param name="data">The PQSData asset whose biome lookup is baked.</param>
        /// <returns>A <see cref="BakeResult" /> describing the outcome.</returns>
        public static BakeResult Bake(PQSData data)
        {
            if (data == null) return Fail("PQSData is null.");

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

            var sidecar = PlanetAuthoringRegistry.Instance.GetOrCreatePQSData(data);
            if (sidecar == null) return Fail("Could not resolve the PQSDataAuthoring sidecar for this PQSData.");

            var mappedChannels = 0;
            for (var i = 0; i < 4; i++)
            {
                if (sidecar.biomeChannelMapping[i] != PQSData.KSP2BiomeType.NONE)
                {
                    mappedChannels++;
                }
            }
            if (mappedChannels == 0) return Fail("No biome channels are mapped to a KSP2BiomeType. Bake would emit an empty hash table.");

            var bodyKey = ResolveBodyKey(data);
            var folder = ResolveFolder(data);

            var hashTable = BuildHashTable(biomeMask, subzones ? subzoneMask : null, sidecar, subzones);

            var pqsDataPath = AssetDatabase.GetAssetPath(data);
            var savedHashTable = WriteHashTableAsSubAsset(hashTable, data, folder, bodyKey);
            var savedColorLut = WriteStubColorLutAsSubAsset(data, folder, bodyKey);

            data.PlanetBiomeHashTable = savedHashTable;
            if (data.BiomeColorLookupTable == null)
            {
                data.BiomeColorLookupTable = savedColorLut;
            }
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

        private static string ResolveFolder(PQSData data)
        {
            var path = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(path)) return "Assets";
            return Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
        }

        private static BiomeLookupHashTable BuildHashTable(Texture2D biomeMask, Texture2D subzoneMask, PQSDataAuthoring sidecar, bool subzones)
        {
            var hashTable = ScriptableObject.CreateInstance<BiomeLookupHashTable>();
            var total = HashTableDimension * HashTableDimension;

            // Scratch buffers reused across cells. The 16x16 type grid stays alive for the whole
            // bake. The chunk-pack buffer collects packed ints per cell and gets copied (sized to
            // fit) into a fresh per-cell List<int> at the end. Avoids ~5 List-growth reallocations
            // per cell (the List doubles its capacity per resize: 4, 8, 16, 32, ...).
            var subCellTypes = new PQSData.KSP2BiomeType[CellDimension, CellDimension];
            var packBuffer = new List<int>(CellDimension * CellDimension);

            for (var cellIndex = 0; cellIndex < total; cellIndex++)
            {
                var cellX = cellIndex % HashTableDimension;
                var cellY = cellIndex / HashTableDimension;
                FillSubCellGrid(subCellTypes, biomeMask, subzoneMask, sidecar, subzones, cellX, cellY);
                packBuffer.Clear();
                PartitionAndPackInto(subCellTypes, packBuffer);
                hashTable.Cells[cellIndex] = new BiomeLookupHashCell
                {
                    BiomeChunks = new List<int>(packBuffer.Count) { Capacity = packBuffer.Count },
                };
                hashTable.Cells[cellIndex].BiomeChunks.AddRange(packBuffer);
            }
            return hashTable;
        }

        private static void FillSubCellGrid(
            PQSData.KSP2BiomeType[,] dst,
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
                    var subzoneType = sidecar.biomeSubzoneMapping[PqsAuthoringNaming.CellIndex(biome, subzone)];
                    if (subzoneType != PQSData.KSP2BiomeType.NONE)
                    {
                        type = subzoneType;
                    }
                }
                dst[sy, sx] = type;
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
        private static void PartitionAndPackInto(PQSData.KSP2BiomeType[,] grid, List<int> packed)
        {
            var claimed = new bool[CellDimension, CellDimension];
            for (var y = 0; y < CellDimension; y++)
            for (var x = 0; x < CellDimension; x++)
            {
                if (claimed[y, x]) continue;
                var type = grid[y, x];
                var maxX = x;
                while (maxX + 1 < CellDimension && !claimed[y, maxX + 1] && grid[y, maxX + 1] == type) maxX++;
                var maxY = y;
                while (maxY + 1 < CellDimension && RowMatches(grid, claimed, type, x, maxX, maxY + 1)) maxY++;
                for (var yy = y; yy <= maxY; yy++)
                {
                    for (var xx = x; xx <= maxX; xx++)
                    {
                        claimed[yy, xx] = true;
                    }
                }
                packed.Add(BiomeLookupHashTable.PackBiomeChunkData(x, maxX, y, maxY, (int)type, 0));
            }
        }

        private static bool RowMatches(PQSData.KSP2BiomeType[,] grid, bool[,] claimed, PQSData.KSP2BiomeType type, int x0, int x1, int y)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (claimed[y, x] || grid[y, x] != type) return false;
            }
            return true;
        }

        // Stored as a sub-asset of PQSData so the bundle includes it automatically through PQSData's
        // PlanetBiomeHashTable field. Nothing in the runtime loads it by addressables label or key,
        // so it doesn't need a standalone GUID. Older bakes wrote a separate file; migration deletes
        // that file and its addressables entry on the next bake.
        private static BiomeLookupHashTable WriteHashTableAsSubAsset(BiomeLookupHashTable hashTable, PQSData host, string folder, string bodyKey)
        {
            var fileStem = bodyKey + HashTableSuffix;
            MigrateLegacyFile($"{folder}/{fileStem}.asset");
            hashTable.name = fileStem;
            RemoveExistingSubAssets<BiomeLookupHashTable>(host);
            AssetDatabase.AddObjectToAsset(hashTable, host);
            return hashTable;
        }

        // Gameplay never reads BiomeSurfaceData.color, but PQS logs a one-time error when the LUT
        // reference is null. Ship a 1-entry stub so the field is satisfied and the error stays
        // silent. The single entry uses Color.black, matching the fallback path inside
        // BiomeLookupHashTable.GetBiomeDataAtUV when colorTable is null.
        private static BiomeTextureColorLookupTable WriteStubColorLutAsSubAsset(PQSData host, string folder, string bodyKey)
        {
            var fileStem = bodyKey + ColorLutSuffix;
            MigrateLegacyFile($"{folder}/{fileStem}.asset");
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

        private static void MigrateLegacyFile(string legacyPath)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(legacyPath) == null) return;
            Ksp2UnityTools.Editor.API.AddressablesTools.RemoveAddressable(legacyPath);
            AssetDatabase.DeleteAsset(legacyPath);
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
