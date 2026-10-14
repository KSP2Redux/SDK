using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors on each node of a tree that has no ID.
    /// </summary>
    /// <remarks>
    /// A node without an ID cannot bake, and nothing can require it.
    /// </remarks>
    public class TechTreeNodeIdEmptyValidator : ITechTreeValidator
    {
        /// <summary>Stable code emitted for a node with no ID.</summary>
        public const string Code = "TECHTREE_NODE_ID_EMPTY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechTreeValidationContext context)
        {
            for (int i = 0; i < context.Tree.Nodes.Count; i++)
            {
                if (string.IsNullOrEmpty(context.Tree.Nodes[i].Data?.ID))
                {
                    yield return new ValidationIssue(Code, ValidationSeverity.Error, $"Node {i + 1} has no ID, so it cannot bake.");
                }
            }
        }
    }
}
