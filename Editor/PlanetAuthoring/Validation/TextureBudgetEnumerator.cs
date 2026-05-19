using System.Collections.Generic;
using KSP;
using KSP.Rendering;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Walks every texture reachable from a body's data model and reports per-texture VRAM size.
    /// </summary>
    /// <remarks>
    /// Sizes come from <see cref="Profiler.GetRuntimeMemorySizeLong" /> which handles compressed formats and
    /// Texture2DArray slice counts. Textures are deduped by instance ID so the same Texture2DArray referenced
    /// from both the surface material and a buffer binding is counted once. The enumerator surfaces a per-category
    /// breakdown and a top-N list so the artist sees what is heavy. Streaming-mip-map state is reported but not
    /// adjusted away from the upper-bound number.
    /// </remarks>
    public sealed class TextureBudgetEnumerator
    {
        /// <summary>One enumerated texture and the category it was first found under.</summary>
        public readonly struct Entry
        {
            /// <summary>Source texture asset.</summary>
            public Texture Tex { get; }
            /// <summary>Category label (heightmap stack, surface material, etc.).</summary>
            public string Category { get; }
            /// <summary>Runtime VRAM bytes reported by the profiler.</summary>
            public long Bytes { get; }
            /// <summary>Optional context (matrix slot, atlas slice, etc.) shown alongside the entry.</summary>
            public string Detail { get; }

            internal Entry(Texture tex, string category, long bytes, string detail)
            {
                Tex = tex;
                Category = category;
                Bytes = bytes;
                Detail = detail;
            }
        }

        /// <summary>Per-texture entries deduped by instance ID.</summary>
        public List<Entry> Entries { get; } = new();
        /// <summary>Aggregate VRAM in bytes across every entry.</summary>
        public long TotalBytes { get; private set; }
        /// <summary>Byte total per category, summed across the entries.</summary>
        public Dictionary<string, long> BytesByCategory { get; } = new();
        /// <summary>Texture count per category.</summary>
        public Dictionary<string, int> CountByCategory { get; } = new();
        /// <summary>Number of entries with <c>Texture2D.streamingMipmaps</c> enabled.</summary>
        public int StreamingMipsCount { get; private set; }

        private readonly HashSet<int> _seen = new();

        /// <summary>
        /// Builds a fresh enumeration for <paramref name="body" />.
        /// </summary>
        /// <param name="body">The body to enumerate. Null returns an empty result.</param>
        public static TextureBudgetEnumerator Compute(CoreCelestialBodyData body)
        {
            var calc = new TextureBudgetEnumerator();
            if (body?.Core?.data == null) return calc;
            BodyClassFlags cls = BodyClassClassifier.Classify(body);

            if ((cls & BodyClassFlags.SolidSurface) != 0)
            {
                PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
                if (pqs?.data != null)
                {
                    calc.AddHeightmapStack(pqs.data.heightMapInfo);
                    // Redirect packed Texture2DArray subassets to their authored source textures BEFORE
                    // walking the surface material, so the array is marked seen and the source slots
                    // get the bytes attributed to them.
                    calc.AddPackedArraySources(pqs.data);
                    calc.AddMaterialTextures(pqs.data.materialSettings?.surfaceMaterial, "Surface material");
                    calc.AddMaterialTextures(pqs.data.materialSettings?.scaledSpaceMaterial, "Scaled-space material");
                    calc.AddDecals(body);
                }
            }

            if ((cls & (BodyClassFlags.Star | BodyClassFlags.GasGiant)) != 0)
            {
                GameObject scaled = AddressableKeyLookup.GetPrefab(body.Core.data.assetKeyScaled);
                calc.AddPrefabTextures(scaled, "Scaled-space prefab");
            }

            if (body.Core.data.hasAtmosphere)
                calc.AddAtmosphere(body);

            return calc;
        }

        private void AddHeightmapStack(PQSData.HeightMapInfo info)
        {
            if (info == null) return;
            const string Cat = "PQS heightmaps";
            Add(info.globalHeightMap, Cat);
            Add(info.mask, Cat);
            Add(info.subZoneMask, Cat);
            AddRegion(info.largeR, Cat);
            AddRegion(info.largeG, Cat);
            AddRegion(info.largeB, Cat);
            AddRegion(info.largeA, Cat);
            AddRegion(info.mediumR, Cat);
            AddRegion(info.mediumG, Cat);
            AddRegion(info.mediumB, Cat);
            AddRegion(info.mediumA, Cat);
            AddRegion(info.subzone3R, Cat);
            AddRegion(info.subzone3G, Cat);
            AddRegion(info.subzone3B, Cat);
            AddRegion(info.subzone3A, Cat);
            AddRegion(info.subzone4R, Cat);
            AddRegion(info.subzone4G, Cat);
            AddRegion(info.subzone4B, Cat);
            AddRegion(info.subzone4A, Cat);
        }

        private void AddRegion(PQSData.HeightRegion region, string category)
        {
            if (region == null) return;
            Add(region.heightMap, category);
        }

        private void AddMaterialTextures(Material mat, string category)
        {
            if (mat == null || mat.shader == null) return;
            int count = ShaderUtil.GetPropertyCount(mat.shader);
            for (int i = 0; i < count; i++)
            {
                if (ShaderUtil.GetPropertyType(mat.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                    continue;
                string name = ShaderUtil.GetPropertyName(mat.shader, i);
                Texture tex = mat.GetTexture(name);
                Add(tex, category);
            }
        }

        private void AddPackedArraySources(PQSData pqsData)
        {
            if (pqsData == null) return;
            PQSDataAuthoring authoring = AuthoringSidecars.GetOrCreate(pqsData);
            if (authoring == null) return;

            // Each named array is a Texture2DArray subasset of the PQSData. Find it once, charge each slice's
            // share of the array's bytes against the authored source texture, then mark the array as seen so
            // the surface-material walk skips it. Each row carries the authored matrix slot as Detail so the
            // artist sees which (biome, layer) cell to fix even when the source texture is generically named.
            AddSmallTileSources(pqsData, Texture2DArrayPacker.SmallAlbedoArrayName, "Small biome tiles (albedo)", CollectAlbedoSources(authoring));
            AddSmallTileSources(pqsData, Texture2DArrayPacker.SmallNormalArrayName, "Small biome tiles (normal)", CollectNormalSources(authoring));
            AddSmallTileSources(pqsData, Texture2DArrayPacker.SmallMetalArrayName, "Small biome tiles (metal)", CollectMetalSources(authoring));
            AddSubzoneNormalSources(pqsData, Texture2DArrayPacker.SubzoneNormalsArrayName, "Subzone normals", CollectSubzoneNormalSources(authoring));
        }

        private void AddSmallTileSources(PQSData pqsData, string arrayName, string category, Texture2D[] sources)
        {
            long perSlice = MarkArrayAndComputePerSliceCost(pqsData, arrayName);
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                Texture2D src = sources[i];
                if (src == null) continue;
                if (!_seen.Add(src.GetInstanceID())) continue;
                int biome = i / 4;
                int layer = i % 4 + 1;
                string detail = $"Biome {PlanetAuthoringNaming.BiomeChannels[biome]} Layer {layer}";
                AddEntry(src, category, perSlice, detail);
            }
        }

        private void AddSubzoneNormalSources(PQSData pqsData, string arrayName, string category, Texture2D[] sources)
        {
            long perSlice = MarkArrayAndComputePerSliceCost(pqsData, arrayName);
            if (sources == null) return;
            for (int i = 0; i < sources.Length; i++)
            {
                Texture2D src = sources[i];
                if (src == null) continue;
                if (!_seen.Add(src.GetInstanceID())) continue;
                int tier = i < 4 ? 3 : 4;
                int biome = i % 4;
                string detail = $"Subzone {tier} Biome {PlanetAuthoringNaming.BiomeChannels[biome]}";
                AddEntry(src, category, perSlice, detail);
            }
        }

        private long MarkArrayAndComputePerSliceCost(PQSData pqsData, string arrayName)
        {
            Texture2DArray array = FindArraySubasset(pqsData, arrayName);
            if (array == null) return 0;
            // Mark the array as already accounted for so the surface-material walk doesn't add it back.
            _seen.Add(array.GetInstanceID());
            int sliceCount = array.depth;
            long arrayBytes = Profiler.GetRuntimeMemorySizeLong(array);
            return sliceCount > 0 ? arrayBytes / sliceCount : 0;
        }

        private static Texture2DArray FindArraySubasset(PQSData pqsData, string name)
        {
            string path = AssetDatabase.GetAssetPath(pqsData);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Texture2DArray arr && arr.name == name)
                    return arr;
            }
            return null;
        }

        private static Texture2D[] CollectAlbedoSources(PQSDataAuthoring a)
        {
            var arr = new Texture2D[16];
            for (int i = 0; i < 16; i++)
                arr[i] = (a.smallLayerSlots != null && i < a.smallLayerSlots.Length) ? a.smallLayerSlots[i]?.EffectiveAlbedoTexture : null;
            return arr;
        }

        private static Texture2D[] CollectNormalSources(PQSDataAuthoring a)
        {
            var arr = new Texture2D[16];
            for (int i = 0; i < 16; i++)
                arr[i] = (a.smallLayerSlots != null && i < a.smallLayerSlots.Length) ? a.smallLayerSlots[i]?.EffectiveNormalTexture : null;
            return arr;
        }

        private static Texture2D[] CollectMetalSources(PQSDataAuthoring a)
        {
            var arr = new Texture2D[16];
            for (int i = 0; i < 16; i++)
                arr[i] = (a.smallLayerSlots != null && i < a.smallLayerSlots.Length) ? a.smallLayerSlots[i]?.EffectiveMetallicTexture : null;
            return arr;
        }

        private static Texture2D[] CollectSubzoneNormalSources(PQSDataAuthoring a)
        {
            var arr = new Texture2D[8];
            for (int i = 0; i < 4; i++)
                arr[i] = (a.subzone3Normals != null && i < a.subzone3Normals.Length) ? a.subzone3Normals[i] : null;
            for (int i = 0; i < 4; i++)
                arr[4 + i] = (a.subzone4Normals != null && i < a.subzone4Normals.Length) ? a.subzone4Normals[i] : null;
            return arr;
        }

        private void AddDecals(CoreCelestialBodyData body)
        {
            const string Cat = "Decal data";
            foreach (PQSDecalController controller in body.GetComponentsInChildren<PQSDecalController>(includeInactive: true))
            {
                if (controller?.PqsDecalData == null) continue;
                var data = controller.PqsDecalData;
                Add(data.DiffuseTextureArray, Cat);
                Add(data.NormalTextureArray, Cat);
                Add(data.AlphaMaskTextureArray, Cat);
                Add(data.PeakTextureArray, Cat);
                Add(data.SlopeTextureArray, Cat);
            }
        }

        private void AddAtmosphere(CoreCelestialBodyData body)
        {
            const string Cat = "Atmosphere LUTs";
            foreach (AtmosphereDataModelComponent comp in body.GetComponentsInChildren<AtmosphereDataModelComponent>(includeInactive: true))
            {
                if (comp == null || string.IsNullOrEmpty(comp.AtmosphereModelKey)) continue;
                string path = ResolveAddressablePath(comp.AtmosphereModelKey);
                if (string.IsNullOrEmpty(path)) continue;
                AtmosphereModel model = AssetDatabase.LoadAssetAtPath<AtmosphereModel>(path);
                if (model == null) continue;
                Add(model.TransmittanceTexture, Cat);
                Add(model.IrradianceTexture, Cat);
                Add(model.ScatteringTexture, Cat);
            }
        }

        private void AddPrefabTextures(GameObject prefab, string category)
        {
            if (prefab == null) return;
            foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (Material m in mats)
                    AddMaterialTextures(m, category);
            }
        }

        private void Add(Texture tex, string category, string detail = null)
        {
            if (tex == null) return;
            // Skip runtime-allocated render targets - they're not authored content and the artist can't
            // act on them from the breakdown.
            if (tex is RenderTexture) return;
            int id = tex.GetInstanceID();
            if (!_seen.Add(id)) return;
            long bytes = Profiler.GetRuntimeMemorySizeLong(tex);
            AddEntry(tex, category, bytes, detail);
        }

        private void AddEntry(Texture tex, string category, long bytes, string detail)
        {
            Entries.Add(new Entry(tex, category, bytes, detail));
            TotalBytes += bytes;
            if (BytesByCategory.TryGetValue(category, out long cur))
                BytesByCategory[category] = cur + bytes;
            else
                BytesByCategory[category] = bytes;
            CountByCategory[category] = CountByCategory.TryGetValue(category, out int n) ? n + 1 : 1;
            if (tex is Texture2D t2d && t2d.streamingMipmaps)
                StreamingMipsCount++;
        }

        /// <summary>
        /// Returns the texture's name, or <c>(unnamed)</c> when blank.
        /// </summary>
        /// <param name="tex">Texture to describe.</param>
        public static string TextureName(Texture tex)
        {
            if (tex == null) return "(null)";
            return string.IsNullOrEmpty(tex.name) ? "(unnamed)" : tex.name;
        }

        /// <summary>
        /// Returns the dimensions and format of <paramref name="tex" /> without the name.
        /// </summary>
        /// <param name="tex">Texture to describe.</param>
        public static string TextureDimensions(Texture tex)
        {
            if (tex == null) return string.Empty;
            switch (tex)
            {
                case Texture2DArray arr:
                    return $"{arr.width}x{arr.height} x{arr.depth} {arr.format}";
                case Texture3D t3d:
                    return $"{t3d.width}x{t3d.height}x{t3d.depth} {t3d.format}";
                case Texture2D t2d:
                    return $"{t2d.width}x{t2d.height} {t2d.format}";
                default:
                    return $"{tex.width}x{tex.height}";
            }
        }

        /// <summary>
        /// Formats a byte count as a short human-readable size (B, KB, MB, GB).
        /// </summary>
        /// <param name="bytes">Byte count.</param>
        public static string FormatSize(long bytes) => FormatBytes(bytes);

        private static string ResolveAddressablePath(string key)
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null) continue;
                    if (entry.address != key) continue;
                    return AssetDatabase.GUIDToAssetPath(entry.guid);
                }
            }
            return null;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("0.#") + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("0.#") + " MB";
            double gb = mb / 1024.0;
            return gb.ToString("0.##") + " GB";
        }
    }
}
