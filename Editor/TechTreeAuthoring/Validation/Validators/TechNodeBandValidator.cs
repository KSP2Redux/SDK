using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node whose top the R&amp;D Center cuts off above its band, and notes one below it.
    /// </summary>
    /// <remarks>
    /// The R&amp;D Center scrolls vertically for nodes below the band, so those only need noting. Nothing scrolls up.
    /// The fix moves a node down to the highest row that shows whole.
    /// </remarks>
    public class TechNodeBandValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node above the band.</summary>
        public const string ABOVE_BAND_CODE = "TECHNODE_ABOVE_BAND";

        /// <summary>Stable code emitted for a node below the band.</summary>
        public const string BELOW_BAND_CODE = "TECHNODE_BELOW_BAND";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            Vector2 position = node.Data.TechTreePosition;
            if (position.y > TechTreeCanvasGeometry.GetHighestY(false))
            {
                float highest = TechTreeCanvasGeometry.GetHighestY(context.Tree.SnapsToGrid);
                yield return new ValidationIssue(
                    ABOVE_BAND_CODE,
                    ValidationSeverity.Warning,
                    $"{context.Name} sits above the band the R&D Center shows, so its top is cut off.",
                    new[]
                    {
                        new ValidationFix("Move into the band", () => context.Tree.Change("Move Tech Node",
                            () => node.Data.TechTreePosition = new Vector2(position.x, highest))),
                    }
                );
            }

            if (position.y < TechTreeCanvasGeometry.GetLowestY())
            {
                yield return new ValidationIssue(
                    BELOW_BAND_CODE,
                    ValidationSeverity.Info,
                    $"{context.Name} sits below the band, so the R&D Center scrolls vertically to show it."
                );
            }
        }
    }
}
