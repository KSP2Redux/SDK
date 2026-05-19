namespace Ksp2UnityTools.Editor.PlanetAuthoring
{
    /// <summary>
    /// Centralizes the file name, GameObject name, and SerializedProperty path conventions
    /// shared across the planet authoring tools.
    /// </summary>
    /// <remarks>
    /// One naming hub for the whole planet-authoring tree: prefab/scene/material file names,
    /// PQS authoring asset names, biome channel ordering, and the SerializedProperty paths
    /// used by the PQS inspector. A future rename lands as a single edit here rather than
    /// scattered string literals.
    /// </remarks>
    public static class PlanetAuthoringNaming
    {
        /// <summary>
        /// Prefix every celestial body's prefab and scene file uses.
        /// </summary>
        public const string CelestialPrefix = "Celestial.";

        /// <summary>
        /// Suffix for the scaled-space prefab file.
        /// </summary>
        public const string ScaledPrefabSuffix = ".Scaled.prefab";

        /// <summary>
        /// Suffix for the local-space (simulation) prefab file.
        /// </summary>
        public const string LocalPrefabSuffix = ".Local.prefab";

        /// <summary>
        /// Returns the scaled-space prefab file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The prefab file name including the celestial prefix and scaled suffix.</returns>
        public static string ScaledPrefab(string key) => $"{CelestialPrefix}{key}{ScaledPrefabSuffix}";

        /// <summary>
        /// Returns the local-space prefab file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The prefab file name including the celestial prefix and local suffix.</returns>
        public static string LocalPrefab(string key) => $"{CelestialPrefix}{key}{LocalPrefabSuffix}";

        /// <summary>
        /// Returns the authoring scene file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The Unity scene file name including the celestial prefix.</returns>
        public static string Scene(string key) => $"{CelestialPrefix}{key}.unity";

        /// <summary>
        /// Returns the scaled-space material asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The material asset file name.</returns>
        public static string ScaledMaterial(string key) => $"{key}_Scaled.mat";

        /// <summary>
        /// Returns the local-space material asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The material asset file name.</returns>
        public static string LocalMaterial(string key) => $"{key}_Local.mat";

        /// <summary>
        /// Returns the PQSData asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The PQSData asset file name.</returns>
        public static string PqsData(string key) => $"{key}_PQS.asset";

        /// <summary>
        /// Returns the PQSDecalData asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The PQSDecalData asset file name.</returns>
        public static string PqsDecalData(string key) => $"{key}_PQSDecalData.asset";

        /// <summary>
        /// Returns the ScienceRegions asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The ScienceRegions asset file name.</returns>
        public static string ScienceRegions(string key) => $"{key}_ScienceRegions.asset";

        /// <summary>
        /// Returns the PostProcessData asset file name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The PostProcessData asset file name.</returns>
        public static string PostProcessData(string key) => $"{key}_PostProcess.asset";

        /// <summary>
        /// Returns the scaled-space prefab's root GameObject name for <paramref name="key" />.
        /// </summary>
        /// <param name="key">The celestial body key.</param>
        /// <returns>The root GameObject name including the celestial prefix and scaled suffix.</returns>
        public static string ScaledGameObject(string key) => $"{CelestialPrefix}{key}.Scaled";

        // ---- Biome / PQS authoring constants (relocated from PqsAuthoringNaming) ----

        /// <summary>
        /// Canonical biome-channel order used across the PQS authoring surface.
        /// </summary>
        /// <remarks>
        /// RGBA-as-biomes is hardcoded in the shader. Renaming or reordering would require a
        /// shader edit.
        /// </remarks>
        public static readonly string[] BiomeChannels = { "R", "G", "B", "A" };

        /// <summary>
        /// Returns the SerializedProperty path for the subzone normal at the given tier and biome index.
        /// </summary>
        /// <param name="tier">Subzone tier (3 or 4).</param>
        /// <param name="biomeIndex">Biome index in the canonical RGBA order (0..3).</param>
        /// <returns>The SerializedProperty path into the subzone normals array.</returns>
        public static string SubzoneNormalPath(int tier, int biomeIndex) =>
            $"subzone{tier}Normals.Array.data[{biomeIndex}]";

        /// <summary>
        /// Returns the SerializedProperty path for the <c>SmallLayerSlot</c> at the given slot index.
        /// </summary>
        /// <param name="slot">Flat slot index in the <c>smallLayerSlots</c> array.</param>
        /// <returns>The SerializedProperty path to the slot element.</returns>
        public static string SmallLayerSlotPath(int slot) => $"smallLayerSlots.Array.data[{slot}]";

        /// <summary>
        /// Returns the SerializedProperty path for the named field on the <c>SmallLayerSlot</c> at the given slot index.
        /// </summary>
        /// <param name="slot">Flat slot index in the <c>smallLayerSlots</c> array.</param>
        /// <param name="field">Name of the field on the <c>SmallLayerSlot</c> element.</param>
        /// <returns>The SerializedProperty path to the named field on the slot element.</returns>
        public static string SmallLayerSlotFieldPath(int slot, string field) => $"smallLayerSlots.Array.data[{slot}].{field}";

        /// <summary>
        /// Maps a (biomeChannel, secondaryChannel) pair to the flat index used by the 16-slot (biome, subzone) and (biome, small-layer) arrays.
        /// </summary>
        /// <remarks>
        /// Each axis is 0..3 (R/G/B/A). Centralizing the multiply removes typo risk at every
        /// call site that addresses these arrays.
        /// </remarks>
        /// <param name="biomeChannel">Primary biome channel index (0..3).</param>
        /// <param name="secondaryChannel">Secondary channel index (0..3) within the biome.</param>
        /// <returns>The flat index into the 16-slot array.</returns>
        public static int CellIndex(int biomeChannel, int secondaryChannel) => biomeChannel * 4 + secondaryChannel;
    }
}
