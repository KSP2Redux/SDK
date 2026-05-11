using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags a body whose ScienceRegionData fingerprint has changed since the last bake.
    /// </summary>
    /// <remarks>
    /// The bake captures a hash of the source map + region rows on its way out. This validator
    /// recomputes it from the current state and compares. Mismatch means the artist edited an
    /// input (added a region, recolored one, replaced the source texture, retoggled R/W on the
    /// importer) without re-running the bake, so the JSON / baked map shipped with the body is
    /// out of date.
    /// </remarks>
    public sealed class ScienceRegionBakeDriftValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SR_BAKED_DRIFT";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data == null) yield break;

            ScienceRegionAuthoring sidecar = PlanetAuthoringRegistry.Instance.FindScienceRegion(data);
            // Empty string means "never baked", which is a separate concern (the missing-bake
            // condition surfaces via other paths). Only flag when a stored fingerprint exists
            // and the recomputed one differs.
            if (sidecar == null || string.IsNullOrEmpty(sidecar.LastBakeFingerprint)) yield break;

            string current = ScienceRegionBaker.ComputeFingerprint(data);
            if (current == sidecar.LastBakeFingerprint) yield break;

            ScienceRegionData captured = data;
            var fixes = new[]
            {
                new ValidationFix("Re-bake science regions", () => ScienceRegionBaker.Bake(captured)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Science regions for '{bodyName}' have changed since the last bake. " +
                $"The shipped JSON and baked map are out of date.",
                fixes);
        }
    }
}
