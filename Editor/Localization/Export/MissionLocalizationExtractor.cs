using System.Collections.Generic;
using Ksp2UnityTools.Editor.MissionAuthoring;
using UnityEditor;

namespace Ksp2UnityTools.Editor.Localization.Export
{
    /// <summary>
    /// Emits localization keys for a mission asset: top-level name/description plus per-stage
    /// name/description/Objective.
    /// </summary>
    public static class MissionLocalizationExtractor
    {
        public static List<LocalizationKeyEntry> Extract(Mission mission)
        {
            var entries = new List<LocalizationKeyEntry>();
            if (mission == null || mission.missionData == null) return entries;
            var data = mission.missionData;
            var missionId = string.IsNullOrEmpty(data.ID) ? mission.name : data.ID;
            var assetPath = AssetDatabase.GetAssetPath(mission);
            var sourceHint = $"Mission: {missionId} ({assetPath})";

            if (!string.IsNullOrEmpty(data.name))
            {
                entries.Add(new LocalizationKeyEntry(
                    data.name, missionId,
                    $"Mission title for {missionId}", sourceHint));
            }
            if (!string.IsNullOrEmpty(data.description))
            {
                entries.Add(new LocalizationKeyEntry(
                    data.description, string.Empty,
                    $"Mission synopsis for {missionId}", sourceHint));
            }

            if (data.missionStages != null)
            {
                for (int i = 0; i < data.missionStages.Count; i++)
                {
                    var stage = data.missionStages[i];
                    if (stage == null) continue;
                    if (!string.IsNullOrEmpty(stage.name))
                    {
                        entries.Add(new LocalizationKeyEntry(
                            stage.name, string.Empty,
                            $"Stage name (stage {i}) for mission {missionId}", sourceHint));
                    }
                    if (!string.IsNullOrEmpty(stage.description))
                    {
                        entries.Add(new LocalizationKeyEntry(
                            stage.description, string.Empty,
                            $"Stage description (stage {i}) for mission {missionId}", sourceHint));
                    }
                    if (!string.IsNullOrEmpty(stage.Objective))
                    {
                        entries.Add(new LocalizationKeyEntry(
                            stage.Objective, string.Empty,
                            $"Stage objective shown to player (stage {i}) for mission {missionId}", sourceHint));
                    }
                }
            }
            return entries;
        }
    }
}
