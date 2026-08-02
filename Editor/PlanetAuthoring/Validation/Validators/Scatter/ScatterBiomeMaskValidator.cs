using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when biome masking is enabled on a scatter system with no mask texture assigned.
    /// </summary>
    /// <remarks>
    /// <c>UseBiomeTextureMask</c> defaults to true, so this fires on any scatter system whose mask
    /// was never assigned. Biome rules cannot resolve without the texture, which makes every
    /// biome-restricted item behave differently from what its rules describe.
    /// </remarks>
    public sealed class ScatterBiomeMaskValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_NO_BIOME_MASK";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || !system.UseBiomeTextureMask || system.BiomeTextureMask != null)
                yield break;

            var fixes = new[]
            {
                new ValidationFix("Turn biome masking off", () => Disable(system)),
            };

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                "Biome masking is on but no mask texture is assigned, so biome rules cannot resolve.",
                fixes);
        }

        private static void Disable(VegetationSystemPro system)
        {
            if (system == null)
                return;

            Undo.RecordObject(system, "Disable Scatter Biome Mask");
            system.UseBiomeTextureMask = false;
            EditorUtility.SetDirty(system);
        }
    }
}
