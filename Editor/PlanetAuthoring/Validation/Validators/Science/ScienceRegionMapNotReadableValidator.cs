using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags a body's Science Region map texture when assigned but Read/Write is disabled.
    /// </summary>
    /// <remarks>
    /// The bake walks the source texture via GetPixels. With Read/Write off the call returns an
    /// empty array and the bake produces a uniform map. One-click fix toggles the importer flag.
    /// </remarks>
    public sealed class ScienceRegionMapNotReadableValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SCIENCE_REGION_MAP_NOT_READABLE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            Texture2D map = data?.scienceRegionMap;
            if (map == null || map.isReadable) yield break;

            Texture2D captured = map;
            var fixes = new[]
            {
                new ValidationFix("Enable Read/Write on importer", () => EnableReadWrite(captured)),
            };
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Science region map '{map.name}' on '{bodyName}' has Read/Write disabled. " +
                $"The bake will produce a uniform map until R/W is enabled.",
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
