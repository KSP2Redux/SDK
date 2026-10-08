using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Authoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.ScriptableObjects;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Landmark
{
    /// <summary>
    /// Flags a <see cref="SurfaceLandmark" /> whose enabled child toggles point at missing
    /// managed references.
    /// </summary>
    /// <remarks>
    /// Catches three failure modes per landmark: decal toggle on but ManagedDecal null or
    /// destroyed, prefab toggle on but ManagedSpawner null or destroyed, discoverable toggle on
    /// but no entry with the landmark's region Id in the body's ScienceRegionData. Most often
    /// surfaces after a manual hierarchy edit (artist deleted the decal child but not the
    /// landmark) or a sidecar bake that wiped the discoverables list.
    /// </remarks>
    public sealed class LandmarkUnresolvedReferenceValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "LANDMARK_UNRESOLVED_REFERENCE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            var landmarks = pqs.GetComponentsInChildren<SurfaceLandmark>(true);
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            var data = string.IsNullOrEmpty(body.Data?.bodyName)
                ? null
                : ScienceRegionAssetLocator.FindForBody(body.Data.bodyName);

            foreach (var landmark in landmarks)
            {
                if (landmark == null) continue;
                var name = landmark.gameObject.name;

                if (landmark.EnableDecal && landmark.ManagedDecal == null)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Surface landmark '{name}' on '{bodyName}' has the decal toggle on but no managed decal. Toggle the decal off and on to recreate.");
                }
                if (landmark.EnablePrefab && landmark.ManagedSpawner == null)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Surface landmark '{name}' on '{bodyName}' has the prefab toggle on but no managed spawner. Toggle the prefab off and on to recreate.");
                }
                if (landmark.EnableDiscoverable)
                {
                    if (data == null)
                    {
                        yield return new ValidationIssue(
                            Code,
                            ValidationSeverity.Warning,
                            $"Surface landmark '{name}' on '{bodyName}' has the discoverable toggle on but the body has no ScienceRegionData asset. Create one to host discoverables.");
                    }
                    else if (!string.IsNullOrEmpty(landmark.DiscoverableRegionId) && !HasMatchingDiscoverable(data, landmark.DiscoverableRegionId))
                    {
                        yield return new ValidationIssue(
                            Code,
                            ValidationSeverity.Warning,
                            $"Surface landmark '{name}' on '{bodyName}' references discoverable region '{landmark.DiscoverableRegionId}' which has no entry in the body's ScienceRegionData. Toggle the discoverable off and on to recreate.");
                    }
                }
            }
        }

        private static bool HasMatchingDiscoverable(ScienceRegionData data, string regionId)
        {
            if (data.discoverables == null) return false;
            foreach (var d in data.discoverables)
            {
                if (d != null && string.Equals(d.ScienceRegionId, regionId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
