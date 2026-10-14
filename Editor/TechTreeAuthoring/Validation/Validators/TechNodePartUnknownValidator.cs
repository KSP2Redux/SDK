using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes each part a node unlocks that the project does not know.
    /// </summary>
    /// <remarks>
    /// Only a note, since a tree may unlock another mod's parts. Expensive, as the project's parts are loaded to know
    /// them.
    /// </remarks>
    public class TechNodePartUnknownValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a part the project does not know.</summary>
        public const string Code = "TECHNODE_PART_UNKNOWN";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            HashSet<string> known = context.Tree.KnownParts;
            if (known == null)
                yield break;

            foreach (string part in (context.Node.Data.UnlockedPartsIDs ?? Array.Empty<string>()).Distinct())
            {
                if (string.IsNullOrEmpty(part) || known.Contains(part))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"{context.Name} unlocks {part}, which is not a part this project knows."
                );
            }
        }
    }
}
