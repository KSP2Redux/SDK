using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns when both <c>CelestialBodyData.MinTerrainHeight</c> and <c>MaxTerrainHeight</c> are
    /// zero. Both being zero almost always means the artist hasn't sampled the heightmap yet, since
    /// any real planet has terrain that varies above and below sea level.
    /// </summary>
    /// <remarks>
    /// The runtime uses these values for camera and physics range calculations, so leaving them
    /// unset can produce visible LOD popping or culling artifacts. Provides a "Recalculate" fix
    /// that runs the same heightmap sweep as the inspector button.
    /// </remarks>
    public sealed class UnsetTerrainHeightRangeValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "UNSET_TERRAIN_HEIGHT_RANGE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body == null || body.Core?.data == null)
                yield break;
            var pqs = body.GetComponentInChildren<PQS>(true);
            if (pqs == null)
                yield break;
            if (body.Core.data.MinTerrainHeight != 0.0 || body.Core.data.MaxTerrainHeight != 0.0)
                yield break;

            string message = "MinTerrainHeight and MaxTerrainHeight are both 0. Sample the heightmap so camera and physics ranges have correct elevation bounds.";

            var fixes = new[]
            {
                new ValidationFix("Recalculate from heightmap", () => RecalculateRange(body, pqs)),
            };

            yield return new ValidationIssue(Code, ValidationSeverity.Warning, message, fixes);
        }

        private static void RecalculateRange(CoreCelestialBodyData body, PQS pqs)
        {
            var result = TerrainHeightRangeCalculator.Compute(pqs);
            if (!result.Success)
            {
                Debug.LogWarning($"[UnsetTerrainHeightRangeValidator] Recalculate failed: {result.FailureReason}");
                return;
            }
            Undo.RecordObject(body, "Recalculate terrain height range");
            body.Core.data.MinTerrainHeight = result.MinHeight;
            body.Core.data.MaxTerrainHeight = result.MaxHeight;
            EditorUtility.SetDirty(body);
        }
    }
}
