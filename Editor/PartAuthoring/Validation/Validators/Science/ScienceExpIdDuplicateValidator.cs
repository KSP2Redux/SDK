using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Warns when two or more <c>Experiments</c> rows share the same
    /// <c>ExperimentDefinitionID</c>.
    /// </summary>
    /// <remarks>
    /// Module_ScienceExperiment.InitializePAMItems keys PAM actions by experiment ID. Duplicate
    /// keys collide and only one PAM button surfaces in flight.
    /// </remarks>
    public sealed class ScienceExpIdDuplicateValidator : IPartValidator
    {
        /// <summary>Stable code emitted per duplicate-ID group.</summary>
        public const string Code = "SCIENCE_EXP_ID_DUPLICATE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_ScienceExperiment science || science.Experiments == null)
                {
                    continue;
                }
                var counts = new Dictionary<string, int>();
                foreach (var row in science.Experiments)
                {
                    if (string.IsNullOrEmpty(row.ExperimentDefinitionID))
                    {
                        continue;
                    }
                    counts[row.ExperimentDefinitionID] = counts.TryGetValue(row.ExperimentDefinitionID, out int n) ? n + 1 : 1;
                }
                foreach (var kv in counts)
                {
                    if (kv.Value < 2)
                    {
                        continue;
                    }
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Experiment ID '{kv.Key}' appears in {kv.Value} rows. PAM actions collide - only the last button surfaces.");
                }
            }
        }
    }
}
