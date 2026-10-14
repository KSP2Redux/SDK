using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node with no icon.
    /// </summary>
    /// <remarks>
    /// An address is never checked against the project, since stock icons and other mods' are not in it.
    /// </remarks>
    public class TechNodeIconValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node with no icon.</summary>
        public const string Code = "TECHNODE_ICON_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (!string.IsNullOrEmpty(context.Node.Data.IconID))
                yield break;

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, $"{context.Name} has no icon.");
        }
    }
}
