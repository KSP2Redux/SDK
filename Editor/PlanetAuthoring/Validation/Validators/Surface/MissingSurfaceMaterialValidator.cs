using System.Collections.Generic;
using KSP;
using KSP.Rendering.Planets;
using Ksp2UnityTools.Editor.PlanetAuthoring.Tools;
using Ksp2UnityTools.Editor.Validation;

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
            PQSData pqsData = PlanetAuthoringSession.AuthoredData(BodyResolver.FindPqsIncludingAsset(body));
            if (pqsData == null) yield break;
            if (pqsData.materialSettings != null && pqsData.materialSettings.surfaceMaterial != null) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"PQSData for '{body.Core.data.bodyName}' has no surface material assigned. Assign one in the PQSData inspector or run the Create Celestial Body wizard.");
        }
    }
}
