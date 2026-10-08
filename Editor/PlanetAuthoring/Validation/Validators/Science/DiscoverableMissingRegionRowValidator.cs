using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags a discoverable whose ScienceRegionId has no matching MapId=-1 region row in the
    /// body's ScienceRegionData definitions.
    /// </summary>
    /// <remarks>
    /// Discoverables pair with a region row at MapId=-1 to expose their situation scalars to the
    /// gameplay science context. Without the row the runtime can resolve the position but has no
    /// scalars to apply, so the discoverable is effectively dead weight.
    /// </remarks>
    public sealed class DiscoverableMissingRegionRowValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DISCOVERABLE_NO_REGION_ROW";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.discoverables == null || data.information?.ScienceRegionDefinitions == null) yield break;

            var missing = new List<string>();
            foreach (CelestialBodyDiscoverablePosition d in data.discoverables)
            {
                string id = d?.ScienceRegionId;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!HasMatchingRegion(data, id)) missing.Add(id);
            }
            foreach (string id in missing)
            {
                ScienceRegionData captured = data;
                string capturedId = id;
                var fixes = new[]
                {
                    new ValidationFix("Add stub region row", () => AddStubRegion(captured, capturedId)),
                };
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Discoverable '{id}' on '{bodyName}' has no matching MapId=-1 region row. " +
                    $"Without one the gameplay science context has no situation scalars for it.",
                    fixes);
            }
        }

        private static bool HasMatchingRegion(ScienceRegionData data, string id)
        {
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs = data.information.ScienceRegionDefinitions;
            for (int i = 0; i < defs.Length; i++)
                if (defs[i] != null && defs[i].MapId < 0 && defs[i].Id == id) return true;
            return false;
        }

        private static void AddStubRegion(ScienceRegionData data, string id)
        {
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs = data.information.ScienceRegionDefinitions ?? new ScienceRegionData.ExtendedScienceRegionDefinition[0];
            var resized = new ScienceRegionData.ExtendedScienceRegionDefinition[defs.Length + 1];
            defs.CopyTo(resized, 0);
            // Match PlaceDiscoverableTool's defaults: MapId=-1, scalars NONE (-1 for atm/spl, 1 land).
            resized[defs.Length] = new ScienceRegionData.ExtendedScienceRegionDefinition
            {
                Id = id,
                MapId = -1,
                AtmosphereScalar = -1f,
                SplashedScalar = -1f,
                LandedScalar = 1f,
                RegionColor = Color.gray,
            };
            Undo.RecordObject(data, "Add discoverable region row");
            data.information.ScienceRegionDefinitions = resized;
            EditorUtility.SetDirty(data);
        }
    }
}
