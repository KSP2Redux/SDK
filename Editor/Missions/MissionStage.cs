using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using KSP.IO;
using Ksp2UnityTools.Editor.Missions.ConditionTree;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Missions
{
    [Serializable]
    public class MissionStage
    {
        public int stageId;
        public string name;
        public string description;

        [Tooltip(
            "The I2 Localization key for the name of the mission. Add this key to <mod_folder>/Copied/localizations/<localization_file>.csv"
        )]
        public string objectiveLocalizationKey;

        public bool displayObjective;
        public bool revealObjectiveOnActivate;
        public MissionCondition0 condition;
        public MissionRewardDefinition[] missionRewards;
        public MissionAction[] actions;

        private static Regex _firstBracketRegex = new(Regex.Escape("{"), RegexOptions.Compiled);

        public KSP.Game.Missions.Definitions.MissionStage ToMissionStage()
        {
            var cond = condition.ToCondition();
            return new KSP.Game.Missions.Definitions.MissionStage
            {
                StageID = stageId,
                name = name,
                description = description,
                Objective = objectiveLocalizationKey,
                DisplayObjective = displayObjective,
                RevealObjectiveOnActivate = revealObjectiveOnActivate,
                condition = condition.ToCondition(),
                MissionReward = new MissionReward
                {
                    MissionRewardDefinitions = missionRewards.ToList()
                },
                IgnoreExceptionBranches = false,
                actions = actions.Select(x => x.ToMissionAction()).ToList(),
                branches = new List<MissionBranch>(),
                scriptableCondition = cond == null
                    ? null
                    : JObject.Parse(
                        _firstBracketRegex.Replace(
                            IOProvider.ToJson(cond),
                            $"{{\n    \"$type\": \"{cond.GetType().AssemblyQualifiedName}\",",
                            1
                        )
                    )
            };
        }
    }
}