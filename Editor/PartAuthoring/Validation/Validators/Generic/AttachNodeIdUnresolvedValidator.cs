using System;
using System.Collections.Generic;
using KSP.Sim.Definitions;
using Ksp2UnityTools.Editor.Validation;
using Redux.Modules.Attributes;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Generic
{
    /// <summary>
    /// Errors when an <c>[AttachNodeId]</c>-decorated field references a node ID that is not in
    /// <see cref="PartData.attachNodes" />.
    /// </summary>
    /// <remarks>
    /// Pairs with the inspector autocomplete widget at AttachNodeIdField. Empty values are skipped
    /// (LiftingSurface.attachNodeName and ResourceIntake.occludeNode are intentionally blank when
    /// the gate is off). Non-empty unresolved values mean the runtime cannot find the attached
    /// part on undock / decouple / cargo-bay-occlusion checks - the module silently misbehaves.
    /// </remarks>
    public sealed class AttachNodeIdUnresolvedValidator : IPartValidator
    {
        /// <summary>Stable code emitted per unresolved attach-node ID.</summary>
        public const string Code = "ATTACH_NODE_ID_UNRESOLVED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            if (context?.Part == null)
            {
                yield break;
            }
            List<AttachNodeDefinition> nodes = context.Data?.attachNodes;
            var known = new HashSet<string>(StringComparer.Ordinal);
            if (nodes != null)
            {
                foreach (AttachNodeDefinition node in nodes)
                {
                    if (!string.IsNullOrEmpty(node.nodeID))
                    {
                        known.Add(node.nodeID);
                    }
                }
            }

            foreach (var fieldRef in ModuleFieldEnumerator.EnumerateStringFieldsWithAttribute<AttachNodeIdAttribute>(context.ModuleDatas))
            {
                if (string.IsNullOrEmpty(fieldRef.Value))
                {
                    continue;
                }
                if (known.Contains(fieldRef.Value))
                {
                    continue;
                }
                string available = known.Count == 0
                    ? "(no attach nodes declared)"
                    : string.Join(", ", known);
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{fieldRef.DisplayPath} = '{fieldRef.Value}' does not match any attach node ID. Available: {available}.");
            }
        }
    }
}
