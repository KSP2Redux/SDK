using System.Collections.Generic;
using KSP;
using KSP.Game.Science;
using Ksp2UnityTools.Editor.PlanetAuthoring.Science;
using Ksp2UnityTools.Editor.ScriptableObjects;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Science
{
    /// <summary>
    /// Flags region rows whose colors sit close enough together that compression noise can move the
    /// boundary between them.
    /// </summary>
    /// <remarks>
    /// **Nothing merges, contrary to what this validator used to say.**
    /// <c>ScienceRegionData.ConvertToIndex</c> is a plain nearest-color search with no tolerance, so
    /// two close colors each keep every pixel nearest to them and both regions survive at full
    /// extent. What degrades is the seam: the decision boundary sits midway between the two colors,
    /// so a pixel needs only half their separation in color error to land in the wrong region. Block
    /// compression supplies error of that order, which shows up as speckle along the border and as
    /// assignments that shift when the source texture is reimported at a different setting.
    ///
    /// The threshold is compared as a straight distance. An earlier version squared the tolerance and
    /// multiplied by three, which tests a sphere of radius <c>tolerance * sqrt(3)</c> while the
    /// message still quoted the bare tolerance, so it fired on pairs nearly twice as far apart as it
    /// claimed and named a number that was not the one being tested.
    ///
    /// Only rows with a map id are considered. Note this is a scoping choice and not, as previously
    /// claimed, because discoverable rows cannot contribute pixels: <c>ConvertToIndex</c> iterates
    /// every definition with no map-id filter, and <c>(byte)</c> of a negative id wraps. That is a
    /// bake bug rather than a validation one and is not addressed here.
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

            float tolerance = ScienceRegionConstants.ColorCollisionTolerance;
            float toleranceSquared = tolerance * tolerance;
            for (int i = 0; i < defs.Length; i++)
            {
                if (defs[i] == null || defs[i].MapId < 0) continue;
                for (int j = i + 1; j < defs.Length; j++)
                {
                    if (defs[j] == null || defs[j].MapId < 0) continue;
                    float distanceSquared = DistanceSquared(defs[i].RegionColor, defs[j].RegionColor);
                    if (distanceSquared > toleranceSquared) continue;

                    // Halved because the boundary between two colors sits midway, so that is the error
                    // budget a pixel actually has before it is classified as the wrong region.
                    float margin = Mathf.Sqrt(distanceSquared) * 0.5f;
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Warning,
                        $"Regions '{defs[i].Id}' and '{defs[j].Id}' on '{bodyName}' are only {Mathf.Sqrt(distanceSquared):0.###} apart in color. " +
                        $"A pixel needs just {margin:0.###} of error to be classified as the wrong one, which block compression can supply, " +
                        $"so the border between them will speckle and may shift when the map is reimported.");
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
