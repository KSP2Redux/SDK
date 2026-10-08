using System.IO;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Shared editor-side actions for the mission authoring workflow.
    /// </summary>
    /// <remarks>
    /// Bake JSON is invoked from both the <see cref="MissionInspector" /> button and the
    /// <see cref="Windows.MissionEditorWindow" /> header chip.
    /// </remarks>
    public static class MissionAuthoringActions
    {
        /// <summary>
        /// Writes the mission's runtime JSON to a sibling file next to the asset, force-imports
        /// it, and registers it as an addressable under the resolved group with key
        /// <c>{missionData.ID}.json</c>.
        /// </summary>
        /// <remarks>
        /// Prompts for overwrite confirmation if a JSON file already exists at the target path.
        /// </remarks>
        /// <param name="mission">The mission asset to bake.</param>
        public static void BakeToJson(Mission mission)
        {
            if (mission == null) return;
            var assetPath = AssetDatabase.GetAssetPath(mission);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog(
                    "Bake to JSON",
                    "Mission has no asset path. Save the asset first.",
                    "OK");
                return;
            }

            var jsonPath = Path.ChangeExtension(assetPath, ".json");

            if (File.Exists(jsonPath))
            {
                var ok = EditorUtility.DisplayDialog(
                    "Bake to JSON",
                    $"Overwrite the existing JSON at '{jsonPath}'? Any hand-edits will be lost.",
                    "Overwrite",
                    "Cancel");
                if (!ok) return;
            }

            var json = MissionJsonIo.ToJson(mission.missionData);
            File.WriteAllText(jsonPath, json);
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);

            var group = MissionAuthoringAddressables.ResolveGroup(mission);
            if (group != null)
            {
                AddressablesTools.MakeAddressable(
                    group,
                    jsonPath,
                    $"{mission.missionData.ID}.json",
                    MissionAuthoringAddressables.MissionsLabel);
                Debug.Log($"Baked '{jsonPath}' into addressables group '{group.Name}'.");
            }
            else
            {
                Debug.LogWarning(
                    $"Baked '{jsonPath}', but no addressables group resolved (neither a parent " +
                    $"mod with missionsGroup nor the project-level " +
                    $"'{MissionAuthoringAddressables.MissionsGroupName}' group exists). The JSON " +
                    $"will not load at runtime until it is registered.");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
