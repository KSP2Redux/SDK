using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node with no English description.
    /// </summary>
    public class TechNodeDescriptionEmptyValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node with no English description.</summary>
        public const string Code = "TECHNODE_DESCRIPTION_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.Node.EnglishDescription))
                yield break;

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, $"{context.Name} has no description.");
        }
    }
}
