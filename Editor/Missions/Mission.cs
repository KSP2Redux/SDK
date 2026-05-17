using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Missions.Definitions;
using KSP.Game.Missions;
using KSP.Game.Missions.Definitions;
using KSP.Game.Missions.State;
using Ksp2UnityTools.Editor.Modding;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.Missions
{
    public class Mission : TextAssetGenerator
    {
        [MenuItem("Assets/Redux SDK/Mission", priority = KSP2UnityTools.MenuPriority)]
        public static void CreateMission()
        {
            KSP2UnityTools.CreateKsp2UnityToolsAssetAtSelectedPath<Mission>("New Mission");
        }

        [Tooltip("The ID of the mission, this will be the key used in addressables.")]
        public string missionId;

        [Tooltip(
            "The I2 Localization key for the name of the mission. Add this key to <mod_folder>/Copied/localizations/<localization_file>.csv"
        )]
        public string nameLocalizationKey;

        [Tooltip(
            "The I2 Localization key for the description of the mission. Add this key to <mod_folder>/Copied/localizations/<localization_file>.csv"
        )]
        public string descriptionLocalizationKey;

        public MissionType missionType;
        public MissionOwner owner;
        public MissionState state = MissionState.Inactive;
        public bool hidden;

        [Tooltip("The granter for this mission")]
        public string granter;

        [Tooltip("The addressables key for the triumph loop video")]
        public string triumphLoopVideo;

        public bool rewardsVisible;
        public UIDisplayType uiDisplayType;
        public MissionStage[] stages;
        public MissionContentBranch[] branches;


        [HideInInspector] public bool usePatches;

        public override bool ShouldGenerate => usePatches;
        public override string PathInMod => $"patches/missions/{missionId}.patch";

        public override string Generate()
        {
            string json = KSP2UnityTools.ToJson(GenerateMissionData());
            json = $"@new(\"{missionId}\")\n:missions {{\n@set\n" + json + ";\n}";
            return json;
        }

        public MissionData GenerateMissionData()
        {
            var missionData = new MissionData
            {
                ID = missionId,
                name = nameLocalizationKey,
                description = descriptionLocalizationKey,
                type = missionType,
                Owner = owner,
                state = state,
                Hidden = hidden,
                MissionGranterKey = granter,
                TriumphLoopVideoKey = triumphLoopVideo,
                uiDisplayType = uiDisplayType,
                GameModeFeatureId = "ExplorationMissions",
                currentStageIndex = stages.Min(x => x.stageId),
                ExceptionBranches = new List<MissionBranch>(),
                missionStages = stages.Select(x => x.ToMissionStage()).ToList(),
                ContentBranches = branches.Select(x => x.ToMissionContentBranch()).ToList(),
                maxStageID = stages.Max(x => x.stageId),
                VisibleRewards = true,
                MissionSaveAssetKey = ""
            };
            return missionData;
        }
    }
}