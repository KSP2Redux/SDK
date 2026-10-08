using System.Collections.Generic;
using KSP;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Warns when the body's prefab hierarchy in the authoring scene has unapplied overrides.
    /// </summary>
    /// <remarks>
    /// Decal placements and other authoring edits land on the scene-instance copy of the body's
    /// nested prefabs (Local/Simulation, Scaled). Without applying those overrides back to the
    /// prefab assets, the changes do not ship with the body. This validator walks the body's
    /// prefab instance roots and surfaces a warning when any of them carry pending overrides.
    /// </remarks>
    public sealed class UnappliedPrefabOverridesValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "UNAPPLIED_PREFAB_OVERRIDES";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null)
                yield break;
            // Only meaningful for live scene instances. Prefab-asset edits in isolation cannot have scene overrides.
            if (PrefabUtility.IsPartOfPrefabAsset(body))
                yield break;

            var instanceRoots = new List<GameObject>();
            CollectPrefabInstanceRoots(body.gameObject, instanceRoots);

            var pending = new List<string>();
            foreach (GameObject root in instanceRoots)
            {
                if (!PrefabUtility.HasPrefabInstanceAnyOverrides(root, includeDefaultOverrides: false))
                    continue;
                pending.Add(root.name);
            }
            if (pending.Count == 0)
                yield break;

            string list = string.Join(", ", pending);
            string message = $"The body's prefab hierarchy has unapplied overrides on: {list}. Open the prefab overrides dropdown on each and Apply All so authoring changes (e.g. placed decals) ship with the body.";
            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
        }

        private static void CollectPrefabInstanceRoots(GameObject go, List<GameObject> into)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(go))
                into.Add(go);
            foreach (Transform child in go.transform)
                CollectPrefabInstanceRoots(child.gameObject, into);
        }
    }
}
