using System.Collections.Generic;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when two or more stages share the same <see cref="MissionStage.StageID" />.
    /// </summary>
    /// <remarks>
    /// Branch resolution at runtime uses <c>missionStages.Find(s => s.StageID == target)</c>, which
    /// returns the first match. Duplicate IDs silently route branches to the wrong stage.
    /// </remarks>
    public sealed class StageDuplicateIdValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when two stages share a StageID.</summary>
        public const string Code = "STAGE_DUPLICATE_ID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Data == null) yield break;
            var stages = context.Stages;
            if (stages.Count < 2) yield break;

            var seen = new Dictionary<int, int>();
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null) continue;
                if (stage.StageID == MissionStage.INVALID_ID) continue;
                if (seen.TryGetValue(stage.StageID, out int firstIdx))
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Stages at array index {firstIdx} and {i} share StageID = {stage.StageID}. Renumber one of them so branch targets resolve unambiguously.");
                }
                else
                {
                    seen[stage.StageID] = i;
                }
            }
        }
    }
}
