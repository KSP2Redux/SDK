using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Landmark
{
    /// <summary>
    /// Flags a <see cref="PrefabSpawner" /> in the authoring scene whose hierarchy has no
    /// <see cref="PQS" /> in its parent chain.
    /// </summary>
    /// <remarks>
    /// PrefabSpawner's <c>Awake</c> dereferences <c>GetComponentInParent&lt;PQS&gt;().CoreCelestialBodyData.Data.radius</c>
    /// to compute its altitude threshold. Without a PQS ancestor that throws on entering preview
    /// or game start. Walks every spawner across every scene root because the Local prefab holding
    /// PQS is a sibling scene root of the body, not a child.
    /// </remarks>
    public sealed class SurfacePrefabNoPqsParentValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SURFACE_PREFAB_NO_PQS_PARENT";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var scene = body.gameObject.scene;
            if (!scene.IsValid()) yield break;
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var spawner in root.GetComponentsInChildren<PrefabSpawner>(true))
                {
                    if (spawner == null) continue;
                    if (spawner.GetComponentInParent<PQS>() != null) continue;
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        $"Surface prefab spawner '{spawner.gameObject.name}' on '{bodyName}' has no PQS in its parent chain. PrefabSpawner.Awake will throw on entering preview. Move the spawner under the PQS GameObject on the Local prefab.");
                }
            }
        }
    }
}
