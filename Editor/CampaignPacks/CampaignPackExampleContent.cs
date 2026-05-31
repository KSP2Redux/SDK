using System.IO;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Creates a small campaign pack authoring example that can be inspected, edited, and baked locally.
    /// </summary>
    public static class CampaignPackExampleContent
    {
        private const string ExampleFolderName = "CampaignPackExample";

        /// <summary>
        /// Creates an example campaign pack asset graph in the selected project folder.
        /// </summary>
        [MenuItem("Assets/Redux SDK/Create Campaign Pack Example", priority = KSP2UnityTools.MenuPriority + 1)]
        public static void CreateExample()
        {
            var folder = CreateUniqueFolder(GetSelectedFolder(), ExampleFolderName);

            var techTreeSet = CreateAsset<TechTreeSet>(folder, "ExampleTechTreeSet");
            techTreeSet.id = "example.tech-tree.basic";
            techTreeSet.techNodeIds.Add("tNode_0v_advanced_telemetry");

            var missionSet = CreateAsset<MissionSet>(folder, "ExampleMissionSet");
            missionSet.id = "example.missions.kerbin";
            missionSet.missionIds.Add("KSP2Mission_Secondary_Kerbin_EVAGround");

            var scienceSet = CreateAsset<ScienceSet>(folder, "ExampleScienceSet");
            scienceSet.id = "example.science.basic";
            scienceSet.experimentIds.Add("ResourceScan");

            var extension = CreateAsset<CampaignPackExtension>(folder, "ExampleCampaignPackExtension");
            extension.id = "example.extension.extra-starting-node";
            extension.targetCampaignPackId = "example.campaign-pack";
            extension.addTechNodeIds.Add("tNode_1.5v_rockets_01");
            extension.addMissionIds.Add("KSP2Mission_Secondary_Kerbin_KerbinTourSOI");

            var pack = CreateAsset<CampaignPack>(folder, "ExampleCampaignPack");
            pack.id = "example.campaign-pack";
            pack.nameLocKey = "CampaignPacks/Example/Name";
            pack.descriptionLocKey = "CampaignPacks/Example/Description";
            pack.galaxyDefinitionKey = "GalaxyDefinition_Default";
            pack.techTreeSet = techTreeSet;
            pack.missionSet = missionSet;
            pack.scienceSet = scienceSet;
            pack.extensions.Add(extension);

            EditorUtility.SetDirty(techTreeSet);
            EditorUtility.SetDirty(missionSet);
            EditorUtility.SetDirty(scienceSet);
            EditorUtility.SetDirty(extension);
            EditorUtility.SetDirty(pack);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = pack;
            EditorGUIUtility.PingObject(pack);
            CampaignPackBrowserWindow.Open();
        }

        private static T CreateAsset<T>(string folder, string fileName) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{fileName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static string GetSelectedFolder()
        {
            var selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return "Assets";
            }

            if (File.Exists(selectedPath))
            {
                selectedPath = Path.GetDirectoryName(selectedPath)?.Replace('\\', '/') ?? "Assets";
            }

            return AssetDatabase.IsValidFolder(selectedPath) ? selectedPath : "Assets";
        }

        private static string CreateUniqueFolder(string parentFolder, string folderName)
        {
            var target = $"{parentFolder}/{folderName}";
            if (!AssetDatabase.IsValidFolder(target))
            {
                AssetDatabase.CreateFolder(parentFolder, folderName);
                return target;
            }

            for (var i = 1; i < 1000; i++)
            {
                var candidateName = $"{folderName}_{i}";
                var candidatePath = $"{parentFolder}/{candidateName}";
                if (AssetDatabase.IsValidFolder(candidatePath))
                {
                    continue;
                }

                AssetDatabase.CreateFolder(parentFolder, candidateName);
                return candidatePath;
            }

            throw new IOException($"Could not create a unique folder under '{parentFolder}'.");
        }
    }
}
