using System.Collections.Generic;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when <c>missionStages</c> is empty.
    /// </summary>
    /// <remarks>
    /// The runtime logs an error and refuses to activate a mission with zero stages.
    /// </remarks>
    public sealed class MissionNoStagesValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when the mission has no stages.</summary>
        public const string Code = "MISSION_NO_STAGES";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Data == null) yield break;
            if (context.Stages.Count > 0) yield break;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Mission has no stages. Add at least one stage (the strip's + card) so the mission has an entry point.");
        }
    }
}
