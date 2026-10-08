using System;
using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation
{
    /// <summary>
    /// Aggregated result of running every mission validator against a context.
    /// </summary>
    /// <remarks>
    /// Built fresh on each refresh by <see cref="Run" />. Validators that throw are logged and
    /// skipped so one bad validator cannot blank the report.
    /// </remarks>
    public readonly struct MissionValidationReport
    {
        /// <summary>Empty report. Returned when there is no mission to validate.</summary>
        public static readonly MissionValidationReport Empty = new(Array.Empty<ValidationIssue>(), 0, 0, 0);

        private MissionValidationReport(IReadOnlyList<ValidationIssue> issues, int infoCount, int warningCount, int errorCount)
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
        /// Runs every registered mission validator matching <paramref name="costFilter" /> against
        /// the mission wrapped by <paramref name="context" /> and aggregates the issues.
        /// </summary>
        /// <param name="context">The validation context. Null returns <see cref="Empty" />.</param>
        /// <param name="costFilter">Which validators to include. <see cref="ValidatorCost.Cheap" /> only is the per-tick default. Pass null to include both.</param>
        /// <param name="progress">Optional progress reporter, called once before each validator runs with (fraction in 0..1, validator type name).</param>
        /// <returns>The aggregated report covering every matched validator.</returns>
        public static MissionValidationReport Run(
            MissionValidationContext context,
            ValidatorCost? costFilter = ValidatorCost.Cheap,
            Action<float, string> progress = null)
        {
            if (context?.Mission == null)
            {
                return Empty;
            }

            var matched = new List<IValidator<MissionValidationContext>>();
            foreach (IValidator<MissionValidationContext> v in ValidatorRegistry<MissionValidationContext>.Validators)
            {
                if (costFilter.HasValue && v.Cost != costFilter.Value) continue;
                matched.Add(v);
            }

            var issues = new List<ValidationIssue>();
            int info = 0, warn = 0, err = 0;

            for (int i = 0; i < matched.Count; i++)
            {
                IValidator<MissionValidationContext> validator = matched[i];
                progress?.Invoke((float)i / matched.Count, validator.GetType().Name);

                IEnumerable<ValidationIssue> results;
                try
                {
                    results = validator.Validate(context) ?? Array.Empty<ValidationIssue>();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MissionValidationReport] Validator '{validator.GetType().FullName}' threw: {e}");
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
            return new MissionValidationReport(issues, info, warn, err);
        }

        /// <summary>
        /// Convenience overload that builds a <see cref="MissionValidationContext" /> from the given mission and runs the cheap validators.
        /// </summary>
        /// <param name="mission">The mission to validate. Null returns <see cref="Empty" />.</param>
        /// <returns>The aggregated cheap-cost report for <paramref name="mission" />.</returns>
        public static MissionValidationReport RunCheap(Mission mission)
        {
            if (mission == null) return Empty;
            return Run(new MissionValidationContext(mission), ValidatorCost.Cheap);
        }
    }
}
