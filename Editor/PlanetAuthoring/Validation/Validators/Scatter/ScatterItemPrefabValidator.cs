using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Errors when a mesh item has no prefab to spawn.
    /// </summary>
    /// <remarks>
    /// Keys on <c>PrefabType</c>, so only mesh items are checked. A texture item generates its
    /// geometry instead of instantiating a prefab, so a null prefab reference there is correct and
    /// normal. Stock's <c>grassland_grass</c> is one such item.
    /// </remarks>
    public sealed class ScatterItemPrefabValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_NO_PREFAB";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (item.PrefabType != VegetationPrefabType.Mesh || item.VegetationPrefab != null)
                    continue;

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' is a mesh item with no prefab, "
                    + "so it spawns nothing.");
            }
        }
    }
}
