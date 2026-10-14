using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors on each prerequisite of a node that is not a node of the tree or the reference tree.
    /// </summary>
    /// <remarks>
    /// The game draws no line to it and can never unlock the node. The fix removes the link.
    /// </remarks>
    public class TechNodePrerequisiteMissingValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a prerequisite that is not a node.</summary>
        public const string Code = "TECHNODE_PREREQUISITE_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeAsset tree = context.Tree.Tree;
            string id = context.Id;
            foreach (string prerequisiteId in (context.Node.Data.RequiredTechNodeIDs ?? Array.Empty<string>()).Distinct())
            {
                if (context.Tree.FindAny(prerequisiteId) != null)
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{context.Name} requires {prerequisiteId}, which is not a node, so it can never be unlocked.",
                    new[]
                    {
                        new ValidationFix("Remove link", () => context.Tree.Change("Remove Tech Node Prerequisite",
                            () => TechTreeEditing.RemovePrerequisite(tree, id, prerequisiteId))),
                    }
                );
            }
        }
    }
}
