using System;
using System.Collections.Generic;
using System.Linq;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.TechTreeAuthoring.Validation.Validators
{
    /// <summary>
    /// Notes each mission a node requires that the project does not know.
    /// </summary>
    /// <remarks>
    /// Only a note, since a tree may gate on another mod's missions. Expensive, as the project's missions are loaded
    /// to know them.
    /// </remarks>
    public class TechNodeMissionUnknownValidator : ITechNodeValidator
    {
        /// <summary>Stable code emitted for a mission the project does not know.</summary>
        public const string Code = "TECHNODE_MISSION_UNKNOWN";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(TechNodeValidationContext context)
        {
            HashSet<string> known = context.Tree.KnownMissions;
            if (known == null)
                yield break;

            foreach (string mission in (context.Node.Data.RequiredMissionIDs ?? Array.Empty<string>()).Distinct())
            {
                if (string.IsNullOrEmpty(mission) || known.Contains(mission))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"{context.Name} requires the mission {mission}, which this project does not know."
                );
            }
        }
    }
}
