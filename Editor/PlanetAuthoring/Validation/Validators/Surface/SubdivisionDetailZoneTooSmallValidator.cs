using System;
using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Warns when the max-detail subdivision zone radius around the camera is below 200 m.
    /// </summary>
    /// <remarks>
    /// The threshold formula scales linearly with body radius, so small bodies get a tiny
    /// fully-refined zone unless <see cref="PQSData.subdivisionMaxLevelOverride" /> is lowered.
    /// 200 m is the visual + physics floor: below it the player sees an obvious sharp ring of
    /// highest-detail quads around the camera (everything outside the ring is conspicuously
    /// coarser), and the abrupt mesh-density step also causes collision artifacts where vessels
    /// and kerbals straddle the boundary. The suggested fix computes the smallest max-level whose
    /// zone radius reaches the guideline.
    /// </remarks>
    public sealed class SubdivisionDetailZoneTooSmallValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "SUBDIVISION_DETAIL_ZONE_TOO_SMALL";

        private const double MinZoneRadius = 200.0;

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            double radius = body.Core.data.radius;
            if (radius <= 0) yield break;

            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs?.data == null) yield break;

            PQSGlobalSettings.SubdivData sd = ResolveSubdivData(pqs);
            if (sd == null) yield break;

            int currentMaxLevel = pqs.data.subdivisionMaxLevelOverride > 0
                ? Math.Clamp(pqs.data.subdivisionMaxLevelOverride, sd.minLevel + 1, sd.maxLevel)
                : sd.maxLevel;

            double scale = radius * sd.minDetailMultiplier * sd.subdivisionThreshold;
            double zoneRadius = scale / Math.Pow(2, currentMaxLevel - 1);

            if (zoneRadius >= MinZoneRadius) yield break;

            // Smallest max-level whose zone radius reaches the guideline:
            //   200 = scale / 2^(L-1)  ->  L = floor(1 + log2(scale / 200))
            int suggested = (int)Math.Floor(1.0 + Math.Log(scale / MinZoneRadius, 2));
            suggested = Math.Clamp(suggested, sd.minLevel + 1, sd.maxLevel);

            // No improvement possible if the suggestion isn't lower than what's already in effect.
            if (suggested >= currentMaxLevel) yield break;

            PQSData pqsData = pqs.data;
            int captured = suggested;
            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                $"Body '{body.Core.data.bodyName}' max-detail zone radius is {zoneRadius:0.#} m, below the 200 m guideline. Lower PQSData.subdivisionMaxLevelOverride to {suggested} to lift the zone to ~{scale / Math.Pow(2, suggested - 1):0.#} m. Trade-off: each level you drop doubles leaf-quad size.",
                new[] { new ValidationFix($"Set max level override to {suggested}", () => SetMaxLevelOverride(pqsData, captured)) });
        }

        private static PQSGlobalSettings.SubdivData ResolveSubdivData(PQS pqs)
        {
            // pqs.settings is runtime-assigned from Game.GraphicsManager. At edit time pull the
            // shipped asset via the same addressable EditorPqsBootstrap uses everywhere else.
            if (pqs.settings != null) return pqs.settings.subdivisionInfo.subdivData;
            return EditorPqsBootstrap.PQSGlobalSettings?.subdivisionInfo.subdivData;
        }

        private static void SetMaxLevelOverride(PQSData data, int value)
        {
            Undo.RecordObject(data, "Set subdivision max level override");
            data.subdivisionMaxLevelOverride = value;
            EditorUtility.SetDirty(data);
        }
    }
}
