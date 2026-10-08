using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when an assigned package holds no items.
    /// </summary>
    /// <remarks>
    /// An assigned but empty package looks like a configured body from every angle in the inspector,
    /// so the emptiness is the one thing worth stating out loud.
    /// </remarks>
    public sealed class ScatterEmptyPackageValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_EMPTY_PACKAGE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null)
                yield break;

            foreach (VegetationPackagePro package in system.VegetationPackageProList)
            {
                if (package != null && (package.VegetationInfoList == null || package.VegetationInfoList.Count == 0))
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Package '{ScatterValidatorHelper.PackageLabel(package)}' has no items, so it contributes nothing.");
                }
            }
        }
    }
}
