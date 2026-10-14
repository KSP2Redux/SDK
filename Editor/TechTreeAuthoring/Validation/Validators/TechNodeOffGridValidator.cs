using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes a node off the stock grid while the editor snaps to it.
    /// </summary>
    public class TechNodeOffGridValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node off the stock grid.</summary>
        public const string Code = "TECHNODE_OFF_GRID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            Vector2 snapped = TechTreeCanvasGeometry.Snap(node.Data.TechTreePosition);
            if (!context.Tree.SnapsToGrid || snapped == node.Data.TechTreePosition)
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Info,
                $"{context.Name} is off the stock grid.",
                new[]
                {
                    new ValidationFix("Snap to grid", () => context.Tree.Change("Move Tech Node",
                        () => node.Data.TechTreePosition = snapped)),
                }
            );
        }
    }
}
