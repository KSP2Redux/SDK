using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <see cref="KSP.Rendering.Planets.PQSData" /> asset, holding the
    /// per-(biome, layer) small-tile sources and per-biome subzone-tier normal sources that the
    /// surface inspector edits and the texture packer reads.
    /// </summary>
    /// <remarks>
    /// Stored as a sub-asset of <see cref="PlanetAuthoringRegistry" />, keyed by the PQSData
    /// asset's AssetDatabase GUID. Source textures live here so the runtime PQSData asset doesn't
    /// pull them into the addressables bundle (only the packed Texture2DArray subassets do).
    /// </remarks>
    public class PQSDataAuthoring : ScriptableObject
    {
        /// <summary>
        /// AssetDatabase GUID of the owning <see cref="PQSData" /> asset, used as the sidecar key.
        /// </summary>
        public string PQSDataGuid;

        /// <summary>
        /// Per-biome (R, G, B, A) source normal textures for subzone tier 3.
        /// </summary>
        /// <remarks>
        /// Packed into the surface material's _SubZonesNormalTextureArray subasset by Texture2DArrayPacker.
        /// </remarks>
        public Texture2D[] subzone3Normals = new Texture2D[4];

        /// <summary>
        /// Per-biome (R, G, B, A) source normal textures for subzone tier 4.
        /// </summary>
        /// <remarks>
        /// Packed into the surface material's _SubZonesNormalTextureArray subasset by Texture2DArrayPacker.
        /// </remarks>
        public Texture2D[] subzone4Normals = new Texture2D[4];

        /// <summary>
        /// Small-biome albedo tile sources, indexed by [biome * 4 + layer] where biome is 0..3 (R/G/B/A) and layer is 0..3.
        /// </summary>
        /// <remarks>
        /// Packed into the _SmallAlbedoArray subasset by Texture2DArrayPacker.
        /// </remarks>
        public Texture2D[] smallAlbedoTiles = new Texture2D[16];

        /// <summary>
        /// Small-biome normal tile sources, indexed by [biome * 4 + layer] where biome is 0..3 (R/G/B/A) and layer is 0..3.
        /// </summary>
        /// <remarks>
        /// Packed into the _SmallNormalArray subasset by Texture2DArrayPacker.
        /// </remarks>
        public Texture2D[] smallNormalTiles = new Texture2D[16];

        /// <summary>
        /// Small-biome metal tile sources, indexed by [biome * 4 + layer] where biome is 0..3 (R/G/B/A) and layer is 0..3.
        /// </summary>
        /// <remarks>
        /// Packed into the _SmallMetalArray subasset by Texture2DArrayPacker.
        /// </remarks>
        public Texture2D[] smallMetalTiles = new Texture2D[16];

        /// <summary>
        /// Per-channel KSP2BiomeType assignments consumed by the biome lookup baker.
        /// </summary>
        /// <remarks>
        /// The baker walks _BiomeMaskTex, picks the dominant R/G/B/A channel per cell, and looks up the assigned KSP2BiomeType here. With subzones disabled only this mapping is read.
        /// </remarks>
        public PQSData.KSP2BiomeType[] biomeChannelMapping = new PQSData.KSP2BiomeType[4];

        /// <summary>
        /// Per-(biome, subzone) KSP2BiomeType overrides applied when SUB_ZONES_ENABLED is on, indexed by [biomeChannel * 4 + subzoneChannel].
        /// </summary>
        /// <remarks>
        /// NONE means "inherit the row's <see cref="biomeChannelMapping" /> entry" so artists only fill the cells where a subzone genuinely changes the type.
        /// </remarks>
        public PQSData.KSP2BiomeType[] biomeSubzoneMapping = new PQSData.KSP2BiomeType[16];
    }
}
