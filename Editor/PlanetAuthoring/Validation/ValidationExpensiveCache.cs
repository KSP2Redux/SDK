using System;
using System.Collections.Generic;
using KSP;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Process-wide cache for the last <see cref="ValidatorCost.Expensive" /> run per body.
    /// </summary>
    /// <remarks>
    /// Read by the inline severity chip on <see cref="Inspectors.CelestialBodyEditor" /> and by
    /// <see cref="Windows.ValidationReportWindow" /> so a single expensive run is visible in both. Static lifetime
    /// is tied to the editor assembly and resets on domain reload.
    /// </remarks>
    public static class ValidationExpensiveCache
    {
        private static readonly Dictionary<EntityId, IReadOnlyList<ValidationIssue>> _cache = new();

        /// <summary>
        /// Returns the cached expensive results for <paramref name="body" />, or empty when none have been run.
        /// </summary>
        /// <param name="body">The body whose cache entry to retrieve. Null returns empty.</param>
        public static IReadOnlyList<ValidationIssue> Get(CoreCelestialBodyData body)
        {
            if (body == null)
                return Array.Empty<ValidationIssue>();
            return _cache.TryGetValue(body.GetEntityId(), out IReadOnlyList<ValidationIssue> cached)
                ? cached
                : Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Returns true when an expensive run has completed for <paramref name="body" />, regardless of whether
        /// the run produced any issues.
        /// </summary>
        /// <param name="body">The body to check. Null returns false.</param>
        public static bool HasRunFor(CoreCelestialBodyData body)
        {
            if (body == null) return false;
            return _cache.ContainsKey(body.GetEntityId());
        }

        /// <summary>
        /// Stores <paramref name="issues" /> as the most recent expensive-run result for <paramref name="body" />.
        /// </summary>
        /// <param name="body">The body to associate the issues with. Null is a no-op.</param>
        /// <param name="issues">The expensive-run issues to cache.</param>
        public static void Set(CoreCelestialBodyData body, IReadOnlyList<ValidationIssue> issues)
        {
            if (body == null) return;
            _cache[body.GetEntityId()] = issues ?? Array.Empty<ValidationIssue>();
            Changed?.Invoke(body);
        }

        /// <summary>
        /// Drops every cached entry. Used in tests and when validators are added or modified.
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
        /// Argument is the body whose entry changed. A null argument signals a full-cache invalidation
        /// (every body's cached results just disappeared) so subscribers should re-render unconditionally
        /// rather than skip on body-mismatch.
        /// </remarks>
        public static event Action<CoreCelestialBodyData> Changed;
    }
}
