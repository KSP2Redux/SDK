using System.Collections.Generic;
using System.IO;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags MapId greater-or-equal-to-zero region rows that contributed zero pixels to the
    /// current baked map.
    /// </summary>
    /// <remarks>
    /// Defined-but-empty regions usually mean the artist added a row, picked a color, then never
    /// painted the texture, or that color collision with another row swallowed every pixel. The
    /// region has no gameplay coverage, so any situation scalars on it are unreachable.
    /// </remarks>
    public sealed class ScienceRegionEmptyValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SR_EMPTY_REGION";

        /// <inheritdoc />
        public ValidatorCost Cost => ValidatorCost.Expensive;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs = data?.information?.ScienceRegionDefinitions;
            if (defs == null || defs.Length == 0) yield break;

            CelestialBodyBakedScienceRegionMap bakedMap = LoadBakedMap(data);
            if (bakedMap?.MapData == null || bakedMap.MapData.Length == 0) yield break;

            var seen = new HashSet<byte>();
            for (int i = 0; i < bakedMap.MapData.Length; i++)
                seen.Add(bakedMap.MapData[i]);

            foreach (ScienceRegionData.ExtendedScienceRegionDefinition d in defs)
            {
                if (d == null || d.MapId < 0) continue;
                if (d.MapId > byte.MaxValue) continue;
                if (seen.Contains((byte)d.MapId)) continue;
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"Region '{d.Id}' (MapId {d.MapId}) on '{bodyName}' has no pixels in the baked map. " +
                    $"The region is defined but unreachable at runtime.");
            }
        }

        private static CelestialBodyBakedScienceRegionMap LoadBakedMap(ScienceRegionData data)
        {
            string srPath = AssetDatabase.GetAssetPath(data);
            if (string.IsNullOrEmpty(srPath)) return null;
            string folder = Path.GetDirectoryName(srPath)?.Replace('\\', '/') ?? "Assets";
            string bodyKey = data.information?.BodyName?.ToLowerInvariant() ?? string.Empty;
            string bakedPath = $"{folder}/{bodyKey}{ScienceRegionBaker.BakedMapSuffix}.asset";
            return AssetDatabase.LoadAssetAtPath<CelestialBodyBakedScienceRegionMap>(bakedPath);
        }
    }
}
