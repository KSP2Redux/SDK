using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a body's scatter system is left suspended.
    /// </summary>
    /// <remarks>
    /// <c>_suspendVegetationSystem</c> is a serialized public bool, and <c>SetDefaultSettings</c>
    /// cannot correct it without a GameManager. A stale <c>true</c> saved onto the prefab therefore
    /// means a silently dark preview and a silently dark game, with nothing to distinguish it from a
    /// body that has no scatter authored.
    /// </remarks>
    public sealed class ScatterSuspendedValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_SUSPENDED";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || !system._suspendVegetationSystem)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Resume the scatter system", () => Resume(system)),
            };

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                "Scatter system is suspended, so it renders nothing in preview or in game.",
                fixes);
        }

        private static void Resume(VegetationSystemPro system)
        {
            if (system == null)
                return;

            Undo.RecordObject(system, "Resume Scatter System");
            system._suspendVegetationSystem = false;
            EditorUtility.SetDirty(system);
        }
    }
}
