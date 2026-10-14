using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on a node whose top the R&amp;D Center cuts off above its band.
    /// </summary>
    /// <remarks>
    /// Nothing scrolls up, so the fix moves the node down to the highest row that shows whole.
    /// </remarks>
    public class TechNodeAboveBandValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node above the band.</summary>
        public const string Code = "TECHNODE_ABOVE_BAND";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            Vector2 position = node.Data.TechTreePosition;
            if (position.y <= TechTreeCanvasGeometry.GetHighestY(false))
                yield break;

            float highest = TechTreeCanvasGeometry.GetHighestY(context.Tree.SnapsToGrid);
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"{context.Name} sits above the band the R&D Center shows, so its top is cut off.",
                new[]
                {
                    new ValidationFix("Move into the band", () => context.Tree.Change("Move Tech Node",
                        () => node.Data.TechTreePosition = new Vector2(position.x, highest))),
                }
            );
        }
    }
}
