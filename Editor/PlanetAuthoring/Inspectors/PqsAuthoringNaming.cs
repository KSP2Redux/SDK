namespace Ksp2UnityTools.Editor.PlanetAuthoring.Inspectors
{
    /// <summary>
    /// Centralized naming for PQS authoring widgets.
    /// </summary>
    /// <remarks>
    /// Holds the canonical biome-channel order and the SerializedProperty paths into
    /// <c>PQSData.heightMapInfo.smallXxxTiles</c> arrays that the matrix view and per-cell
    /// detail editor both reach into.
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
        /// Returns the SerializedProperty path for the small albedo tile at the given slot.
        /// </summary>
        /// <param name="slot">The biome slot index.</param>
        /// <returns>The SerializedProperty path into <c>PQSData.heightMapInfo.smallAlbedoTiles</c>.</returns>
        public static string SmallAlbedoTilePath(int slot) => $"heightMapInfo.smallAlbedoTiles.Array.data[{slot}]";

        /// <summary>
        /// Returns the SerializedProperty path for the small normal tile at the given slot.
        /// </summary>
        /// <param name="slot">The biome slot index.</param>
        /// <returns>The SerializedProperty path into <c>PQSData.heightMapInfo.smallNormalTiles</c>.</returns>
        public static string SmallNormalTilePath(int slot) => $"heightMapInfo.smallNormalTiles.Array.data[{slot}]";

        /// <summary>
        /// Returns the SerializedProperty path for the small metal tile at the given slot.
        /// </summary>
        /// <param name="slot">The biome slot index.</param>
        /// <returns>The SerializedProperty path into <c>PQSData.heightMapInfo.smallMetalTiles</c>.</returns>
        public static string SmallMetalTilePath(int slot) => $"heightMapInfo.smallMetalTiles.Array.data[{slot}]";
    }
}
