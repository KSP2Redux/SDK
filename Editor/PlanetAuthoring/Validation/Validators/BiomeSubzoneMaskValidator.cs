using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Flags solid bodies with subzones enabled but no readable subzone mask texture.
    /// </summary>
    /// <remarks>
    /// SUB_ZONES_ENABLED + biomeSubzoneMapping presumes a usable subzone mask. Missing or
    /// non-readable subzone mask makes the baker reject the bake (and would make the shader
    /// fall back to channel 0 for every pixel even if it didn't).
    /// </remarks>
    public sealed class BiomeSubzoneMaskValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "BIOME_SUBZONE_MASK_MISSING";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null) yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            PQSData pqsData = pqs?.data;
            if (pqsData?.heightMapInfo == null || !pqsData.heightMapInfo.subZonesEnabled) yield break;

            Texture2D subMask = pqsData.heightMapInfo.subZoneMask;
            string bodyName = body.Data?.bodyName ?? "(unnamed)";
            if (subMask == null)
            {
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Subzones are enabled on '{bodyName}' but the subzone mask texture is unassigned. " +
                    $"Set heightMapInfo.subZoneMask or disable subzones.");
                yield break;
            }
            if (!subMask.isReadable)
            {
                Texture2D captured = subMask;
                var fixes = new[]
                {
                    new ValidationFix("Enable Read/Write on importer", () => EnableReadWrite(captured)),
                };
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Subzone mask '{subMask.name}' on '{bodyName}' has Read/Write disabled. " +
                    $"The biome lookup baker needs CPU access to sample it.",
                    fixes);
            }
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
