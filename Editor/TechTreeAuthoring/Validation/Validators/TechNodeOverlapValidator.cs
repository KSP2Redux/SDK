using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;
using UnityEngine;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns when a node's frame overlaps an earlier node's or a reference node's.
    /// </summary>
    /// <remarks>
    /// A pair of the tree's own nodes is reported once, on the later of the two.
    /// </remarks>
    public class TechNodeOverlapValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node drawn over another.</summary>
        public const string Code = "TECHNODE_OVERLAP";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            Vector2 position = context.Node.Data.TechTreePosition;
            TechTreeNode overlapped = context.EarlierNodes
                .Concat(context.ReferenceOnlyNodes)
                .FirstOrDefault(other => other.Data != null && TechTreeCanvasGeometry.FramesOverlap(position, other.Data.TechTreePosition));
            if (overlapped == null)
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"{context.Name} overlaps {TechTreeValidationContext.NameOf(overlapped)}."
            );
        }
    }
}
