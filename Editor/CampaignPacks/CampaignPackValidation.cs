using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Severity for campaign pack authoring validation output.
    /// </summary>
    public enum CampaignPackIssueSeverity
    {
        /// <summary>
        /// A non-blocking issue that may still produce incomplete or unexpected campaign content.
        /// </summary>
        Warning,

        /// <summary>
        /// A blocking authoring issue such as a missing or duplicated required identifier.
        /// </summary>
        Error
    }

    /// <summary>
    /// Validation issue produced for campaign pack authoring assets.
    /// </summary>
    public sealed class CampaignPackIssue
    {
        /// <summary>
        /// Severity of the validation issue.
        /// </summary>
        public CampaignPackIssueSeverity Severity;

        /// <summary>
        /// Identifier of the asset or set that produced the issue.
        /// </summary>
        public string SourceId = string.Empty;

        /// <summary>
        /// Human-readable validation message for editor display.
        /// </summary>
        public string Message = string.Empty;
    }

    /// <summary>
    /// Catalog of known identifiers used for campaign pack autocomplete and validation.
    /// </summary>
    public sealed class CampaignPackCatalog
    {
        /// <summary>
        /// Known galaxy definition keys.
        /// </summary>
        public HashSet<string> GalaxyKeys = new(StringComparer.Ordinal);

        /// <summary>
        /// Known tech node identifiers.
        /// </summary>
        public HashSet<string> TechNodeIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known mission identifiers.
        /// </summary>
        public HashSet<string> MissionIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known science experiment identifiers.
        /// </summary>
        public HashSet<string> ExperimentIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known science region identifiers.
        /// </summary>
        public HashSet<string> ScienceRegionIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known discoverable identifiers.
        /// </summary>
        public HashSet<string> DiscoverableIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known campaign pack identifiers.
        /// </summary>
        public HashSet<string> PackIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known tech tree set identifiers.
        /// </summary>
        public HashSet<string> TechTreeSetIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known mission set identifiers.
        /// </summary>
        public HashSet<string> MissionSetIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Known science set identifiers.
        /// </summary>
        public HashSet<string> ScienceSetIds = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Validates campaign pack authoring assets against known content and set identifiers.
    /// </summary>
    public static class CampaignPackValidator
    {
        /// <summary>
        /// Validates all campaign pack authoring assets together so duplicate identifiers can be detected.
        /// </summary>
        /// <param name="packs">Campaign packs to validate.</param>
        /// <param name="techTreeSets">Tech tree sets to validate.</param>
        /// <param name="missionSets">Mission sets to validate.</param>
        /// <param name="scienceSets">Science sets to validate.</param>
        /// <param name="extensions">Campaign pack extensions to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied authoring assets.</returns>
        public static List<CampaignPackIssue> ValidateAll(
            IEnumerable<CampaignPack> packs,
            IEnumerable<TechTreeSet> techTreeSets,
            IEnumerable<MissionSet> missionSets,
            IEnumerable<ScienceSet> scienceSets,
            IEnumerable<CampaignPackExtension> extensions,
            CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            var packList = packs.Where(p => p != null).ToList();
            var techList = techTreeSets.Where(s => s != null).ToList();
            var missionList = missionSets.Where(s => s != null).ToList();
            var scienceList = scienceSets.Where(s => s != null).ToList();
            var extensionList = extensions.Where(e => e != null).ToList();

            AddDuplicateIssues(issues, "Campaign pack", packList.Select(p => p.id));
            AddDuplicateIssues(issues, "Tech tree set", techList.Select(s => s.id));
            AddDuplicateIssues(issues, "Mission set", missionList.Select(s => s.id));
            AddDuplicateIssues(issues, "Science set", scienceList.Select(s => s.id));
            AddDuplicateIssues(issues, "Campaign pack extension", extensionList.Select(e => e.id));

            foreach (var pack in packList) Validate(pack, catalog, issues);
            foreach (var set in techList) Validate(set, catalog, issues);
            foreach (var set in missionList) Validate(set, catalog, issues);
            foreach (var set in scienceList) Validate(set, catalog, issues);
            foreach (var extension in extensionList) Validate(extension, catalog, issues);

            return issues;
        }

        /// <summary>
        /// Validates a single campaign pack.
        /// </summary>
        /// <param name="pack">Campaign pack to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied campaign pack.</returns>
        public static List<CampaignPackIssue> Validate(CampaignPack pack, CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            Validate(pack, catalog, issues);
            return issues;
        }

        /// <summary>
        /// Validates a single tech tree set.
        /// </summary>
        /// <param name="set">Tech tree set to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied tech tree set.</returns>
        public static List<CampaignPackIssue> Validate(TechTreeSet set, CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            Validate(set, catalog, issues);
            return issues;
        }

        /// <summary>
        /// Validates a single mission set.
        /// </summary>
        /// <param name="set">Mission set to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied mission set.</returns>
        public static List<CampaignPackIssue> Validate(MissionSet set, CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            Validate(set, catalog, issues);
            return issues;
        }

        /// <summary>
        /// Validates a single science set.
        /// </summary>
        /// <param name="set">Science set to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied science set.</returns>
        public static List<CampaignPackIssue> Validate(ScienceSet set, CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            Validate(set, catalog, issues);
            return issues;
        }

        /// <summary>
        /// Validates a single campaign pack extension.
        /// </summary>
        /// <param name="extension">Campaign pack extension to validate.</param>
        /// <param name="catalog">Known identifiers used to validate references.</param>
        /// <returns>Validation issues for the supplied extension.</returns>
        public static List<CampaignPackIssue> Validate(CampaignPackExtension extension, CampaignPackCatalog catalog)
        {
            var issues = new List<CampaignPackIssue>();
            Validate(extension, catalog, issues);
            return issues;
        }

        private static void Validate(CampaignPack pack, CampaignPackCatalog catalog, List<CampaignPackIssue> issues)
        {
            var id = pack.id;
            RequireId(issues, "Campaign pack", id);
            if (string.IsNullOrWhiteSpace(pack.galaxyDefinitionKey))
            {
                Add(issues, CampaignPackIssueSeverity.Warning, id, "Campaign pack has no galaxy definition key.");
            }
            else if (!catalog.GalaxyKeys.Contains(pack.galaxyDefinitionKey))
            {
                Add(issues, CampaignPackIssueSeverity.Warning, id, $"Galaxy key '{pack.galaxyDefinitionKey}' was not found.");
            }
        }

        private static void Validate(TechTreeSet set, CampaignPackCatalog catalog, List<CampaignPackIssue> issues)
        {
            RequireId(issues, "Tech tree set", set.id);
            AddDuplicateIssues(issues, $"Tech tree set '{set.id}' tech node", set.techNodeIds);
            AddMissingIssues(issues, set.id, "Tech node", set.techNodeIds, catalog.TechNodeIds);
        }

        private static void Validate(MissionSet set, CampaignPackCatalog catalog, List<CampaignPackIssue> issues)
        {
            RequireId(issues, "Mission set", set.id);
            AddDuplicateIssues(issues, $"Mission set '{set.id}' mission", set.missionIds);
            AddMissingIssues(issues, set.id, "Mission", set.missionIds, catalog.MissionIds);
        }

        private static void Validate(ScienceSet set, CampaignPackCatalog catalog, List<CampaignPackIssue> issues)
        {
            RequireId(issues, "Science set", set.id);
            AddDuplicateIssues(issues, $"Science set '{set.id}' experiment", set.experimentIds);
            AddDuplicateIssues(issues, $"Science set '{set.id}' science region", set.scienceRegionIds);
            AddDuplicateIssues(issues, $"Science set '{set.id}' discoverable", set.discoverableIds);
            AddMissingIssues(issues, set.id, "Science experiment", set.experimentIds, catalog.ExperimentIds);
            AddMissingIssues(issues, set.id, "Science region", set.scienceRegionIds, catalog.ScienceRegionIds);
            AddMissingIssues(issues, set.id, "Discoverable", set.discoverableIds, catalog.DiscoverableIds);
        }

        private static void Validate(CampaignPackExtension extension, CampaignPackCatalog catalog, List<CampaignPackIssue> issues)
        {
            RequireId(issues, "Campaign pack extension", extension.id);
            if (string.IsNullOrWhiteSpace(extension.targetCampaignPackId) &&
                string.IsNullOrWhiteSpace(extension.targetTechTreeSetId) &&
                string.IsNullOrWhiteSpace(extension.targetMissionSetId) &&
                string.IsNullOrWhiteSpace(extension.targetScienceSetId))
            {
                Add(issues, CampaignPackIssueSeverity.Warning, extension.id, "Extension does not target a pack or set.");
            }

            AddMissingTarget(issues, extension.id, "Campaign pack", extension.targetCampaignPackId, catalog.PackIds);
            AddMissingTarget(issues, extension.id, "Tech tree set", extension.targetTechTreeSetId, catalog.TechTreeSetIds);
            AddMissingTarget(issues, extension.id, "Mission set", extension.targetMissionSetId, catalog.MissionSetIds);
            AddMissingTarget(issues, extension.id, "Science set", extension.targetScienceSetId, catalog.ScienceSetIds);

            AddConflictIssues(issues, extension.id, "tech node", extension.addTechNodeIds, extension.removeTechNodeIds);
            AddConflictIssues(issues, extension.id, "mission", extension.addMissionIds, extension.removeMissionIds);
            AddConflictIssues(issues, extension.id, "science experiment", extension.addExperimentIds, extension.removeExperimentIds);
            AddConflictIssues(issues, extension.id, "science region", extension.addScienceRegionIds, extension.removeScienceRegionIds);
            AddConflictIssues(issues, extension.id, "discoverable", extension.addDiscoverableIds, extension.removeDiscoverableIds);

            AddMissingIssues(issues, extension.id, "Tech node", extension.addTechNodeIds.Concat(extension.removeTechNodeIds), catalog.TechNodeIds);
            AddMissingIssues(issues, extension.id, "Mission", extension.addMissionIds.Concat(extension.removeMissionIds), catalog.MissionIds);
            AddMissingIssues(issues, extension.id, "Science experiment", extension.addExperimentIds.Concat(extension.removeExperimentIds), catalog.ExperimentIds);
            AddMissingIssues(issues, extension.id, "Science region", extension.addScienceRegionIds.Concat(extension.removeScienceRegionIds), catalog.ScienceRegionIds);
            AddMissingIssues(issues, extension.id, "Discoverable", extension.addDiscoverableIds.Concat(extension.removeDiscoverableIds), catalog.DiscoverableIds);
        }

        private static void RequireId(List<CampaignPackIssue> issues, string kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Add(issues, CampaignPackIssueSeverity.Error, kind, $"{kind} id is required.");
            }
        }

        private static void AddDuplicateIssues(List<CampaignPackIssue> issues, string kind, IEnumerable<string> ids)
        {
            foreach (var duplicate in ids
                         .Where(id => !string.IsNullOrWhiteSpace(id))
                         .GroupBy(id => id)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                Add(issues, CampaignPackIssueSeverity.Error, kind, $"{kind} id '{duplicate}' is duplicated.");
            }
        }

        private static void AddMissingIssues(
            List<CampaignPackIssue> issues,
            string sourceId,
            string kind,
            IEnumerable<string> ids,
            HashSet<string> known)
        {
            foreach (var id in ids.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                if (!known.Contains(id))
                {
                    Add(issues, CampaignPackIssueSeverity.Warning, sourceId, $"{kind} id '{id}' was not found.");
                }
            }
        }

        private static void AddMissingTarget(
            List<CampaignPackIssue> issues,
            string sourceId,
            string kind,
            string id,
            HashSet<string> known)
        {
            if (!string.IsNullOrWhiteSpace(id) && !known.Contains(id))
            {
                Add(issues, CampaignPackIssueSeverity.Warning, sourceId, $"{kind} target '{id}' was not found.");
            }
        }

        private static void AddConflictIssues(
            List<CampaignPackIssue> issues,
            string sourceId,
            string kind,
            IEnumerable<string> additions,
            IEnumerable<string> removals)
        {
            var addSet = new HashSet<string>((additions).Where(id => !string.IsNullOrWhiteSpace(id)));
            foreach (var id in (removals).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                if (addSet.Contains(id))
                {
                    Add(issues, CampaignPackIssueSeverity.Warning, sourceId, $"Extension both adds and removes {kind} '{id}'. Removal will win.");
                }
            }
        }

        private static void Add(List<CampaignPackIssue> issues, CampaignPackIssueSeverity severity, string sourceId, string message)
        {
            issues.Add(new CampaignPackIssue { Severity = severity, SourceId = sourceId, Message = message });
        }
    }
}
