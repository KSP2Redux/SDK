using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node with no English name or description.
    /// </summary>
    public class TechNodeTextValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node with no English name.</summary>
        public const string NAME_EMPTY_CODE = "TECHNODE_NAME_EMPTY";

        /// <summary>Stable code emitted for a node with no English description.</summary>
        public const string DESCRIPTION_EMPTY_CODE = "TECHNODE_DESCRIPTION_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(context.Node.EnglishName))
            {
                yield return new ValidationIssue(NAME_EMPTY_CODE, ValidationSeverity.Warning, $"{context.Id} has no name.");
            }

            if (string.IsNullOrWhiteSpace(context.Node.EnglishDescription))
            {
                yield return new ValidationIssue(DESCRIPTION_EMPTY_CODE, ValidationSeverity.Warning, $"{context.Name} has no description.");
            }
        }
    }
}
