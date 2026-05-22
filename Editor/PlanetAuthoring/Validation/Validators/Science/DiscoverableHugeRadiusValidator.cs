using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags discoverables whose Radius is larger than the body itself.
    /// </summary>
    /// <remarks>
    /// A discoverable's trigger volume should sit on the surface, so any radius larger than the
    /// body radius is almost certainly a typo (e.g. an extra zero). The threshold is deliberately
    /// loose because there is no runtime-grounded upper bound. The check catches obviously broken
    /// values, not artistic choices.
    /// </remarks>
    public sealed class DiscoverableHugeRadiusValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DISCOVERABLE_HUGE_RADIUS";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            double bodyRadius = body?.Data?.radius ?? 0;
            if (bodyRadius <= 0) yield break;
            string bodyName = body.Data.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.discoverables == null) yield break;

            for (int i = 0; i < data.discoverables.Count; i++)
            {
                CelestialBodyDiscoverablePosition d = data.discoverables[i];
                if (d == null || d.Radius <= bodyRadius) continue;
                string label = string.IsNullOrEmpty(d.ScienceRegionId) ? $"#{i}" : d.ScienceRegionId;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Discoverable '{label}' on '{bodyName}' has Radius = {d.Radius:0} m, larger than the body radius ({bodyRadius:0} m). " +
                    $"Likely a typo. Set a sensible local radius in the Science Region inspector.");
            }
        }
    }
}
