using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Surface
{
    /// <summary>
    /// Errors when a solid-surface body's <c>PQSData.materialSettings.surfaceMaterial</c> is null.
    /// </summary>
    /// <remarks>
    /// PQSRenderer dereferences the surface material in its boot path. A null reference leaves the body invisible
    /// and floods the console with NREs.
    /// </remarks>
    public sealed class MissingSurfaceMaterialValidator : IPlanetValidator
    {
        /// <summary>
        /// Stable code identifying issues emitted by this validator.
        /// </summary>
        public const string Code = "MISSING_SURFACE_MAT";

        /// <inheritdoc />
        public BodyClassFlags AppliesTo => BodyClassFlags.SolidSurface;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            PQS pqs = BodyResolver.FindPqsIncludingAsset(body);
            if (pqs?.data == null) yield break;
            if (pqs.data.materialSettings != null && pqs.data.materialSettings.surfaceMaterial != null) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"PQSData for '{body.Core.data.bodyName}' has no surface material assigned. Assign one in the PQSData inspector or run the Create Celestial Body wizard.");
        }
    }
}
