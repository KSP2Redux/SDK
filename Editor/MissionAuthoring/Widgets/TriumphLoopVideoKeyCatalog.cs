using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Hardcoded suggestions for <c>MissionData.TriumphLoopVideoKey</c>. The runtime
    /// resolves the key to <c>{key}.mp4</c> via the asset system. Stock ships four tier
    /// videos. Add Redux-specific keys here once corresponding videos are added to the
    /// addressables.
    /// </summary>
    public static class TriumphLoopVideoKeyCatalog
    {
        private static readonly string[] KnownKeys =
        {
            "reward_mission_tier1",
            "reward_mission_tier2",
            "reward_mission_tier3",
            "reward_mission_tier4",
        };

        /// <summary>
        /// Returns the hardcoded list of known triumph-loop video keys.
        /// </summary>
        /// <returns>The known video keys.</returns>
        public static IReadOnlyList<string> GetKnownVideoKeys() => KnownKeys;
    }
}
