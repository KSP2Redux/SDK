using System;
using System.Collections.Generic;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;

namespace Ksp2UnityTools.Editor.MissionAuthoring.Validation
{
    /// <summary>
    /// Memoized inputs shared across all validators in a single run.
    /// </summary>
    /// <remarks>
    /// Built once per validation tick by the runner. The lazy <see cref="StagesById" /> lookup is
    /// the critical reverse-lookup used by every validator that follows a branch's
    /// <see cref="MissionBranch.TargetStage" />. Validators never instantiate this directly. The
    /// runner owns construction so the per-run memoization scope matches one inspector tick.
    /// </remarks>
    public sealed class MissionValidationContext
    {
        private readonly Lazy<IReadOnlyDictionary<int, MissionStage>> _stagesById;

        /// <summary>
        /// Constructs a context wrapping <paramref name="mission" />.
        /// </summary>
        /// <param name="mission">The mission whose validation surfaces the context exposes. Null produces an empty context whose memoized fields return empty collections.</param>
        /// <param name="isProjectScanAvailable">When true, Expensive-cost cross-mission validators may walk other Mission assets in the project. Set false when running in an SDK environment without the full project catalog.</param>
        public MissionValidationContext(Mission mission, bool isProjectScanAvailable = true)
        {
            Mission = mission;
            IsProjectScanAvailable = isProjectScanAvailable;
            _stagesById = new Lazy<IReadOnlyDictionary<int, MissionStage>>(BuildStagesById);
        }

        /// <summary>The validated mission.</summary>
        public Mission Mission { get; }

        /// <summary>The mission's data block. Null when the mission is null.</summary>
        public MissionData Data => Mission != null ? Mission.missionData : null;

        /// <summary>The mission's stages in array order. Empty when the mission or data is null.</summary>
        public IReadOnlyList<MissionStage> Stages =>
            Data?.missionStages ?? (IReadOnlyList<MissionStage>)Array.Empty<MissionStage>();

        /// <summary>Mission-scoped Exception branches.</summary>
        public IReadOnlyList<MissionBranch> ExceptionBranches =>
            Data?.ExceptionBranches ?? (IReadOnlyList<MissionBranch>)Array.Empty<MissionBranch>();

        /// <summary>Mission-scoped Prerequisite branches.</summary>
        public IReadOnlyList<MissionBranch> PreRequisiteBranches =>
            Data?.PreRequisiteBranches ?? (IReadOnlyList<MissionBranch>)Array.Empty<MissionBranch>();

        /// <summary>
        /// Reverse lookup from <see cref="MissionStage.StageID" /> to the stage. First-occurrence
        /// wins on duplicates. Validators that care about duplicates iterate
        /// <see cref="Stages" /> directly.
        /// </summary>
        public IReadOnlyDictionary<int, MissionStage> StagesById => _stagesById.Value;

        /// <summary>
        /// True when Expensive-cost cross-mission validators may walk other Mission assets in the project.
        /// </summary>
        public bool IsProjectScanAvailable { get; }

        private IReadOnlyDictionary<int, MissionStage> BuildStagesById()
        {
            var dict = new Dictionary<int, MissionStage>();
            var stages = Data?.missionStages;
            if (stages == null) return dict;
            foreach (var stage in stages)
            {
                if (stage == null) continue;
                if (!dict.ContainsKey(stage.StageID)) dict[stage.StageID] = stage;
            }
            return dict;
        }
    }
}
