using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on each part variant a node unlocks that is missing its part, its variant set or its own ID.
    /// </summary>
    /// <remarks>
    /// Such a variant names nothing, so it unlocks nothing. A name is never checked against the project, since stock
    /// parts and other mods' are not in it. The fix removes the variant from the node.
    /// </remarks>
    public class TechNodeVariantIncompleteValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a variant missing its part, set or ID.</summary>
        public const string Code = "TECHNODE_VARIANT_INCOMPLETE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            foreach (TechUnlockedVariant variant in node.Data.UnlockedVariants ?? Array.Empty<TechUnlockedVariant>())
            {
                if (variant != null &&
                    !string.IsNullOrEmpty(variant.PartName) &&
                    !string.IsNullOrEmpty(variant.VariantSetId) &&
                    !string.IsNullOrEmpty(variant.VariantId))
                {
                    continue;
                }

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"{context.Name} unlocks a variant without a part, a variant set or a variant, which unlocks nothing.",
                    new[]
                    {
                        new ValidationFix("Remove from this node", () => context.Tree.Change("Remove Tech Node Variant",
                            () => node.Data.UnlockedVariants = node.Data.UnlockedVariants.Where(other => other != variant).ToArray())),
                    }
                );
            }
        }
    }
}
