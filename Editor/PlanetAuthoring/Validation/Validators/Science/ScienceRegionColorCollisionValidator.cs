using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags two or more MapId greater-or-equal-to-zero region rows whose colors fall within
    /// <see cref="ScienceRegionConstants.ColorCollisionTolerance" />.
    /// </summary>
    /// <remarks>
    /// The bake classifies pixels by nearest color, so visually-similar region colors collapse
    /// into one region in the baked map. Discoverable rows (MapId &lt; 0) are skipped because
    /// they don't contribute pixels to the bake.
    /// </remarks>
    public sealed class ScienceRegionColorCollisionValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SR_COLOR_COLLISION";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            string bodyName = body?.Data?.bodyName;
            if (string.IsNullOrEmpty(bodyName)) yield break;
            ScienceRegionData data = ScienceRegionAssetLocator.FindForBody(bodyName);
            ScienceRegionData.ExtendedScienceRegionDefinition[] defs = data?.information?.ScienceRegionDefinitions;
            if (defs == null || defs.Length < 2) yield break;

            float threshSq = ScienceRegionConstants.ColorCollisionTolerance * ScienceRegionConstants.ColorCollisionTolerance * 3f;
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i] == null || defs[i].MapId < 0) continue;
                for (int j = i + 1; j < defs.Length; j++)
                {
                    if (defs[j] == null || defs[j].MapId < 0) continue;
                    if (DistanceSquared(defs[i].RegionColor, defs[j].RegionColor) > threshSq) continue;
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Regions '{defs[i].Id}' and '{defs[j].Id}' on '{bodyName}' have colors within {ScienceRegionConstants.ColorCollisionTolerance:0.00} of each other. " +
                        $"Bake will merge their pixels into one.");
                }
            }
        }

        private static float DistanceSquared(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }
    }
}
