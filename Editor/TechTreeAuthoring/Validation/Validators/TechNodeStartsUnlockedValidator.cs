using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes a node every campaign starts with, one that costs nothing and needs nothing.
    /// </summary>
    public class TechNodeStartsUnlockedValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node that starts unlocked.</summary>
        public const string Code = "TECHNODE_STARTS_UNLOCKED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (!TechTreePreview.GetStartingUnlocked(new[] { context.Node }).Contains(context.Id))
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Info,
                $"{context.Name} costs nothing and needs nothing, so every campaign starts with it."
            );
        }
    }
}
