using System.Collections.Generic;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation.Validators
{
    /// <summary>
    /// Errors when a <see cref="MissionBranch.TargetStage" /> references a StageID that no stage carries.
    /// </summary>
    /// <remarks>
    /// Checks stage-local branches on every stage, plus mission-scoped Exception and Prerequisite
    /// branches. The runtime logs an error and bails when a branch fires against a missing stage,
    /// so this validator catches the same failure ahead of runtime.
    /// </remarks>
    public sealed class BranchTargetMissingValidator : IMissionValidator
    {
        /// <summary>Stable code emitted when a branch targets a StageID that no stage carries.</summary>
        public const string Code = "BRANCH_TARGET_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(MissionValidationContext context)
        {
            if (context?.Data == null) yield break;
            var stagesById = context.StagesById;

            for (int i = 0; i < context.Stages.Count; i++)
            {
                var stage = context.Stages[i];
                if (stage?.branches == null) continue;
                for (int b = 0; b < stage.branches.Count; b++)
                {
                    var branch = stage.branches[b];
                    foreach (var issue in CheckBranch(branch, $"Stage {stage.StageID}", $"branch #{b}", stagesById))
                    {
                        yield return issue;
                    }
                }
            }

            for (int b = 0; b < context.ExceptionBranches.Count; b++)
            {
                foreach (var issue in CheckBranch(context.ExceptionBranches[b], "Mission", $"Exception branch #{b}", stagesById))
                {
                    yield return issue;
                }
            }

            for (int b = 0; b < context.PreRequisiteBranches.Count; b++)
            {
                foreach (var issue in CheckBranch(context.PreRequisiteBranches[b], "Mission", $"Prerequisite branch #{b}", stagesById))
                {
                    yield return issue;
                }
            }
        }

        private static IEnumerable<ValidationIssue> CheckBranch(
            MissionBranch branch,
            string sourceLabel,
            string branchLabel,
            IReadOnlyDictionary<int, MissionStage> stagesById)
        {
            if (branch == null) yield break;
            if (branch.TargetStage == MissionStage.INVALID_ID)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{sourceLabel}'s {branchLabel} has no target set (TargetStage = -1). Pick a destination stage.");
                yield break;
            }
            if (!stagesById.ContainsKey(branch.TargetStage))
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"{sourceLabel}'s {branchLabel} targets StageID {branch.TargetStage}, which does not exist in this mission. Retarget to a defined stage.");
            }
        }
    }
}
