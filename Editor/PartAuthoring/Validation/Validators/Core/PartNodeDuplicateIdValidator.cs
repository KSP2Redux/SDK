using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Core
{
    /// <summary>
    /// Errors when two or more attach nodes share the same <c>nodeID</c>.
    /// </summary>
    /// <remarks>
    /// Modules that look up an attach node by ID (decouplers, docking nodes, cargo bays) resolve
    /// the first match. Duplicate IDs make the resolution non-deterministic across module orders
    /// and confuse the author's mental model of which node a module is referencing.
    /// </remarks>
    public sealed class PartNodeDuplicateIdValidator : IPartValidator
    {
        /// <summary>Stable code emitted for each duplicate-ID group.</summary>
        public const string Code = "PART_NODE_DUPLICATE_ID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            List<AttachNodeDefinition> nodes = context?.Data?.attachNodes;
            if (nodes == null || nodes.Count < 2)
            {
                yield break;
            }

            var counts = new Dictionary<string, int>();
            foreach (AttachNodeDefinition node in nodes)
            {
                if (string.IsNullOrEmpty(node.nodeID))
                {
                    continue;
                }
                counts[node.nodeID] = counts.TryGetValue(node.nodeID, out int n) ? n + 1 : 1;
            }
            foreach (var kv in counts)
            {
                if (kv.Value < 2)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Attach node ID '{kv.Key}' used by {kv.Value} nodes. Module lookups resolve to the first match only.");
            }
        }
    }
}
