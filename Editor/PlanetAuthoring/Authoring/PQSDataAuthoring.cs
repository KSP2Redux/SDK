using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Authoring
{
    /// <summary>
    /// Editor-only sidecar for a <see cref="PQSData" /> asset, holding the per-(biome, layer) small-tile sources and per-biome subzone-tier normal sources that the surface inspector edits and the texture packer reads.
    /// </summary>
    /// <remarks>
    /// Lives as a standalone <c>.asset</c> in the owning body's <c>Data/</c> folder, resolved by
    /// <see cref="AuthoringSidecars" />. Source textures live here so the runtime PQSData asset
    /// doesn't pull them into the addressables bundle (only the packed Texture2DArray subassets do).
    /// </remarks>
    public class PQSDataAuthoring : ScriptableObject
    {
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
        /// Per-(biome, layer) small-layer slots holding the optional <see cref="SmallLayerMaterial" /> reference plus per-field override toggles and values, indexed by [biome * 4 + layer].
        /// </summary>
        /// <remarks>
        /// Each slot's effective textures and numeric values are pushed into the surface material
        /// by <c>SmallLayerMaterialCompiler.Compile</c>. Per-body parameters (height/slope windows,
        /// gradience weights, master weight, distance resample tier, subzone overrides) are not
        /// stored here - they live on the body's surface Material as before.
        /// </remarks>
        public SmallLayerSlot[] smallLayerSlots = new SmallLayerSlot[16];

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

        /// <summary>
        /// Stamped by Texture2DArrayPacker.RepackSmallTiles after a successful pack, read by the bake-drift validator.
        /// </summary>
        public string LastSmallTilesPackFingerprint = string.Empty;

        /// <summary>
        /// Stamped by <see cref="Tools.BodySurfaceBakerOperation" /> after a successful bake, read by the surface-bake-drift validator.
        /// </summary>
        /// <remarks>
        /// Covers the inputs that feed the gradience bake and per-biome normal bake (per-biome raw
        /// heightmaps + their height scales + body radius). A mismatch means the artist edited an
        /// input since the last bake, so the gradience/normal outputs sampled by the surface
        /// shader are stale.
        /// </remarks>
        public string LastSurfaceBakeFingerprint = string.Empty;
    }
}
