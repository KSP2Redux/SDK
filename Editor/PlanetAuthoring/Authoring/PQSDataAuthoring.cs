using KSP.Rendering.Planets;
using UnityEditor;
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
        /// Legacy small-biome albedo tile sources, indexed by [biome * 4 + layer]. Superseded by <see cref="smallLayerSlots" /> after migration.
        /// </summary>
        /// <remarks>
        /// Kept on disk as a one-way migration source. Texture2DArrayPacker now reads effective
        /// textures from <see cref="smallLayerSlots" /> instead. New writes should not target this
        /// array.
        /// </remarks>
        public Texture2D[] smallAlbedoTiles = new Texture2D[16];

        /// <summary>
        /// Legacy small-biome normal tile sources, indexed by [biome * 4 + layer]. Superseded by <see cref="smallLayerSlots" /> after migration.
        /// </summary>
        public Texture2D[] smallNormalTiles = new Texture2D[16];

        /// <summary>
        /// Legacy small-biome metal tile sources, indexed by [biome * 4 + layer]. Superseded by <see cref="smallLayerSlots" /> after migration.
        /// </summary>
        public Texture2D[] smallMetalTiles = new Texture2D[16];

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
        /// One-shot migration that copies the three legacy <c>smallXTiles</c> texture arrays into <see cref="smallLayerSlots" /> as per-cell texture overrides.
        /// </summary>
        /// <remarks>
        /// Idempotent: no-op once any slot already references an SO or carries an override texture.
        /// Each cell that had a legacy texture becomes a slot with <c>Material = null</c> and the
        /// three texture-override flags forced on, so the body keeps rendering with its current
        /// textures until an artist swaps in an SO. Marks the authoring sidecar dirty when at least
        /// one slot is updated so the migrated state persists on next save.
        /// </remarks>
        /// <param name="authoring">The sidecar to migrate in place.</param>
        public static void MigrateLegacyTilesToSlots(PQSDataAuthoring authoring)
        {
            if (authoring == null) return;
            if (authoring.smallLayerSlots == null || authoring.smallLayerSlots.Length != 16)
                authoring.smallLayerSlots = new SmallLayerSlot[16];

            bool changed = false;
            for (int i = 0; i < 16; i++)
            {
                if (authoring.smallLayerSlots[i] == null)
                {
                    authoring.smallLayerSlots[i] = new SmallLayerSlot();
                    changed = true;
                }

                var slot = authoring.smallLayerSlots[i];

                // Skip cells that already carry slot state (SO reference or any override texture set).
                if (slot.Material != null) continue;
                if (slot.OverrideAlbedoTexture || slot.OverrideNormalTexture || slot.OverrideMetallicTexture) continue;

                var legacyAlbedo = (authoring.smallAlbedoTiles != null && i < authoring.smallAlbedoTiles.Length) ? authoring.smallAlbedoTiles[i] : null;
                var legacyNormal = (authoring.smallNormalTiles != null && i < authoring.smallNormalTiles.Length) ? authoring.smallNormalTiles[i] : null;
                var legacyMetal = (authoring.smallMetalTiles != null && i < authoring.smallMetalTiles.Length) ? authoring.smallMetalTiles[i] : null;

                if (legacyAlbedo == null && legacyNormal == null && legacyMetal == null) continue;

                if (legacyAlbedo != null)
                {
                    slot.OverrideAlbedoTexture = true;
                    slot.AlbedoTexture = legacyAlbedo;
                }
                if (legacyNormal != null)
                {
                    slot.OverrideNormalTexture = true;
                    slot.NormalTexture = legacyNormal;
                }
                if (legacyMetal != null)
                {
                    slot.OverrideMetallicTexture = true;
                    slot.MetallicTexture = legacyMetal;
                }
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(authoring);
        }
    }
}
