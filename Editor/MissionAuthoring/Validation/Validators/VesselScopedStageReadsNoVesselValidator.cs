using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using KSP.Messages.PropertyWatchers;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns when a vessel scoped stage has no condition that reads the active vessel.
    /// </summary>
    /// <remarks>
    /// A vessel scoped stage is only evaluated while a vessel is active, so one built entirely from assembly
    /// building or campaign wide conditions can never complete: the conditions are only reachable somewhere the
    /// stage does not run. Such a stage belongs in a campaign scoped run.
    /// <para>
    /// Only raised when every condition in the tree can be shown not to read a vessel. A tree holding an event or
    /// script condition says nothing either way, so it is left alone rather than warned about.
    /// </para>
    /// </remarks>
    public sealed class VesselScopedStageReadsNoVesselValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when a vessel scoped stage reads no vessel.</summary>
        public const string Code = "STAGE_VESSEL_SCOPE_READS_NO_VESSEL";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Data == null)
                yield break;

            IReadOnlyList<MissionStage> stages = context.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                MissionStage stage = stages[i];
                if (stage == null || stage.ProgressScope != MissionProgressScope.Vessel || stage.condition == null)
                    continue;

                if (ReadsVesselOrCannotTell(stage.condition))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Stage at array index {i} is Vessel scoped but no condition in it reads the active vessel. " +
                    "A Vessel scoped stage only runs while a vessel is active, so this stage can never complete. " +
                    "Set it to Campaign, or give it a condition that reads the vessel.");
            }
        }

        private static bool ReadsVesselOrCannotTell(Condition condition)
        {
            switch (condition)
            {
                case null:
                    return false;
                case ConditionSet conditionSet:
                {
                    if (conditionSet.Children == null)
                        return false;

                    foreach (Condition child in conditionSet.Children)
                    {
                        if (ReadsVesselOrCannotTell(child))
                            return true;
                    }

                    return false;
                }
                case PropertyCondition propertyCondition:
                {
                    Type watcher = Type.GetType(propertyCondition.PropertyTypeAQN);

                    // An unresolvable watcher is a separate problem, and guessing at it here would produce a
                    // warning about the wrong thing.
                    return watcher == null || typeof(VehiclePropertyWatcher).IsAssignableFrom(watcher);
                }
                default:
                    // An event or script condition can reach anything, so it is not evidence either way.
                    return true;
            }
        }
    }
}
