using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns on each part a node unlocks that a reference node or an earlier node unlocks too.
    /// </summary>
    /// <remarks>
    /// The game unlocks a part with the first node it loads that names it. The fix removes the part from this node.
    /// </remarks>
    public class TechNodePartSharedValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a part another node unlocks too.</summary>
        public const string Code = "TECHNODE_PART_SHARED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            TechTreeNode node = context.Node;
            var owners = new Dictionary<string, string>();
            foreach (TechTreeNode other in context.Tree.Reference.Concat(context.EarlierNodes).Where(other => other.Data?.ID != null))
            {
                foreach (string part in other.Data.UnlockedPartsIDs ?? Array.Empty<string>())
                {
                    owners.TryAdd(part, other.Data.ID);
                }
            }

            foreach (string part in (node.Data.UnlockedPartsIDs ?? Array.Empty<string>()).Distinct())
            {
                if (string.IsNullOrEmpty(part) || !owners.TryGetValue(part, out string owner))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"{part} is also unlocked by {owner}, and the game unlocks it with the first node it loads.",
                    new[]
                    {
                        new ValidationFix("Remove from this node", () => context.Tree.Change("Remove Tech Node Part",
                            () => node.Data.UnlockedPartsIDs = node.Data.UnlockedPartsIDs.Where(other => other != part).ToArray())),
                    }
                );
            }
        }
    }
}
