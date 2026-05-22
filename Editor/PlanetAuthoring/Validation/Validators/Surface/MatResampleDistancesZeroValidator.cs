using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using UnityEditor;
using UnityEngine;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Flags the wizard-default <c>_DistanceResampleDistances</c> = <c>(0,0,0,0)</c> (Error) and
    /// <c>_DistanceResampleUVScales</c> = <c>(1,1,1,1)</c> (Warning) on the surface material.
    /// </summary>
    /// <remarks>
    /// <c>ComputeResampleOpacity</c> in CelestialBody_Local divides by the distances. An all-zero vector yields
    /// NaN that the GPU discards, leaving the body invisible without console errors. UvScales at all-1 means
    /// every cascade tier samples at the same UV density. The cascade is functional but visually wrong, with no
    /// detail step-up at closer ranges. Stamps the documented PARAMS.md defaults: distances
    /// <c>(50, 500, 2000, 12000)</c>, uv scales <c>(1, 2, 4, 8)</c>.
    /// </remarks>
    public sealed class MatResampleDistancesZeroValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code emitted when the distances vector is all zero.
        /// </summary>
        public const string CodeDistancesZero = "MAT_RESAMPLE_DISTANCES_ZERO";

        /// <summary>
        /// Stable code emitted when the uv-scales vector is the all-1 wizard default.
        /// </summary>
        public const string CodeUvScalesUniform = "MAT_RESAMPLE_UVSCALES_UNIFORM";

        private static readonly int DistancesProp = Shader.PropertyToID("_DistanceResampleDistances");
        private static readonly int UvScalesProp = Shader.PropertyToID("_DistanceResampleUVScales");
        private static readonly Vector4 DefaultDistances = new(50f, 500f, 2000f, 12000f);
        private static readonly Vector4 DefaultUvScales = new(1f, 2f, 4f, 8f);
        private static readonly Vector4 UniformOnes = new(1f, 1f, 1f, 1f);

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            Material mat = pqs?.data?.materialSettings?.surfaceMaterial;
            if (mat == null) yield break;

            Material captured = mat;
            if (mat.HasVector(DistancesProp) && mat.GetVector(DistancesProp) == Vector4.zero)
            {
                yield return new ValidationIssue(
                    CodeDistancesZero,
                    ValidationSeverity.Error,
                    $"Surface material '{mat.name}' has _DistanceResampleDistances at (0,0,0,0). The body will render as fully discarded fragments. Stamp the documented defaults.",
                    new[] { new ValidationFix("Stamp default cascade", () => StampDefaults(captured)) });
            }

            if (mat.HasVector(UvScalesProp) && mat.GetVector(UvScalesProp) == UniformOnes)
            {
                yield return new ValidationIssue(
                    CodeUvScalesUniform,
                    ValidationSeverity.Warning,
                    $"Surface material '{mat.name}' has _DistanceResampleUVScales at (1,1,1,1). Every distance tier samples at the same density, so the cascade has no detail step-up at closer ranges.",
                    new[] { new ValidationFix("Stamp default cascade", () => StampDefaults(captured)) });
            }
        }

        private static void StampDefaults(Material mat)
        {
            Undo.RecordObject(mat, "Stamp resample defaults");
            if (mat.HasVector(DistancesProp))
                mat.SetVector(DistancesProp, DefaultDistances);
            if (mat.HasVector(UvScalesProp))
                mat.SetVector(UvScalesProp, DefaultUvScales);
            EditorUtility.SetDirty(mat);
        }
    }
}
