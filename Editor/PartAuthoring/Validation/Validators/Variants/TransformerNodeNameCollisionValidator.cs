using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;
using VSwift.Modules.Data;
using VSwift.Modules.Transformers;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Variants
{
    /// <summary>
    /// Info-level note when an <see cref="AttachNodeAdder" /> entry's nodeID matches a node already on the part.
    /// </summary>
    /// <remarks>
    /// The runtime treats matching IDs as the "reposition existing" path per the field's own tooltip, so a collision is not an error. The type name reads as strictly additive, so flagging the dual-semantics case is worth a glance.
    /// No auto-fix: the right action depends on author intent. If the author wanted to add a brand-new node, the entry should be renamed. If the author wanted to reposition, <see cref="AttachNodeMover" /> reads more clearly. Either way it is an authoring call, not a mechanical one.
    /// </remarks>
    public sealed class TransformerNodeNameCollisionValidator : IPartValidator
    {
        /// <summary>Stable code emitted per name match between AttachNodeAdder entries and the part's existing nodes.</summary>
        public const string Code = "TRANSFORMER_NODE_NAME_COLLISION";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            Data_PartSwitch data = VariantValidationHelper.FindData(context);
            if (data?.VariantSets == null)
            {
                yield break;
            }

            var existingNodeIds = new HashSet<string>();
            var existing = context?.Data?.attachNodes;
            if (existing != null)
            {
                foreach (var n in existing)
                {
                    if (!string.IsNullOrEmpty(n.nodeID))
                    {
                        existingNodeIds.Add(n.nodeID);
                    }
                }
            }
            if (existingNodeIds.Count == 0)
            {
                yield break;
            }

            foreach (var set in data.VariantSets)
            {
                if (set?.Variants == null) continue;
                foreach (var variant in set.Variants)
                {
                    if (variant?.Transformers == null) continue;
                    foreach (var transformer in variant.Transformers)
                    {
                        if (transformer is not AttachNodeAdder ana || ana.Nodes == null)
                        {
                            continue;
                        }
                        foreach (var def in ana.Nodes)
                        {
                            string id = def.nodeID;
                            if (string.IsNullOrEmpty(id) || !existingNodeIds.Contains(id))
                            {
                                continue;
                            }
                            yield return new ValidationIssue(
                                Code,
                                ValidationSeverity.Info,
                                $"AttachNodeAdder entry '{id}' (variant '{variant.VariantId}' in set '{set.VariantSetId}') matches an existing attach node. The runtime will reposition the existing node rather than add a new one. Consider AttachNodeMover if reposition is the intent.");
                        }
                    }
                }
            }
        }
    }
}
