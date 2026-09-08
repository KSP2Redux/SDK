using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Notes packages whose biome is not on any mask channel, so nothing bounds them geographically.
    /// </summary>
    /// <remarks>
    /// Informational, not a fault. Stock Kerbin's fifth package works exactly this way, gated by
    /// procedural texture rules alone, and that is the documented escape hatch once the four
    /// channels are spent.
    ///
    /// It still needs saying, because the data does not show it. An author who adds a fifth package
    /// expecting the mask to bound it gets a planet-wide scatter field instead, with nothing
    /// anywhere indicating why.
    /// </remarks>
    public sealed class ScatterBiomeUnmappedValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_BIOME_UNMAPPED";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null || !ScatterBiomeChannels.MaskingActive(system))
                yield break;

            foreach (VegetationPackagePro package in system.VegetationPackageProList)
            {
                if (package == null || package.BiomeType == BiomeType.Default)
                    continue;

                if (ScatterBiomeChannels.TryGetMaskChannel(system, package.BiomeType, out _))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"Package '{ScatterValidatorHelper.PackageLabel(package)}' targets biome {package.BiomeType}, which no mask channel "
                    + "maps to. It is gated by its items' surface rules alone and will spawn wherever those allow.");
            }
        }
    }
}
