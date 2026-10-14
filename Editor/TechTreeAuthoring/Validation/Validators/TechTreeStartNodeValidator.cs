using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes a tree whose start node is not one of its nodes, so the R&amp;D Center opens with nothing in focus.
    /// </summary>
    /// <remarks>
    /// A whole tree is checked against its own nodes. A patch is checked only when it changes the start node, against
    /// its own nodes and the reference nodes it keeps. A node asset and any other tree placed against a reference
    /// start from the reference tree's start node. A start node the tree names and lacks is a warning, a tree relying
    /// on the stock one is only noted.
    /// </remarks>
    public class TechTreeStartNodeValidator : ITechTreeValidator
    {
        /// <summary>Stable code emitted when the start node is not in the tree.</summary>
        public const string Code = "TECHTREE_START_NODE_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechTreeValidationContext context)
        {
            if (!IsChecked(context))
                yield break;

            TechTreeAsset tree = context.Tree;
            bool isNamed = !string.IsNullOrEmpty(tree.StartNodeId);
            string startNodeId = isNamed ? tree.StartNodeId : TechTreeAsset.DEFAULT_START_NODE_ID;
            if (HasNode(context, startNodeId))
                yield break;

            yield return new ValidationIssue(
                Code,
                isNamed ? ValidationSeverity.Warning : ValidationSeverity.Info,
                $"No node is {startNodeId}, the start node the R&D Center focuses on when it first opens."
            );
        }

        private static bool IsChecked(TechTreeValidationContext context) =>
            context.Tree is TechTreePatchAsset patch
                ? TechTreePatchBake.ChangesStartNode(patch)
                : context.Tree is not TechNodeAsset && context.Reference.Count == 0 && context.Tree.Nodes.Count > 0;

        // A patch's start node may be a reference node, unless the patch removes it
        private static bool HasNode(TechTreeValidationContext context, string nodeId) =>
            TechTreeEditing.Find(context.Tree, nodeId) != null
            || (context.Tree is TechTreePatchAsset patch
                && context.ReferenceIds.Contains(nodeId)
                && !patch.RemovedNodeIds.Contains(nodeId));
    }
}
