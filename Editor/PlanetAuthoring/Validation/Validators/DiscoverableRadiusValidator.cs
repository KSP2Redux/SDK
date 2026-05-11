using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags discoverables with a non-positive Radius.
    /// </summary>
    /// <remarks>
    /// Runtime science-context resolution uses Radius to size the discoverable's trigger volume.
    /// Zero or negative collapses the volume and the player can never be "inside" the discoverable.
    /// </remarks>
    public sealed class DiscoverableRadiusValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DISCOVERABLE_RADIUS_INVALID";

        // Default radius applied by the fix. Matches PlaceDiscoverableTool's stock default so
        // unfixed discoverables don't end up with a wildly different scale than freshly placed ones.
        private const double DefaultRadius = 100.0;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.discoverables == null) yield break;

            for (int i = 0; i < data.discoverables.Count; i++)
            {
                CelestialBodyDiscoverablePosition d = data.discoverables[i];
                if (d == null || d.Radius > 0.0) continue;
                ScienceRegionData captured = data;
                int captureIndex = i;
                var fixes = new[]
                {
                    new ValidationFix($"Set to {DefaultRadius:0} m", () => SetRadius(captured, captureIndex, DefaultRadius)),
                };
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Discoverable '{(string.IsNullOrEmpty(d.ScienceRegionId) ? $"#{i}" : d.ScienceRegionId)}' on " +
                    $"'{bodyName}' has Radius = {d.Radius}. The runtime trigger volume collapses to a point.",
                    fixes);
            }
        }

        private static void SetRadius(ScienceRegionData data, int index, double radius)
        {
            if (data?.discoverables == null || index < 0 || index >= data.discoverables.Count) return;
            Undo.RecordObject(data, "Set discoverable radius");
            data.discoverables[index].Radius = radius;
            EditorUtility.SetDirty(data);
        }
    }
}
