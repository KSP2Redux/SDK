using System;
using System.Collections.Generic;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation
{
    /// <summary>
    /// Process-wide cache for the last <see cref="ValidatorCost.Expensive" /> run per part.
    /// </summary>
    /// <remarks>
    /// Mirror of the planet-side ValidationExpensiveCache. The inspector chip and the report
    /// window both read from here so a single "Run full validation" press lights up every surface.
    /// Keyed by the <see cref="CorePartData" /> reference: domain reload destroys both the cache
    /// and the references it holds, so per-reload invalidation is automatic.
    /// </remarks>
    public static class PartValidationExpensiveCache
    {
        private static readonly Dictionary<CorePartData, IReadOnlyList<ValidationIssue>> _cache = new();

        /// <summary>
        /// Returns the cached expensive results for <paramref name="part" />, or empty when none have been run.
        /// </summary>
        /// <param name="part">The part whose cache entry to retrieve. Null returns empty.</param>
        public static IReadOnlyList<ValidationIssue> Get(CorePartData part)
        {
            if (part == null)
            {
                return Array.Empty<ValidationIssue>();
            }
            return _cache.TryGetValue(part, out IReadOnlyList<ValidationIssue> cached)
                ? cached
                : Array.Empty<ValidationIssue>();
        }

        /// <summary>
        /// Returns true when an expensive run has completed for <paramref name="part" />, regardless of whether
        /// the run produced any issues.
        /// </summary>
        /// <param name="part">The part to check. Null returns false.</param>
        public static bool HasRunFor(CorePartData part)
        {
            if (part == null)
            {
                return false;
            }
            return _cache.ContainsKey(part);
        }

        /// <summary>
        /// Stores <paramref name="issues" /> as the most recent expensive-run result for <paramref name="part" />.
        /// </summary>
        /// <param name="part">The part to associate the issues with. Null is a no-op.</param>
        /// <param name="issues">The expensive-run issues to cache.</param>
        public static void Set(CorePartData part, IReadOnlyList<ValidationIssue> issues)
        {
            if (part == null)
            {
                return;
            }
            _cache[part] = issues ?? Array.Empty<ValidationIssue>();
            Changed?.Invoke(part);
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
        /// Argument is the part whose entry changed. A null argument signals a full-cache invalidation
        /// so subscribers should re-render unconditionally rather than skip on part-mismatch.
        /// </remarks>
        public static event Action<CorePartData> Changed;
    }
}
