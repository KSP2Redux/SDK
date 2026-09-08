using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a body's scatter system is left on the default <c>Flat</c> terrain type.
    /// </summary>
    /// <remarks>
    /// <c>TerrainType</c> defaults to <c>Flat</c>, which is enum value zero, so a freshly added
    /// component looks configured. Polar cell construction is gated on the type being
    /// <c>PolarSphere</c>, so the cell list stays empty, nothing spawns anywhere on the body, and
    /// nothing is logged.
    /// </remarks>
    public sealed class ScatterTerrainTypeValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_TERRAIN_TYPE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || system.TerrainType == TerrainType.PolarSphere)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Set to PolarSphere", () => SetPolarSphere(system)),
            };

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Scatter TerrainType is {system.TerrainType}, so no vegetation cells are built and nothing spawns. It must be PolarSphere.",
                fixes);
        }

        private static void SetPolarSphere(VegetationSystemPro system)
        {
            if (system == null)
                return;

            Undo.RecordObject(system, "Set Scatter TerrainType");
            system.TerrainType = TerrainType.PolarSphere;
            EditorUtility.SetDirty(system);
        }
    }
}
