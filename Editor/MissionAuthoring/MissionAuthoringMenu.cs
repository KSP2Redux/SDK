using System;
using System.IO;
using KSP.Game.Missions.Definitions;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.MissionAuthoring
{
    /// <summary>
    /// Menu entries for the mission-authoring workflow.
    /// </summary>
    /// <remarks>
    /// The <c>Convert Mission</c> right-click action bootstraps a sibling Mission asset
    /// from a selected JSON. After conversion, the .asset is the authoring source and
    /// the JSON becomes a build artifact regenerated via the Mission inspector's Bake
    /// to JSON action.
    /// </remarks>
    public static class MissionAuthoringMenu
    {
        // Right-click on a JSON TextAsset to invoke Convert Mission. Loose validation per
        // project convention: any TextAsset whose path ends in .json qualifies for the menu.
        // The parse-as-MissionData check inside ConvertMission is what actually gates conversion.

        [MenuItem("Assets/Redux SDK/Convert Mission", validate = true, priority = KSP2UnityTools.MenuPriority)]
        private static bool ValidateConvertMission()
        {
            return Selection.activeObject is TextAsset textAsset
                && AssetDatabase.GetAssetPath(textAsset)
                    .EndsWith(".json", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem("Assets/Redux SDK/New Mission", priority = KSP2UnityTools.MenuPriority)]
        private static void NewMission()
        {
            var mission = ScriptableObject.CreateInstance<Mission>();
            ProjectWindowUtil.CreateAsset(mission, "NewMission.asset");
        }

        [MenuItem("Assets/Redux SDK/Convert Mission", priority = KSP2UnityTools.MenuPriority)]
        private static void ConvertMission()
        {
            var textAsset = Selection.activeObject as TextAsset;
            if (textAsset == null) return;

            var jsonPath = AssetDatabase.GetAssetPath(textAsset);
            var text = File.ReadAllText(jsonPath);

            MissionData missionData;
            try
            {
                missionData = MissionJsonIo.FromJson(text);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog(
                    "Convert Mission",
                    $"Selected JSON does not parse as MissionData:\n\n{e.Message}",
                    "OK");
                return;
            }

            var assetPath = Path.ChangeExtension(jsonPath, ".asset");
            if (File.Exists(assetPath))
            {
                var ok = EditorUtility.DisplayDialog(
                    "Convert Mission",
                    $"A Mission asset already exists at '{assetPath}'. Overwrite?",
                    "Overwrite",
                    "Cancel");
                if (!ok) return;
                AssetDatabase.DeleteAsset(assetPath);
            }

            var mission = ScriptableObject.CreateInstance<Mission>();
            mission.missionData = missionData;
            AssetDatabase.CreateAsset(mission, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = mission;
        }
    }
}
