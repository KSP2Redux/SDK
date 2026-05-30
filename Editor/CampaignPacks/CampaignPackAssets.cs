using System.Collections.Generic;
using UnityEngine;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Player-facing content profile that selects a galaxy and optional progression-content sets.
    /// </summary>
    public class CampaignPack : ScriptableObject
    {
        /// <summary>
        /// Stable campaign pack identifier used by other authoring assets and baked JSON.
        /// </summary>
        public string id = string.Empty;

        /// <summary>
        /// Localization key for the campaign pack display name.
        /// </summary>
        public string nameLocKey = string.Empty;

        /// <summary>
        /// Localization key for the campaign pack description.
        /// </summary>
        public string descriptionLocKey = string.Empty;

        /// <summary>
        /// Addressable or data key for the galaxy definition selected by this campaign pack.
        /// </summary>
        public string galaxyDefinitionKey = string.Empty;

        /// <summary>
        /// Optional set of tech tree nodes available in this campaign pack.
        /// </summary>
        public TechTreeSet? techTreeSet;

        /// <summary>
        /// Optional set of missions available in this campaign pack.
        /// </summary>
        public MissionSet? missionSet;

        /// <summary>
        /// Optional set of science content available in this campaign pack.
        /// </summary>
        public ScienceSet? scienceSet;

        /// <summary>
        /// Explicit extension assets applied to this pack in addition to globally discoverable extensions.
        /// </summary>
        public List<CampaignPackExtension> extensions = new();
    }

    /// <summary>
    /// Authoring source for the tech tree nodes active in a campaign pack.
    /// </summary>
    public class TechTreeSet : ScriptableObject
    {
        /// <summary>
        /// Stable tech tree set identifier referenced by campaign packs and extensions.
        /// </summary>
        public string id = string.Empty;

        /// <summary>
        /// Tech node identifiers included by this set.
        /// </summary>
        public List<string> techNodeIds = new();
    }

    /// <summary>
    /// Authoring source for the missions active in a campaign pack.
    /// </summary>
    public class MissionSet : ScriptableObject
    {
        /// <summary>
        /// Stable mission set identifier referenced by campaign packs and extensions.
        /// </summary>
        public string id = string.Empty;

        /// <summary>
        /// Mission identifiers included by this set.
        /// </summary>
        public List<string> missionIds = new();
    }

    /// <summary>
    /// Authoring source for science content active in a campaign pack.
    /// </summary>
    public class ScienceSet : ScriptableObject
    {
        /// <summary>
        /// Stable science set identifier referenced by campaign packs and extensions.
        /// </summary>
        public string id = string.Empty;

        /// <summary>
        /// Science experiment identifiers included by this set.
        /// </summary>
        public List<string> experimentIds = new();

        /// <summary>
        /// Science region identifiers included by this set.
        /// </summary>
        public List<string> scienceRegionIds = new();

        /// <summary>
        /// Discoverable identifiers included by this set.
        /// </summary>
        public List<string> discoverableIds = new();
    }

    /// <summary>
    /// Additive/removal layer that lets one mod extend another campaign pack or content set.
    /// </summary>
    public class CampaignPackExtension : ScriptableObject
    {
        /// <summary>
        /// Stable extension identifier used for validation, ordering, and bake output.
        /// </summary>
        public string id = string.Empty;

        /// <summary>
        /// Optional campaign pack identifier targeted by this extension.
        /// </summary>
        public string targetCampaignPackId = string.Empty;

        /// <summary>
        /// Optional tech tree set identifier targeted by this extension.
        /// </summary>
        public string targetTechTreeSetId = string.Empty;

        /// <summary>
        /// Optional mission set identifier targeted by this extension.
        /// </summary>
        public string targetMissionSetId = string.Empty;

        /// <summary>
        /// Optional science set identifier targeted by this extension.
        /// </summary>
        public string targetScienceSetId = string.Empty;

        /// <summary>
        /// Tech node identifiers added when this extension applies.
        /// </summary>
        public List<string> addTechNodeIds = new();

        /// <summary>
        /// Tech node identifiers removed when this extension applies.
        /// </summary>
        public List<string> removeTechNodeIds = new();

        /// <summary>
        /// Mission identifiers added when this extension applies.
        /// </summary>
        public List<string> addMissionIds = new();

        /// <summary>
        /// Mission identifiers removed when this extension applies.
        /// </summary>
        public List<string> removeMissionIds = new();

        /// <summary>
        /// Science experiment identifiers added when this extension applies.
        /// </summary>
        public List<string> addExperimentIds = new();

        /// <summary>
        /// Science experiment identifiers removed when this extension applies.
        /// </summary>
        public List<string> removeExperimentIds = new();

        /// <summary>
        /// Science region identifiers added when this extension applies.
        /// </summary>
        public List<string> addScienceRegionIds = new();

        /// <summary>
        /// Science region identifiers removed when this extension applies.
        /// </summary>
        public List<string> removeScienceRegionIds = new();

        /// <summary>
        /// Discoverable identifiers added when this extension applies.
        /// </summary>
        public List<string> addDiscoverableIds = new();

        /// <summary>
        /// Discoverable identifiers removed when this extension applies.
        /// </summary>
        public List<string> removeDiscoverableIds = new();
    }
}
