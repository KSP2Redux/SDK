using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags two or more discoverables on the same body that share a ScienceRegionId.
    /// </summary>
    /// <remarks>
    /// Discoverables surface to the runtime through a MapId=-1 region row keyed by their
    /// ScienceRegionId. Duplicate Ids would collide on lookup. No auto-fix because the right
    /// rename is editorial.
    /// </remarks>
    public sealed class DiscoverableDuplicateIdValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "DISCOVERABLE_DUPLICATE_ID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            if (data?.discoverables == null) yield break;

            var seen = new HashSet<string>();
            var collisions = new HashSet<string>();
            foreach (CelestialBodyDiscoverablePosition d in data.discoverables)
            {
                string id = d?.ScienceRegionId;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (!seen.Add(id)) collisions.Add(id);
            }
            foreach (string id in collisions)
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Two or more discoverables on '{bodyName}' share ScienceRegionId '{id}'. Rename one in the Science Region inspector.");
        }
    }
}
