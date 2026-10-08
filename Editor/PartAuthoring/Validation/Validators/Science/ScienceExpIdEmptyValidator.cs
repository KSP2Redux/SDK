using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Errors when a row in <c>Data_ScienceExperiment.Experiments</c> has an empty
    /// <c>ExperimentDefinitionID</c>.
    /// </summary>
    /// <remarks>
    /// ScienceExperimentsDataStore.GetExperimentDefinition returns null for empty keys, so the row
    /// contributes no runtime behaviour and silently disappears from the PAM.
    /// </remarks>
    public sealed class ScienceExpIdEmptyValidator : IPartValidator
    {
        /// <summary>Stable code emitted per row with an empty ID.</summary>
        public const string Code = "SCIENCE_EXP_ID_EMPTY";

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
                for (int i = 0; i < science.Experiments.Count; i++)
                {
                    var row = science.Experiments[i];
                    if (!string.IsNullOrWhiteSpace(row.ExperimentDefinitionID))
                    {
                        continue;
                    }
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Experiments[{i}].ExperimentDefinitionID is empty. The row produces no runtime behaviour.");
                }
            }
        }
    }
}
