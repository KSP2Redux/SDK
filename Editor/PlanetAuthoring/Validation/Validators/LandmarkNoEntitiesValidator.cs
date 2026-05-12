using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags a <see cref="SurfaceLandmark" /> with all three child toggles off.
    /// </summary>
    /// <remarks>
    /// A landmark with no enabled children is doing nothing at runtime, usually a leftover after
    /// the artist disabled all three toggles instead of deleting the landmark.
    /// </remarks>
    public sealed class LandmarkNoEntitiesValidator : IPlanetValidator
    {
        public const string Code = "LANDMARK_NO_ENTITIES";

        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            if (pqs == null) yield break;
            var landmarks = pqs.GetComponentsInChildren<SurfaceLandmark>(true);
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            foreach (var landmark in landmarks)
            {
                if (landmark == null) continue;
                if (landmark.EnableDecal || landmark.EnablePrefab || landmark.EnableDiscoverable) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Surface landmark '{landmark.gameObject.name}' on '{bodyName}' has all three child toggles off. Re-enable a child or delete the landmark.");
            }
        }
    }
}
