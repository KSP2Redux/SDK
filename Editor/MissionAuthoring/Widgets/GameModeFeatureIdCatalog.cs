using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Hardcoded suggestions for <c>MissionData.GameModeFeatureId</c>. Game-mode features
    /// are defined in stock GameMode assets and currently surface only one mission-relevant
    /// id. Add new ids here as additional game modes light up.
    /// </summary>
    public static class GameModeFeatureIdCatalog
    {
        private static readonly string[] KnownIds =
        {
            "ExplorationMissions",
        };

        /// <summary>
        /// Returns the hardcoded list of known game-mode feature IDs.
        /// </summary>
        /// <returns>The known feature IDs.</returns>
        public static IReadOnlyList<string> GetKnownFeatureIds() => KnownIds;
    }
}
