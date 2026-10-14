using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a node has the ID of a reference node.
    /// </summary>
    /// <remarks>
    /// With both enabled the game keeps only one of the two, and which one depends on load order.
    /// </remarks>
    public class TechNodeIdInReferenceValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node whose ID the reference tree has.</summary>
        public const string Code = "TECHNODE_ID_IN_REFERENCE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            // A patch's override shares its ID with the reference node on purpose
            bool isOverride = context.Tree.Tree is TechTreePatchAsset patch && patch.IsOverride(context.Id);
            if (isOverride || !context.Tree.ReferenceIds.Contains(context.Id))
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"{context.Id} is also a node of the reference tree. With both enabled the game keeps only one."
            );
        }
    }
}
