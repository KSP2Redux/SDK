using System;
using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Landmark
{
    /// <summary>
    /// Flags discoverable entries and MapId=-1 region rows in a body's ScienceRegionData that no
    /// <see cref="SurfaceLandmark" /> on the body references.
    /// </summary>
    /// <remarks>
    /// Landmark sync is additive. Renaming a landmark's region Id or deleting a landmark leaves the
    /// old entry and region row behind. Standalone discoverables placed via the Place Discoverable
    /// tool are also unmatched by design, so unmatched entries are surfaced as review warnings
    /// rather than errors. Region rows at MapId=-1 that neither a landmark nor a discoverable entry
    /// references are pure leftovers and carry a remove fix.
    /// </remarks>
    public sealed class LandmarkOrphanedDiscoverableValidator : IPlanetValidator
    {
        /// <summary>Stable code for discoverable entries no landmark manages.</summary>
        public const string EntryCode = "LANDMARK_ORPHANED_DISCOVERABLE";

        /// <summary>Stable code for MapId=-1 region rows nothing references.</summary>
        public const string RowCode = "LANDMARK_ORPHANED_REGION_ROW";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            var bodyName = body.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            var data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data == null) yield break;

            var landmarkRegionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var landmark in pqs.GetComponentsInChildren<SurfaceLandmark>(true))
            {
                if (landmark != null && !string.IsNullOrEmpty(landmark.DiscoverableRegionId))
                {
                    landmarkRegionIds.Add(landmark.DiscoverableRegionId);
                }
            }

            var entryRegionIds = new HashSet<string>(StringComparer.Ordinal);
            if (data.discoverables != null)
            {
                foreach (var entry in data.discoverables)
                {
                    var id = entry?.ScienceRegionId;
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    entryRegionIds.Add(id);
                    if (landmarkRegionIds.Contains(id)) continue;
                    var captured = entry;
                    yield return new ValidationIssue(
                        EntryCode,
                        ValidationSeverity.Warning,
                        $"Discoverable '{id}' on '{bodyName}' is not managed by any surface landmark. " +
                        $"Standalone discoverables are valid when placed deliberately. If this is a leftover from a landmark rename or delete, remove it.",
                        new[] { new ValidationFix("Remove discoverable entry", () => RemoveEntry(data, captured)) });
                }
            }

            var defs = data.information?.ScienceRegionDefinitions;
            if (defs == null) yield break;
            foreach (var def in defs)
            {
                var id = def?.Id;
                if (def == null || def.MapId >= 0 || string.IsNullOrWhiteSpace(id)) continue;
                if (landmarkRegionIds.Contains(id) || entryRegionIds.Contains(id)) continue;
                var capturedId = id;
                yield return new ValidationIssue(
                    RowCode,
                    ValidationSeverity.Warning,
                    $"Region row '{id}' on '{bodyName}' has MapId=-1 but no landmark or discoverable entry references it. " +
                    $"It is a leftover with no runtime effect.",
                    new[] { new ValidationFix("Remove region row", () => RemoveRow(data, capturedId)) });
            }
        }

        private static void RemoveEntry(ScienceRegionData data, CelestialBodyDiscoverablePosition entry)
        {
            if (data?.discoverables == null || entry == null) return;
            Undo.RecordObject(data, "Remove orphaned discoverable entry");
            data.discoverables.Remove(entry);
            EditorUtility.SetDirty(data);
        }

        private static void RemoveRow(ScienceRegionData data, string id)
        {
            var defs = data?.information?.ScienceRegionDefinitions;
            if (defs == null) return;
            var kept = new List<ScienceRegionData.ExtendedScienceRegionDefinition>(defs.Length);
            foreach (var def in defs)
            {
                if (def != null && def.MapId < 0 && string.Equals(def.Id, id, StringComparison.Ordinal)) continue;
                kept.Add(def);
            }
            if (kept.Count == defs.Length) return;
            Undo.RecordObject(data, "Remove orphaned region row");
            data.information.ScienceRegionDefinitions = kept.ToArray();
            EditorUtility.SetDirty(data);
        }
    }
}
