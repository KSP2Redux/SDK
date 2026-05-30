using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Editor actions for baking campaign pack authoring assets into JSON artifacts.
    /// </summary>
    public static class CampaignPackAuthoringActions
    {
        /// <summary>
        /// Bakes a supported campaign pack authoring asset into a sibling JSON file.
        /// </summary>
        /// <param name="asset">Authoring asset to bake.</param>
        public static void BakeToJson(Object asset)
        {
            if (asset == null) return;

            object? dto = asset switch
            {
                CampaignPack pack => CampaignPackDtoMapper.ToDto(pack),
                TechTreeSet set => CampaignPackDtoMapper.ToDto(set),
                MissionSet set => CampaignPackDtoMapper.ToDto(set),
                ScienceSet set => CampaignPackDtoMapper.ToDto(set),
                CampaignPackExtension extension => CampaignPackDtoMapper.ToDto(extension),
                _ => null
            };

            if (dto == null) return;

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("Bake Campaign Pack JSON", "Save the asset before baking JSON.", "OK");
                return;
            }

            var jsonPath = Path.ChangeExtension(assetPath, ".json");
            if (File.Exists(jsonPath))
            {
                var ok = EditorUtility.DisplayDialog(
                    "Bake Campaign Pack JSON",
                    $"Overwrite the existing JSON at '{jsonPath}'?",
                    "Overwrite",
                    "Cancel");
                if (!ok) return;
            }

            File.WriteAllText(jsonPath, CampaignPackDtoMapper.ToJson(dto));
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked campaign pack authoring JSON: {jsonPath}");
        }

        /// <summary>
        /// Bakes a campaign pack, its referenced sets, and any matching extensions.
        /// </summary>
        /// <param name="pack">Campaign pack whose authoring graph should be baked.</param>
        public static void BakeAllForPack(CampaignPack pack)
        {
            if (pack == null) return;
            BakeToJson(pack);
            if (pack.techTreeSet != null) BakeToJson(pack.techTreeSet);
            if (pack.missionSet != null) BakeToJson(pack.missionSet);
            if (pack.scienceSet != null) BakeToJson(pack.scienceSet);
            foreach (var extension in CampaignPackResolver.GetMatchingExtensions(pack, CampaignPackEditorDatabase.FindExtensions()))
            {
                BakeToJson(extension);
            }
        }
    }
}
