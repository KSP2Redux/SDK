using System.Collections.Generic;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Widgets
{
    /// <summary>
    /// Hardcoded suggestions for <c>MissionContentBranch.ID</c>. The KSP2 runtime auto-
    /// triggers three conventional content-branch IDs at appropriate moments (briefing,
    /// completion, submission). Authors can still use custom IDs but those must be invoked
    /// manually via <c>MissionData.TryActivateContentBranch</c>.
    /// </summary>
    public static class ContentBranchIdCatalog
    {
        private static readonly string[] KnownIds =
        {
            "Brief",
            "Debrief",
            "OnSubmit",
        };

        /// <summary>
        /// Returns the hardcoded list of runtime-recognised content-branch IDs.
        /// </summary>
        /// <returns>The known content-branch IDs.</returns>
        public static IReadOnlyList<string> GetKnownContentBranchIds() => KnownIds;
    }
}
