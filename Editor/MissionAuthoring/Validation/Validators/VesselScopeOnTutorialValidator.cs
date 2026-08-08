using System.Collections.Generic;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a tutorial or FTUE mission marks a stage as vessel scoped.
    /// </summary>
    /// <remarks>
    /// Neither kind persists per-vessel progress. Both are removed from the active mission list once complete
    /// and are skipped by the save path before it reaches the record vessel progress would live in, so the scope
    /// is read but can never do anything, and the stage behaves as though it were campaign scoped.
    /// </remarks>
    public sealed class VesselScopeOnTutorialValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when a tutorial or FTUE stage is marked vessel scoped.</summary>
        public const string Code = "STAGE_VESSEL_SCOPE_ON_TUTORIAL";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            MissionData data = context?.Data;
            if (data == null)
            {
                yield break;
            }

            if (data.type != MissionType.Tutorial && data.type != MissionType.FTUE)
            {
                yield break;
            }

            IReadOnlyList<MissionStage> stages = context.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                MissionStage stage = stages[i];
                if (stage == null || stage.ProgressScope != MissionProgressScope.Vessel)
                {
                    continue;
                }

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Stage at array index {i} is Vessel scoped, but a {data.type} mission stores no per-vessel " +
                    "progress and will run the stage as though it were Campaign scoped. Set it back to Campaign.");
            }
        }
    }
}
