using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Notes items switched off at the item level.
    /// </summary>
    /// <remarks>
    /// A switched-off item vanishes while every other setting still reads as correct, and the two
    /// toggles live in different inspector sections from each other. Reported at info severity,
    /// since a body mid-authoring trips this routinely.
    /// </remarks>
    public sealed class ScatterItemDisabledValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_ITEM_DISABLED";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (item.RenderItem && item.EnableRuntimeSpawn)
                    continue;

                string reason = (item.EnableRuntimeSpawn, item.RenderItem) switch
                {
                    (false, false) => "spawning and rendering are both off",
                    (false, true) => "runtime spawning is off",
                    _ => "rendering is off",
                };

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' is inactive because {reason}.");
            }
        }
    }
}
