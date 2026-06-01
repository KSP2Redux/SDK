using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation
{
    /// <summary>
    /// Process-wide cache for the last <see cref="ValidatorCost.Expensive" /> run per mission.
    /// </summary>
    /// <remarks>
    /// Mirror of <c>PartValidationExpensiveCache</c>. The header chip and the report window both
    /// read from here, so a single "Run Full" press in the report window lights up every surface.
    /// Keyed by the <see cref="Mission" /> reference. Domain reload destroys both the cache and the
    /// references it holds, so per-reload invalidation is automatic.
    /// </remarks>
    public static class MissionValidationExpensiveCache
    {
        private static readonly Dictionary<Mission, IReadOnlyList<ValidationIssue>> _cache = new();

        /// <summary>
        /// Returns the cached expensive results for <paramref name="mission" />, or empty when none have been run.
        /// </summary>
        /// <param name="mission">The mission whose cache entry to retrieve. Null returns empty.</param>
        /// <returns>The cached issues, or an empty list when no expensive run has completed.</returns>
        public static IReadOnlyList<ValidationIssue> Get(Mission mission)
        {
            if (mission == null) return Array.Empty<ValidationIssue>();
            return _cache.TryGetValue(mission, out IReadOnlyList<ValidationIssue> cached)
                ? cached
                : Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Returns true when an expensive run has completed for <paramref name="mission" />, regardless of whether the run produced any issues.
        /// </summary>
        /// <param name="mission">The mission to check. Null returns false.</param>
        /// <returns>True if an expensive run has been recorded for the mission, false otherwise.</returns>
        public static bool HasRunFor(Mission mission)
        {
            if (mission == null) return false;
            return _cache.ContainsKey(mission);
        }

        /// <summary>
        /// Stores <paramref name="issues" /> as the most recent expensive-run result for <paramref name="mission" />.
        /// </summary>
        /// <param name="mission">The mission to associate the issues with. Null is a no-op.</param>
        /// <param name="issues">The expensive-run issues to cache.</param>
        public static void Set(Mission mission, IReadOnlyList<ValidationIssue> issues)
        {
            if (mission == null) return;
            _cache[mission] = issues ?? Array.Empty<ValidationIssue>();
            Changed?.Invoke(mission);
        }

        /// <summary>
        /// Drops every cached entry.
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
            Changed?.Invoke(null);
        }

        /// <summary>
        /// Fires when an entry is set or the cache is cleared.
        /// </summary>
        /// <remarks>
        /// Argument is the mission whose entry changed. A null argument signals a full-cache
        /// invalidation, so subscribers should re-render unconditionally rather than skip on
        /// mismatch.
        /// </remarks>
        public static event Action<Mission> Changed;
    }
}
