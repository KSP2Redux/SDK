using System;
using System.Collections.Generic;
using System.Linq;

namespace Ksp2UnityTools.Editor.CampaignPacks
{
    /// <summary>
    /// Fully resolved campaign pack contents after base sets and matching extensions are applied.
    /// </summary>
    public sealed record EffectiveCampaignPack
    {
        /// <summary>
        /// Identifier of the campaign pack being previewed.
        /// </summary>
        public string CampaignPackId = string.Empty;

        /// <summary>
        /// Galaxy definition key selected by the campaign pack.
        /// </summary>
        public string GalaxyDefinitionKey = string.Empty;

        /// <summary>
        /// Effective tech node identifiers after extension additions and removals.
        /// </summary>
        public List<string> TechNodeIds = new();

        /// <summary>
        /// Effective mission identifiers after extension additions and removals.
        /// </summary>
        public List<string> MissionIds = new();

        /// <summary>
        /// Effective science experiment identifiers after extension additions and removals.
        /// </summary>
        public List<string> ExperimentIds = new();

        /// <summary>
        /// Effective science region identifiers after extension additions and removals.
        /// </summary>
        public List<string> ScienceRegionIds = new();

        /// <summary>
        /// Effective discoverable identifiers after extension additions and removals.
        /// </summary>
        public List<string> DiscoverableIds = new();

        /// <summary>
        /// Identifiers for extensions applied while resolving this preview.
        /// </summary>
        public List<string> AppliedExtensionIds = new();
    }

    /// <summary>
    /// Builds effective campaign pack previews from authoring assets.
    /// </summary>
    public static class CampaignPackResolver
    {
        /// <summary>
        /// Resolves a campaign pack by applying base content sets, then matching extension additions, then extension removals.
        /// </summary>
        /// <param name="pack">Campaign pack to resolve.</param>
        /// <param name="availableExtensions">Extensions available for implicit matching by target identifiers.</param>
        /// <returns>Effective campaign pack contents suitable for editor preview and validation display.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="pack"/> is <see langword="null"/>.</exception>
        public static EffectiveCampaignPack Resolve(
            CampaignPack pack,
            IEnumerable<CampaignPackExtension>? availableExtensions = null)
        {
            if (pack == null) throw new ArgumentNullException(nameof(pack));

            var result = new EffectiveCampaignPack
            {
                CampaignPackId = pack.id,
                GalaxyDefinitionKey = pack.galaxyDefinitionKey,
                TechNodeIds = Copy(pack.techTreeSet?.techNodeIds),
                MissionIds = Copy(pack.missionSet?.missionIds),
                ExperimentIds = Copy(pack.scienceSet?.experimentIds),
                ScienceRegionIds = Copy(pack.scienceSet?.scienceRegionIds),
                DiscoverableIds = Copy(pack.scienceSet?.discoverableIds)
            };

            var matching = GetMatchingExtensions(pack, availableExtensions)
                .OrderBy(e => e.id, StringComparer.Ordinal)
                .ToList();

            foreach (var extension in matching)
            {
                AddRangeUnique(result.TechNodeIds, extension.addTechNodeIds);
                AddRangeUnique(result.MissionIds, extension.addMissionIds);
                AddRangeUnique(result.ExperimentIds, extension.addExperimentIds);
                AddRangeUnique(result.ScienceRegionIds, extension.addScienceRegionIds);
                AddRangeUnique(result.DiscoverableIds, extension.addDiscoverableIds);
                if (!string.IsNullOrWhiteSpace(extension.id))
                {
                    result.AppliedExtensionIds.Add(extension.id);
                }
            }

            foreach (var extension in matching)
            {
                RemoveAll(result.TechNodeIds, extension.removeTechNodeIds);
                RemoveAll(result.MissionIds, extension.removeMissionIds);
                RemoveAll(result.ExperimentIds, extension.removeExperimentIds);
                RemoveAll(result.ScienceRegionIds, extension.removeScienceRegionIds);
                RemoveAll(result.DiscoverableIds, extension.removeDiscoverableIds);
            }

            return result;
        }

        /// <summary>
        /// Enumerates explicit and globally discoverable extensions that target the supplied pack.
        /// </summary>
        /// <param name="pack">Campaign pack whose matching extensions should be found.</param>
        /// <param name="availableExtensions">Globally discoverable extension assets to test against the pack.</param>
        /// <returns>Unique extensions that apply to the supplied pack.</returns>
        public static IEnumerable<CampaignPackExtension> GetMatchingExtensions(
            CampaignPack pack,
            IEnumerable<CampaignPackExtension>? availableExtensions = null)
        {
            if (pack == null) yield break;

            var seen = new HashSet<CampaignPackExtension>();
            foreach (var extension in pack.extensions)
            {
                if (extension != null && seen.Add(extension))
                {
                    yield return extension;
                }
            }

            foreach (var extension in availableExtensions ?? Enumerable.Empty<CampaignPackExtension>())
            {
                if (extension == null || !seen.Add(extension)) continue;
                if (TargetsPack(extension, pack))
                {
                    yield return extension;
                }
            }
        }

        /// <summary>
        /// Checks whether an extension targets a campaign pack or one of its referenced sets.
        /// </summary>
        /// <param name="extension">Extension to test.</param>
        /// <param name="pack">Campaign pack and referenced sets to match against.</param>
        /// <returns><see langword="true"/> when any target identifier matches the pack or one of its sets.</returns>
        public static bool TargetsPack(CampaignPackExtension extension, CampaignPack pack)
        {
            return Matches(extension.targetCampaignPackId, pack.id) ||
                Matches(extension.targetTechTreeSetId, pack.techTreeSet?.id) ||
                Matches(extension.targetMissionSetId, pack.missionSet?.id) ||
                Matches(extension.targetScienceSetId, pack.scienceSet?.id);
        }

        private static bool Matches(string target, string? actual)
        {
            return !string.IsNullOrWhiteSpace(target) &&
                !string.IsNullOrWhiteSpace(actual) &&
                string.Equals(target, actual, StringComparison.Ordinal);
        }

        private static List<string> Copy(IEnumerable<string>? values)
        {
            return values?.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList() ?? new List<string>();
        }

        private static void AddRangeUnique(List<string> target, IEnumerable<string>? values)
        {
            if (values == null) return;
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value) || target.Contains(value)) continue;
                target.Add(value);
            }
        }

        private static void RemoveAll(List<string> target, IEnumerable<string>? values)
        {
            if (values == null) return;
            var removals = new HashSet<string>(values.Where(v => !string.IsNullOrWhiteSpace(v)));
            target.RemoveAll(removals.Contains);
        }
    }
}
