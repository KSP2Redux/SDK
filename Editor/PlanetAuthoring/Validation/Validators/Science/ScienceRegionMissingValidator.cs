using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags solid-surface bodies that have no <see cref="ScienceRegionData" /> asset.
    /// </summary>
    /// <remarks>
    /// Without science region data, gameplay's surface-situation lookups fall through to an empty
    /// set, so no surface biome scalars resolve and no discoverables surface. Gas giants and other
    /// non-solid bodies are skipped because they legitimately have no surface to author regions for.
    /// </remarks>
    public sealed class ScienceRegionMissingValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "MISSING_SCIENCE_REGIONS";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            // Solid surface gate via the PQS reference. Non-solid bodies (stars, gas giants) don't
            // ship a PQS, so only bodies that have one but no science region asset are flagged.
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            string bodyName = body.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            if (ScienceRegionAssetLocator.FindForBody(bodyName) != null) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body '{bodyName}' has a PQS but no ScienceRegionData. Create one via Assets > KSP2 Unity Tools > Planet Authoring > Science Region Data, " +
                $"then name it so its BodyName matches '{bodyName}'.");
        }
    }
}
