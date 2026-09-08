using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Notes items that set a rule flag the spawn path never reads.
    /// </summary>
    /// <remarks>
    /// <c>UsePqsTextureSampleRule</c> is declared on the item and read by nothing, so setting it
    /// yields no restriction and no error. Surface restriction goes through the procedural texture
    /// include and exclude rule lists, several inspector sections away.
    ///
    /// The inspector offers no control for the flag, so this fires only on hand-edited or imported
    /// data.
    /// </remarks>
    public sealed class ScatterDeadFieldValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "SCATTER_DEAD_FIELD";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            foreach ((VegetationPackagePro package, VegetationItemInfoPro item) in ScatterValidatorHelper.Items(body))
            {
                if (!item.UsePqsTextureSampleRule)
                    continue;

                var fixes = new[]
                {
                    new ValidationFix("Clear the dead flag", () => Clear(package, item)),
                };

                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Info,
                    $"Item '{item.Name}' in package '{ScatterValidatorHelper.PackageLabel(package)}' sets UsePqsTextureSampleRule, which "
                    + "nothing reads. Surface restriction goes through the include and exclude rule lists instead.",
                    fixes);
            }
        }

        private static void Clear(VegetationPackagePro package, VegetationItemInfoPro item)
        {
            if (package == null || item == null)
                return;

            Undo.RecordObject(package, "Clear Dead Scatter Flag");
            item.UsePqsTextureSampleRule = false;
            EditorUtility.SetDirty(package);
        }
    }
}
