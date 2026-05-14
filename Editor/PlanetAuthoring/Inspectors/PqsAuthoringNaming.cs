namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Centralized naming for PQS authoring widgets.
    /// </summary>
    /// <remarks>
    /// Holds the canonical biome-channel order and the SerializedProperty paths into the
    /// editor-only <c>PQSDataAuthoring</c> sidecar arrays that the matrix view and per-cell
    /// detail editor both reach into. The paths are top-level on the sidecar SO, not nested
    /// under <c>heightMapInfo</c> like they used to be on PQSData.
    /// </remarks>
    internal static class PqsAuthoringNaming
    {
        // Biome channel order. RGBA-as-biomes is hardcoded in the shader. Renaming or reordering
        // would require a shader edit, so this list is constant across the inspector surface.
        /// <summary>
        /// Canonical biome-channel order used across the PQS authoring inspector surface.
        /// </summary>
        public static readonly string[] BiomeChannels = { "R", "G", "B", "A" };

        /// <summary>
        /// Returns the SerializedProperty path for the subzone normal at the given tier (3 or 4) and biome index.
        /// </summary>
        public static string SubzoneNormalPath(int tier, int biomeIndex) =>
            $"subzone{tier}Normals.Array.data[{biomeIndex}]";

        /// <summary>
        /// Returns the SerializedProperty path for the <c>SmallLayerSlot</c> at the given slot index.
        /// </summary>
        public static string SmallLayerSlotPath(int slot) => $"smallLayerSlots.Array.data[{slot}]";

        /// <summary>
        /// Returns the SerializedProperty path for the named field on the <c>SmallLayerSlot</c> at the given slot index.
        /// </summary>
        public static string SmallLayerSlotFieldPath(int slot, string field) => $"smallLayerSlots.Array.data[{slot}].{field}";

        /// <summary>
        /// Maps a (biomeChannel, secondaryChannel) pair to the flat index used by the 16-slot
        /// (biome, subzone) and (biome, small-layer) arrays.
        /// </summary>
        /// <remarks>
        /// Each axis is 0..3 (R/G/B/A). Centralizing the multiply removes typo risk at every call
        /// site that addresses these arrays.
        /// </remarks>
        public static int CellIndex(int biomeChannel, int secondaryChannel) => biomeChannel * 4 + secondaryChannel;
    }
}
