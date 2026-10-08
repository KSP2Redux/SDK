using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.PlanetAuthoring.Scatter;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Warns when a scatter item has billboards turned on but no baked billboard to draw.
    /// </summary>
    /// <remarks>
    /// The billboard draw skips any item without a <c>BillboardCustomPrefab</c>, so the item is
    /// simply gone past its mesh distance. <c>UseBillboards</c> defaults to on, which makes this the
    /// state of every new tree or large rock until one is baked.
    /// </remarks>
    public class ScatterUnbakedBillboardValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_UNBAKED_BILLBOARD";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!IsUnbaked(item))
                    continue;

                string message = $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' has billboards on "
                    + "but no baked billboard, so it disappears where its mesh stops drawing.";

                // An item without a prefab is ScatterItemPrefabValidator's finding, and has nothing to bake.
                if (item.VegetationPrefab == null)
                {
                    yield return new ValidationIssue(Code, ValidationSeverity.Warning, message);
                    continue;
                }

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    message,
                    new[] { new ValidationFix("Bake billboard", () => Bake(system, package, item)) });
            }
        }

        /// <summary>
        /// Returns whether an item expects a billboard that has not been baked.
        /// </summary>
        /// <param name="item">The scatter item to check.</param>
        /// <returns>True if the item wants a billboard and has none, false otherwise.</returns>
        public static bool IsUnbaked(VegetationItemInfoPro item) =>
            item.UseBillboards && item.BillboardCustomPrefab == null && ScatterImpostorBaker.CanBillboard(item);

        private static void Bake(VegetationSystemPro system, VegetationPackagePro package, VegetationItemInfoPro item)
        {
            ScatterImpostorBaker.BakeItem(package, item);
            if (system != null)
                system.RefreshVegetationSystem();
        }
    }
}
