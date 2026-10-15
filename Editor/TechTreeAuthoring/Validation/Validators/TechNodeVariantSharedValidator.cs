using System;
using System.Collections.Generic;
using System.Linq;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on each part variant a node unlocks that a reference node or an earlier node unlocks too.
    /// </summary>
    /// <remarks>
    /// The game unlocks a variant with the first node it loads that names it, as it does a part. The fix removes the
    /// variant from this node.
    /// </remarks>
    public class TechNodeVariantSharedValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a variant another node unlocks too.</summary>
        public const string Code = "TECHNODE_VARIANT_SHARED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            var owners = new Dictionary<(string, string, string), string>();
            // An override replaces its reference node, so only the reference nodes still loading as they are count
            foreach (TechTreeNode other in context.ReferenceOnlyNodes.Concat(context.EarlierNodes).Where(other => other.Data?.ID != null))
            {
                foreach (TechUnlockedVariant variant in other.Data.UnlockedVariants ?? Array.Empty<TechUnlockedVariant>())
                {
                    if (variant != null)
                    {
                        owners.TryAdd(KeyOf(variant), other.Data.ID);
                    }
                }
            }

            var seen = new HashSet<(string, string, string)>();
            foreach (TechUnlockedVariant variant in node.Data.UnlockedVariants ?? Array.Empty<TechUnlockedVariant>())
            {
                if (variant == null || !seen.Add(KeyOf(variant)) || !owners.TryGetValue(KeyOf(variant), out string owner))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Variant {variant.VariantId} of {variant.PartName} is also unlocked by {owner}, and the game unlocks it with the first node it loads.",
                    new[]
                    {
                        new ValidationFix("Remove from this node", () => context.Tree.Change("Remove Tech Node Variant",
                            () => node.Data.UnlockedVariants = node.Data.UnlockedVariants.Where(other => other != variant).ToArray())),
                    }
                );
            }
        }

        private static (string, string, string) KeyOf(TechUnlockedVariant variant) =>
            (variant.PartName, variant.VariantSetId, variant.VariantId);
    }
}
