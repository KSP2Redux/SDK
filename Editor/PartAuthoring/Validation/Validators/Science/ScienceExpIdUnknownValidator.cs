using System;
using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.PartAuthoring.Inspectors.Widgets;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Errors when an <c>ExperimentDefinitionID</c> is not in the addressables-loaded experiment
    /// catalog.
    /// </summary>
    /// <remarks>
    /// Same drop path as <c>SCIENCE_EXP_ID_EMPTY</c>: GetExperimentDefinition returns null for IDs
    /// the data store does not recognize. The inspector autocomplete prevents this proactively,
    /// but a copy-paste from an outdated config can land an unknown ID without warning.
    /// </remarks>
    public sealed class ScienceExpIdUnknownValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unknown experiment ID.</summary>
        public const string Code = "SCIENCE_EXP_ID_UNKNOWN";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
            if (modules == null)
            {
                yield break;
            }
            var known = new HashSet<string>(ExperimentNameCatalog.GetKnownExperiments(), StringComparer.Ordinal);
            foreach (var module in modules)
            {
                if (module is not Data_ScienceExperiment science || science.Experiments == null)
                {
                    continue;
                }
                for (int i = 0; i < science.Experiments.Count; i++)
                {
                    var row = science.Experiments[i];
                    if (string.IsNullOrEmpty(row.ExperimentDefinitionID))
                    {
                        continue;
                    }
                    if (known.Contains(row.ExperimentDefinitionID))
                    {
                        continue;
                    }
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Experiments[{i}].ExperimentDefinitionID = '{row.ExperimentDefinitionID}' is not in the experiment catalog.");
                }
            }
        }
    }
}
