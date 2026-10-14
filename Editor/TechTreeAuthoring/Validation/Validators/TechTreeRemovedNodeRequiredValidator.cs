using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Reports each node a patch removes that a node left in the tree still requires, as an error.
    /// </summary>
    /// <remarks>
    /// A node that requires a removed node can never be unlocked. The patch's own nodes count, and so do the
    /// reference's nodes it neither removes nor changes. The fix restores the removed node.
    /// </remarks>
    public class TechTreeRemovedNodeRequiredValidator : ITechTreeValidator
    {
        /// <summary>Stable code emitted for a removed node another node still requires.</summary>
        public const string Code = "TECHTREE_REMOVED_NODE_REQUIRED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechTreeValidationContext context)
        {
            if (context.Tree is not TechTreePatchAsset patch || patch.RemovedNodeIds.Count == 0)
                yield break;

            var removed = new HashSet<string>(patch.RemovedNodeIds);
            List<TechTreeNode> remaining = patch.Nodes
                .Concat(context.Reference.Where(node =>
                    node.Data?.ID != null && !removed.Contains(node.Data.ID) && TechTreeEditing.Find(patch, node.Data.ID) == null))
                .Where(node => node.Data?.RequiredTechNodeIDs != null)
                .ToList();
            foreach (string removedId in removed)
            {
                List<string> dependents = remaining
                    .Where(node => node.Data.RequiredTechNodeIDs.Contains(removedId))
                    .Select(TechTreeValidationContext.NameOf)
                    .ToList();
                if (dependents.Count == 0)
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"The patch removes {removedId}, which {string.Join(", ", dependents)} still require, so they can never be unlocked.",
                    new[]
                    {
                        new ValidationFix("Restore", () => context.Change("Restore Tech Node",
                            () => TechTreeEditing.RestoreReferenceNode(patch, removedId))),
                    }
                );
            }
        }
    }
}
