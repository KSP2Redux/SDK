using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags a <see cref="PrefabSpawner" /> on the body whose hierarchy has no <see cref="PQS" /> in
    /// its parent chain.
    /// </summary>
    /// <remarks>
    /// PrefabSpawner's <c>Awake</c> dereferences <c>GetComponentInParent&lt;PQS&gt;().CoreCelestialBodyData.Data.radius</c>
    /// to compute its altitude threshold. Without a PQS ancestor that throws on entering preview
    /// or game start. The validator walks every spawner in the body's hierarchy regardless of where
    /// it lives so misplaced spawners surface here.
    /// </remarks>
    public sealed class SurfacePrefabNoPqsParentValidator : IPlanetValidator
    {
        public const string Code = "SURFACE_PREFAB_NO_PQS_PARENT";

        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var spawners = body.GetComponentsInChildren<PrefabSpawner>(true);
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            foreach (var spawner in spawners)
            {
                if (spawner == null) continue;
                if (spawner.GetComponentInParent<PQS>() != null) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Surface prefab spawner '{spawner.gameObject.name}' on '{bodyName}' has no PQS in its parent chain. PrefabSpawner.Awake will throw on entering preview. Move the spawner under the PQS GameObject.");
            }
        }
    }
}
