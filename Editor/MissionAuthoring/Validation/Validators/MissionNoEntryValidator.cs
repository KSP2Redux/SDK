using System.Collections.Generic;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when the first stage exists but its <see cref="MissionStage.StageID" /> is unset.
    /// </summary>
    /// <remarks>
    /// The empty-list case is handled by <see cref="MissionNoStagesValidator" />. This validator
    /// covers the disjoint case where the list has entries but the entry stage (array index 0)
    /// carries the <see cref="MissionStage.INVALID_ID" /> sentinel. Branch targets that point at
    /// the entry stage by ID will fail to resolve.
    /// </remarks>
    public sealed class MissionNoEntryValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when the entry stage's ID is unset.</summary>
        public const string Code = "MISSION_NO_ENTRY";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Data == null) yield break;
            var stages = context.Stages;
            if (stages.Count == 0) yield break;
            var entry = stages[0];
            if (entry == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "Mission's entry stage (array index 0) is null. Replace it with a valid stage.");
                yield break;
            }
            if (entry.StageID == MissionStage.INVALID_ID)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    "Mission's entry stage (array index 0) has no StageID set. Assign a non-negative StageID so branches can target it.");
            }
        }
    }
}
