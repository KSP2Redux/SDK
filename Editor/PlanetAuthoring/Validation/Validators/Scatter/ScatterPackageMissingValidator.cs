using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when a body's scatter system has no usable vegetation package.
    /// </summary>
    /// <remarks>
    /// A system standing ready with no content yet is a normal intermediate state while authoring,
    /// which is why this is a warning. It still needs saying, since an otherwise correctly
    /// configured body renders nothing without a package.
    /// </remarks>
    public sealed class ScatterPackageMissingValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCATTER_NO_PACKAGE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            if (system == null)
                yield break;

            if (system.VegetationPackageProList.Count == 0)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    "Scatter system has no vegetation package, so there is nothing to spawn.");
                yield break;
            }

            for (int i = 0; i < system.VegetationPackageProList.Count; i++)
            {
                if (system.VegetationPackageProList[i] == null)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Scatter vegetation package slot {i} is empty.");
                }
            }
        }
    }
}
