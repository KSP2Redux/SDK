using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a node shares its ID with an earlier node of the tree.
    /// </summary>
    /// <remarks>
    /// The game keeps only the first node with an ID, so the later one never shows. The fix gives it a free ID.
    /// </remarks>
    public class TechNodeIdDuplicateValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a node whose ID an earlier node has.</summary>
        public const string Code = "TECHNODE_ID_DUPLICATE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            if (context.EarlierNodes.All(node => node.Data?.ID != context.Id))
                yield break;

            TechTreeNode node = context.Node;
            TechTreeAsset tree = context.Tree.Tree;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"{context.Id} is used by more than one node, and the game keeps only the first.",
                new[]
                {
                    new ValidationFix("Rename", () => context.Tree.Change("Rename Tech Node",
                        () => node.Data.ID = TechTreeEditing.MakeUniqueId(tree, node.Data.ID, context.Tree.Reference))),
                }
            );
        }
    }
}
