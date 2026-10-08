using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using Uber.Scatter;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a body's scatter system has no terrain registered.
    /// </summary>
    /// <remarks>
    /// Registration is the load-bearing step of scatter setup and its absence is doubly silent. The
    /// spawner has no surface to sample, and the polar sphere radius, transform and max height are
    /// all derived from the registered terrain by <c>SetupPolarSphereInfo</c>, so they resolve to
    /// zero and <c>null</c> rather than to the body.
    /// </remarks>
    public sealed class ScatterTerrainRegistrationValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_NO_TERRAIN";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || system.VegetationStudioTerrainObjectList.Count > 0)
                yield break;

            PQS pqs = BodyResolver.FindPqs(body);
            PqsTerrain terrain = ScatterSystemLocator.FindTerrain(pqs);

            var fixes = terrain != null
                ? new[] { new ValidationFix("Register the PQS terrain", () => Register(system, terrain)) }
                : null;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                terrain != null
                    ? "Scatter system has no terrain registered, so nothing spawns and the polar radius stays at zero."
                    : "Scatter system has no terrain registered, and no PqsTerrain exists on this body to register.",
                fixes);
        }

        private static void Register(VegetationSystemPro system, PqsTerrain terrain)
        {
            if (system == null || terrain == null)
                return;

            Undo.RecordObject(system, "Register Scatter Terrain");
            system.AddTerrain(terrain.gameObject);
            EditorUtility.SetDirty(system);
        }
    }
}
