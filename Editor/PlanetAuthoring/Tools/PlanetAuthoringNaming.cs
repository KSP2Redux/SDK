namespace Ksp2UnityTools.Editor.PlanetAuthoring.Tools
{
    /// <summary>
    /// Centralizes the file name and root GameObject conventions used across the celestial body
    /// authoring tools, so a future rename is a single edit.
    /// </summary>
    public static class PlanetAuthoringNaming
    {
        /// <summary>Prefix every celestial body's prefab and scene file uses.</summary>
        public const string CelestialPrefix = "Celestial.";

        /// <summary>Suffix for the scaled-space prefab file.</summary>
        public const string ScaledPrefabSuffix = ".Scaled.prefab";

        /// <summary>Suffix for the local-space (simulation) prefab file.</summary>
        public const string LocalPrefabSuffix = ".Local.prefab";

        /// <summary>Returns the scaled-space prefab file name for <paramref name="key"/>.</summary>
        public static string ScaledPrefab(string key) => $"{CelestialPrefix}{key}{ScaledPrefabSuffix}";

        /// <summary>Returns the local-space prefab file name for <paramref name="key"/>.</summary>
        public static string LocalPrefab(string key) => $"{CelestialPrefix}{key}{LocalPrefabSuffix}";

        /// <summary>Returns the authoring scene file name for <paramref name="key"/>.</summary>
        public static string Scene(string key) => $"{CelestialPrefix}{key}.unity";

        /// <summary>Returns the scaled-space material asset file name for <paramref name="key"/>.</summary>
        public static string ScaledMaterial(string key) => $"{key}_Scaled.mat";

        /// <summary>Returns the local-space material asset file name for <paramref name="key"/>.</summary>
        public static string LocalMaterial(string key) => $"{key}_Local.mat";

        /// <summary>Returns the PQSData asset file name for <paramref name="key"/>.</summary>
        public static string PqsData(string key) => $"{key}_PQS.asset";

        /// <summary>Returns the PQSDecalData asset file name for <paramref name="key"/>.</summary>
        public static string PqsDecalData(string key) => $"{key}_PQSDecalData.asset";

        /// <summary>Returns the ScienceRegions asset file name for <paramref name="key"/>.</summary>
        public static string ScienceRegions(string key) => $"{key}_ScienceRegions.asset";

        /// <summary>Returns the scaled-space prefab's root GameObject name for <paramref name="key"/>.</summary>
        public static string ScaledGameObject(string key) => $"{CelestialPrefix}{key}.Scaled";
    }
}
