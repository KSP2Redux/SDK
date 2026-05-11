using System;
using System.Collections.Generic;
using KSP;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation
{
    /// <summary>
    /// Aggregated result of running every validator against a body.
    /// </summary>
    /// <remarks>
    /// Built fresh on each refresh by <see cref="Run" />. Validators that throw are logged and skipped so one bad validator cannot blank the section.
    /// </remarks>
    public readonly struct PlanetValidationReport
    {
        /// <summary>
        /// Empty report.
        /// </summary>
        /// <remarks>
        /// Returned when there is no body to validate.
        /// </remarks>
        public static readonly PlanetValidationReport Empty = new(Array.Empty<ValidationIssue>(), 0, 0, 0);

        private PlanetValidationReport(IReadOnlyList<ValidationIssue> issues, int infoCount, int warningCount, int errorCount)
        {
            Issues = issues;
            InfoCount = infoCount;
            WarningCount = warningCount;
            ErrorCount = errorCount;
        }

        /// <summary>All issues produced by all validators, in registration-then-emit order.</summary>
        public IReadOnlyList<ValidationIssue> Issues { get; }

        /// <summary>Number of <see cref="ValidationSeverity.Info" /> issues.</summary>
        public int InfoCount { get; }

        /// <summary>Number of <see cref="ValidationSeverity.Warning" /> issues.</summary>
        public int WarningCount { get; }

        /// <summary>Number of <see cref="ValidationSeverity.Error" /> issues.</summary>
        public int ErrorCount { get; }

        /// <summary>True when no validator produced any issue.</summary>
        public bool IsClean => Issues.Count == 0;

        /// <summary>
        /// Runs every registered validator matching <paramref name="costFilter" /> against
        /// <paramref name="body" /> and aggregates the issues.
        /// </summary>
        /// <param name="body">The body to validate. Null returns <see cref="Empty" />.</param>
        /// <param name="costFilter">
        /// Which validators to include. <see cref="ValidatorCost.Cheap" /> only is the per-tick
        /// default. Pass null to include both (used by the "Run full validation" action).
        /// </param>
        /// <param name="progress">
        /// Optional progress reporter, called once before each validator runs with
        /// (fraction in 0..1, validator type name). Used to drive a progress bar.
        /// </param>
        /// <returns>The aggregated report.</returns>
        public static PlanetValidationReport Run(
            CoreCelestialBodyData body,
            ValidatorCost? costFilter = ValidatorCost.Cheap,
            Action<float, string> progress = null)
        {
            if (body == null)
                return Empty;

            var issues = new List<ValidationIssue>();
            int info = 0, warn = 0, err = 0;

            // Pre-filter so the progress fraction reflects the validators that will actually run.
            var matched = new List<IPlanetValidator>();
            foreach (IPlanetValidator v in PlanetValidatorRegistry.Validators)
                if (!costFilter.HasValue || v.Cost == costFilter.Value)
                    matched.Add(v);

            for (int i = 0; i < matched.Count; i++)
            {
                IPlanetValidator validator = matched[i];
                progress?.Invoke((float)i / matched.Count, validator.GetType().Name);

                IEnumerable<ValidationIssue> results;
                try
                {
                    results = validator.Validate(body) ?? Array.Empty<ValidationIssue>();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[PlanetValidationReport] Validator '{validator.GetType().FullName}' threw: {e}");
                    continue;
                }

                foreach (ValidationIssue issue in results)
                {
                    issues.Add(issue);
                    switch (issue.Severity)
                    {
                        case ValidationSeverity.Info: info++; break;
                        case ValidationSeverity.Warning: warn++; break;
                        case ValidationSeverity.Error: err++; break;
                    }
                }
            }
            progress?.Invoke(1f, "Done");
            return new PlanetValidationReport(issues, info, warn, err);
        }
    }
}
