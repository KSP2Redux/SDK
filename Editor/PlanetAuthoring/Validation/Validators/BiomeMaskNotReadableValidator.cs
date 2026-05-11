using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags a <c>_BiomeMaskTex</c> that is assigned but has Read/Write disabled in the importer.
    /// </summary>
    /// <remarks>
    /// The biome lookup baker samples the mask via GetPixelBilinear, which only works on readable
    /// textures. Without R/W the baker quietly produces empty cells. One-click fix toggles the
    /// importer's Read/Write flag and reimports.
    /// </remarks>
    public sealed class BiomeMaskNotReadableValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "BIOME_MASK_NOT_READABLE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            Texture2D mask = pqs?.data?.heightMapInfo?.mask;
            if (mask == null || mask.isReadable) yield break;

            Texture2D captured = mask;
            var fixes = new[]
            {
                new ValidationFix("Enable Read/Write on importer", () => EnableReadWrite(captured)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Biome mask '{mask.name}' on '{body.Data?.bodyName ?? "(unnamed)"}' has Read/Write disabled. " +
                $"The biome lookup baker can't sample it. Bake output would be empty.",
                fixes);
        }

        private static void EnableReadWrite(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
    }
}
