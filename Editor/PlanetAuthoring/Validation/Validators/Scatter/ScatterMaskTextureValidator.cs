using System.Collections.Generic;
using AwesomeTechnologies.VegetationSystem;
using KSP;
using Ksp2UnityTools.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Scatter
{
    /// <summary>
    /// Checks the biome mask texture is imported so the tooling can sample it.
    /// </summary>
    /// <remarks>
    /// Silent when it fires: an unreadable mask cannot be sampled by the overlay or any coverage
    /// check. The GPU spawn path is unaffected, since it reads the mask as a texture regardless, so
    /// this is about the tooling being able to report what the mask says.
    /// </remarks>
    public sealed class ScatterMaskTextureValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying an unreadable mask.</summary>
        public const string NotReadableCode = "SCATTER_MASK_NOT_READABLE";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            VegetationSystemPro system = ScatterValidatorHelper.FindSystem(body);
            Texture2D mask = system == null ? null : system.BiomeTextureMask;
            if (mask == null)
                yield break;

            if (mask.isReadable)
                yield break;

            Texture2D captured = mask;
            var fixes = new[]
            {
                new ValidationFix("Enable Read/Write on importer", () => EnableReadWrite(captured)),
            };

            yield return new ValidationIssue(
                NotReadableCode,
                ValidationSeverity.Warning,
                $"Biome mask '{mask.name}' has Read/Write disabled, so the scatter tools cannot sample it "
                + "to tell you which package owns which ground.",
                fixes);
        }

        private static void EnableReadWrite(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path))
                return;

            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                return;

            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
