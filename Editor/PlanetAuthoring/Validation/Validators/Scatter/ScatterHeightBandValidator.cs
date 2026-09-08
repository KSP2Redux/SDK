using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when an item's height band lies entirely outside the body's terrain.
    /// </summary>
    /// <remarks>
    /// The band defaults to 0 to 1500 metres, tuned for a body the size of Kerbin. On a body whose
    /// terrain sits entirely above or below that, an item with the height rule enabled asks for
    /// ground that does not exist and spawns nothing, while the band still reads as a perfectly
    /// ordinary pair of numbers.
    ///
    /// Reads the body's stored terrain range, and stays silent when there is none. Both numbers are
    /// relative to sea level, which is the frame the height rule uses.
    /// </remarks>
    public sealed class ScatterHeightBandValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_HEIGHT_BAND";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Data == null)
                yield break;

            var terrainMinimum = (float)body.Data.MinTerrainHeight;
            var terrainMaximum = (float)body.Data.MaxTerrainHeight;
            if (terrainMaximum <= terrainMinimum)
                yield break;

            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!item.UseHeightRule ||
                    ScatterCountMath.IsImpossibleWindow(item.MinHeight, item.MaxHeight) ||
                    ScatterCountMath.WindowsOverlap(item.MinHeight, item.MaxHeight, terrainMinimum, terrainMaximum))
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' is limited to heights "
                    + $"{item.MinHeight:0.#} to {item.MaxHeight:0.#} m, but this body's terrain only spans "
                    + $"{terrainMinimum:0.#} to {terrainMaximum:0.#} m. Nothing can spawn.");
            }
        }
    }
}
