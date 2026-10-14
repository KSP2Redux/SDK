using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes a tree whose start node is not one of its nodes, so the R&amp;D Center opens with nothing in focus.
    /// </summary>
    /// <remarks>
    /// Only a whole tree is checked. A node asset and a tree placed against a reference start from the reference
    /// tree's start node. A start node the tree names and lacks is a warning, a tree relying on the stock one is only
    /// noted.
    /// </remarks>
    public class TechTreeStartNodeValidator : ITechTreeValidator
    {
        /// <summary>Stable code emitted when the start node is not in the tree.</summary>
        public const string Code = "TECHTREE_START_NODE_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechTreeValidationContext context)
        {
            TechTreeAsset tree = context.Tree;
            if (tree is TechNodeAsset || context.Reference.Count > 0 || tree.Nodes.Count == 0)
                yield break;

            bool isNamed = !string.IsNullOrEmpty(tree.StartNodeId);
            string startNodeId = isNamed ? tree.StartNodeId : TechTreeAsset.DEFAULT_START_NODE_ID;
            if (TechTreeEditing.Find(tree, startNodeId) != null)
                yield break;

            yield return new ValidationIssue(
                Code,
                isNamed ? ValidationSeverity.Warning : ValidationSeverity.Info,
                $"No node is {startNodeId}, the start node the R&D Center focuses on when it first opens."
            );
        }
    }
}
