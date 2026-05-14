using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Landmark
{
    /// <summary>
    /// Flags a <see cref="PrefabSpawner" /> whose <c>prefabName</c> does not match any addressable
    /// asset entry.
    /// </summary>
    /// <remarks>
    /// The spawner stores a string key into addressables. If the key never resolves, the runtime
    /// load silently fails (assets log an error then return default) and nothing visible spawns at
    /// runtime. Catches typos, deleted prefabs, and key drift after rename. Lookup is cached, so
    /// the cost stays cheap even with many spawners.
    /// </remarks>
    public sealed class SurfacePrefabUnresolvedAddressableValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SURFACE_PREFAB_UNRESOLVED_ADDRESSABLE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs == null) yield break;
            var spawners = pqs.GetComponentsInChildren<PrefabSpawner>(true);
            var bodyName = body.Data?.bodyName ?? "(unnamed)";
            foreach (var spawner in spawners)
            {
                if (spawner == null || string.IsNullOrEmpty(spawner.prefabName)) continue;
                var prefab = AddressableKeyLookup.GetPrefab(spawner.prefabName);
                if (prefab != null) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Surface prefab spawner '{spawner.gameObject.name}' on '{bodyName}' references addressable key '{spawner.prefabName}' which no entry resolves to. The runtime load will fail and nothing will spawn.");
            }
        }
    }
}
