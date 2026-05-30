using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// JSON-facing representation of a campaign pack authoring asset.
    /// </summary>
    public sealed record CampaignPackDefinitionDto
    {
        /// <summary>
        /// Stable campaign pack identifier.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// Localization key for the campaign pack display name.
        /// </summary>
        public string NameLocKey = string.Empty;

        /// <summary>
        /// Localization key for the campaign pack description.
        /// </summary>
        public string DescriptionLocKey = string.Empty;

        /// <summary>
        /// Galaxy definition key selected by the campaign pack.
        /// </summary>
        public string GalaxyDefinitionKey = string.Empty;

        /// <summary>
        /// Optional identifier of the tech tree set selected by the campaign pack.
        /// </summary>
        public string? TechTreeSetId;

        /// <summary>
        /// Optional identifier of the mission set selected by the campaign pack.
        /// </summary>
        public string? MissionSetId;

        /// <summary>
        /// Optional identifier of the science set selected by the campaign pack.
        /// </summary>
        public string? ScienceSetId;
    }

    /// <summary>
    /// JSON-facing representation of a tech tree set.
    /// </summary>
    public sealed record TechTreeSetDefinitionDto
    {
        /// <summary>
        /// Stable tech tree set identifier.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// Tech node identifiers included by the set.
        /// </summary>
        public List<string> TechNodeIds = new();
    }

    /// <summary>
    /// JSON-facing representation of a mission set.
    /// </summary>
    public sealed record MissionSetDefinitionDto
    {
        /// <summary>
        /// Stable mission set identifier.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// Mission identifiers included by the set.
        /// </summary>
        public List<string> MissionIds = new();
    }

    /// <summary>
    /// JSON-facing representation of a science set.
    /// </summary>
    public sealed record ScienceSetDefinitionDto
    {
        /// <summary>
        /// Stable science set identifier.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// Science experiment identifiers included by the set.
        /// </summary>
        public List<string> ExperimentIds = new();

        /// <summary>
        /// Science region identifiers included by the set.
        /// </summary>
        public List<string> ScienceRegionIds = new();

        /// <summary>
        /// Discoverable identifiers included by the set.
        /// </summary>
        public List<string> DiscoverableIds = new();
    }

    /// <summary>
    /// JSON-facing representation of campaign pack extension add/remove operations.
    /// </summary>
    public sealed record CampaignPackExtensionDefinitionDto
    {
        /// <summary>
        /// Stable extension identifier.
        /// </summary>
        public string Id = string.Empty;

        /// <summary>
        /// Optional campaign pack identifier targeted by this extension.
        /// </summary>
        public string TargetCampaignPackId = string.Empty;

        /// <summary>
        /// Optional tech tree set identifier targeted by this extension.
        /// </summary>
        public string TargetTechTreeSetId = string.Empty;

        /// <summary>
        /// Optional mission set identifier targeted by this extension.
        /// </summary>
        public string TargetMissionSetId = string.Empty;

        /// <summary>
        /// Optional science set identifier targeted by this extension.
        /// </summary>
        public string TargetScienceSetId = string.Empty;

        /// <summary>
        /// Tech node identifiers added by this extension.
        /// </summary>
        public List<string> AddTechNodeIds = new();

        /// <summary>
        /// Tech node identifiers removed by this extension.
        /// </summary>
        public List<string> RemoveTechNodeIds = new();

        /// <summary>
        /// Mission identifiers added by this extension.
        /// </summary>
        public List<string> AddMissionIds = new();

        /// <summary>
        /// Mission identifiers removed by this extension.
        /// </summary>
        public List<string> RemoveMissionIds = new();

        /// <summary>
        /// Science experiment identifiers added by this extension.
        /// </summary>
        public List<string> AddExperimentIds = new();

        /// <summary>
        /// Science experiment identifiers removed by this extension.
        /// </summary>
        public List<string> RemoveExperimentIds = new();

        /// <summary>
        /// Science region identifiers added by this extension.
        /// </summary>
        public List<string> AddScienceRegionIds = new();

        /// <summary>
        /// Science region identifiers removed by this extension.
        /// </summary>
        public List<string> RemoveScienceRegionIds = new();

        /// <summary>
        /// Discoverable identifiers added by this extension.
        /// </summary>
        public List<string> AddDiscoverableIds = new();

        /// <summary>
        /// Discoverable identifiers removed by this extension.
        /// </summary>
        public List<string> RemoveDiscoverableIds = new();
    }

    /// <summary>
    /// Converts campaign pack authoring assets into stable JSON DTOs.
    /// </summary>
    public static class CampaignPackDtoMapper
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Serializes a campaign pack DTO using the authoring JSON format.
        /// </summary>
        /// <param name="dto">DTO instance to serialize.</param>
        /// <returns>Indented JSON for the supplied DTO.</returns>
        public static string ToJson(object dto)
        {
            return JsonConvert.SerializeObject(dto, JsonSettings);
        }

        /// <summary>
        /// Converts a campaign pack authoring asset to its JSON-facing DTO.
        /// </summary>
        /// <param name="pack">Campaign pack authoring asset to convert.</param>
        /// <returns>DTO with stable field names for bake output.</returns>
        public static CampaignPackDefinitionDto ToDto(CampaignPack pack)
        {
            return new CampaignPackDefinitionDto
            {
                Id = pack.id,
                NameLocKey = pack.nameLocKey,
                DescriptionLocKey = pack.descriptionLocKey,
                GalaxyDefinitionKey = pack.galaxyDefinitionKey,
                TechTreeSetId = pack.techTreeSet != null ? pack.techTreeSet.id : null,
                MissionSetId = pack.missionSet != null ? pack.missionSet.id : null,
                ScienceSetId = pack.scienceSet != null ? pack.scienceSet.id : null
            };
        }

        /// <summary>
        /// Converts a tech tree set authoring asset to its JSON-facing DTO.
        /// </summary>
        /// <param name="set">Tech tree set authoring asset to convert.</param>
        /// <returns>DTO with the set identifier and tech node IDs.</returns>
        public static TechTreeSetDefinitionDto ToDto(TechTreeSet set)
        {
            return new TechTreeSetDefinitionDto
            {
                Id = set.id,
                TechNodeIds = Copy(set.techNodeIds)
            };
        }

        /// <summary>
        /// Converts a mission set authoring asset to its JSON-facing DTO.
        /// </summary>
        /// <param name="set">Mission set authoring asset to convert.</param>
        /// <returns>DTO with the set identifier and mission IDs.</returns>
        public static MissionSetDefinitionDto ToDto(MissionSet set)
        {
            return new MissionSetDefinitionDto
            {
                Id = set.id,
                MissionIds = Copy(set.missionIds)
            };
        }

        /// <summary>
        /// Converts a science set authoring asset to its JSON-facing DTO.
        /// </summary>
        /// <param name="set">Science set authoring asset to convert.</param>
        /// <returns>DTO with experiment, region, and discoverable IDs.</returns>
        public static ScienceSetDefinitionDto ToDto(ScienceSet set)
        {
            return new ScienceSetDefinitionDto
            {
                Id = set.id,
                ExperimentIds = Copy(set.experimentIds),
                ScienceRegionIds = Copy(set.scienceRegionIds),
                DiscoverableIds = Copy(set.discoverableIds)
            };
        }

        /// <summary>
        /// Converts a campaign pack extension authoring asset to its JSON-facing DTO.
        /// </summary>
        /// <param name="extension">Extension authoring asset to convert.</param>
        /// <returns>DTO with extension targets and add/remove lists.</returns>
        public static CampaignPackExtensionDefinitionDto ToDto(CampaignPackExtension extension)
        {
            return new CampaignPackExtensionDefinitionDto
            {
                Id = extension.id,
                TargetCampaignPackId = extension.targetCampaignPackId,
                TargetTechTreeSetId = extension.targetTechTreeSetId,
                TargetMissionSetId = extension.targetMissionSetId,
                TargetScienceSetId = extension.targetScienceSetId,
                AddTechNodeIds = Copy(extension.addTechNodeIds),
                RemoveTechNodeIds = Copy(extension.removeTechNodeIds),
                AddMissionIds = Copy(extension.addMissionIds),
                RemoveMissionIds = Copy(extension.removeMissionIds),
                AddExperimentIds = Copy(extension.addExperimentIds),
                RemoveExperimentIds = Copy(extension.removeExperimentIds),
                AddScienceRegionIds = Copy(extension.addScienceRegionIds),
                RemoveScienceRegionIds = Copy(extension.removeScienceRegionIds),
                AddDiscoverableIds = Copy(extension.addDiscoverableIds),
                RemoveDiscoverableIds = Copy(extension.removeDiscoverableIds)
            };
        }

        private static List<string> Copy(List<string>? values)
        {
            return values == null ? new List<string>() : new List<string>(values);
        }
    }
}
