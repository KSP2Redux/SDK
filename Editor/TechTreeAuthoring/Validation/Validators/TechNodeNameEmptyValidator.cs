using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node with no English name.
    /// </summary>
    public class TechNodeNameEmptyValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node with no English name.</summary>
        public const string Code = "TECHNODE_NAME_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (!string.IsNullOrWhiteSpace(context.Node.EnglishName))
                yield break;

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, $"{context.Id} has no name.");
        }
    }
}
