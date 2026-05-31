using System.IO;
using System.Linq;
using Ksp2UnityTools.Editor.API;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Editor actions for baking campaign pack authoring assets into JSON artifacts.
    /// </summary>
    public static class CampaignPackAuthoringActions
    {
        private const string CampaignPacksGroupName = "Campaign Packs";
        private const string CampaignPacksLabel = "campaign_packs";
        private const string TechTreeSetsLabel = "campaign_pack_tech_tree_sets";
        private const string MissionSetsLabel = "campaign_pack_mission_sets";
        private const string ScienceSetsLabel = "campaign_pack_science_sets";
        private const string ExtensionsLabel = "campaign_pack_extensions";

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
            RegisterAddressable(asset, dto, jsonPath);
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

        private static void RegisterAddressable(Object asset, object dto, string jsonPath)
        {
            var id = GetId(dto);
            var label = GetLabel(dto);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(label))
            {
                Debug.LogWarning($"Baked '{jsonPath}', but it was not registered as addressable because it has no id or label.");
                return;
            }

            var group = ResolveGroup(asset);
            if (group == null)
            {
                Debug.LogWarning(
                    $"Baked '{jsonPath}', but no addressables group resolved. The JSON will not load at runtime until it is registered.");
                return;
            }

            AddressablesTools.MakeAddressable(group, jsonPath, $"{id}.json", label);
            Debug.Log($"Registered campaign pack JSON '{jsonPath}' in addressables group '{group.Name}' with label '{label}'.");
        }

        private static AddressableAssetGroup? ResolveGroup(Object asset)
        {
            if (KSP2UnityTools.FindParentMod(asset) is { allGroup: not null } mod)
            {
                return mod.allGroup;
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                return null;
            }

            var existing = settings.groups.FirstOrDefault(group => group != null && group.Name == CampaignPacksGroupName);
            if (existing != null)
            {
                return existing;
            }

            return settings.CreateGroup(
                CampaignPacksGroupName,
                false,
                false,
                false,
                settings.DefaultGroup.Schemas);
        }

        private static string GetId(object dto)
        {
            return dto switch
            {
                CampaignPackDefinitionDto value => value.Id,
                TechTreeSetDefinitionDto value => value.Id,
                MissionSetDefinitionDto value => value.Id,
                ScienceSetDefinitionDto value => value.Id,
                CampaignPackExtensionDefinitionDto value => value.Id,
                _ => string.Empty
            };
        }

        private static string GetLabel(object dto)
        {
            return dto switch
            {
                CampaignPackDefinitionDto => CampaignPacksLabel,
                TechTreeSetDefinitionDto => TechTreeSetsLabel,
                MissionSetDefinitionDto => MissionSetsLabel,
                ScienceSetDefinitionDto => ScienceSetsLabel,
                CampaignPackExtensionDefinitionDto => ExtensionsLabel,
                _ => string.Empty
            };
        }
    }
}
