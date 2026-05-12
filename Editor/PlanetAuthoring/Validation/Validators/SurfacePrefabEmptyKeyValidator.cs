using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags a <see cref="PrefabSpawner" /> on the body whose <c>prefabName</c> is empty.
    /// </summary>
    /// <remarks>
    /// An empty key means the runtime <c>SpawnPrefabs</c> short-circuits and nothing ever spawns,
    /// usually a leftover from a place-then-clear edit cycle. One issue per offending spawner.
    /// </remarks>
    public sealed class SurfacePrefabEmptyKeyValidator : IPlanetValidator
    {
        public const string Code = "SURFACE_PREFAB_EMPTY_KEY";

        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            if (pqs == null) yield break;
            var spawners = pqs.GetComponentsInChildren<PrefabSpawner>(true);
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            foreach (var spawner in spawners)
            {
                if (spawner == null || !string.IsNullOrEmpty(spawner.prefabName)) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Surface prefab spawner '{spawner.gameObject.name}' on '{bodyName}' has no prefab assigned. Open its inspector and drop a prefab into the Prefab field.");
            }
        }
    }
}
