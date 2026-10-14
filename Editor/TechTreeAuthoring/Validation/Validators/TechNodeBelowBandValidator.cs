using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes a node below the band the R&amp;D Center shows without scrolling.
    /// </summary>
    /// <remarks>
    /// The R&amp;D Center scrolls vertically to show such a node, so it only needs noting.
    /// </remarks>
    public class TechNodeBelowBandValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node below the band.</summary>
        public const string Code = "TECHNODE_BELOW_BAND";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (context.Node.Data.TechTreePosition.y >= TechTreeCanvasGeometry.GetLowestY())
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Info,
                $"{context.Name} sits below the band, so the R&D Center scrolls vertically to show it."
            );
        }
    }
}
