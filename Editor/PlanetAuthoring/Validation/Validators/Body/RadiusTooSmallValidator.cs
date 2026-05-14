using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators.Body
{
    /// <summary>
    /// Errors when <c>CelestialBodyData.radius</c> is below the 100 m floor.
    /// </summary>
    /// <remarks>
    /// PhysX defaults and PQS subdivision math assume body radii in the tens of kilometers and up. Sub-100m
    /// radii break collider construction, terrain quad subdivision, and the patched-conic solver. Any value
    /// below 100 m is almost certainly a left-over wizard default.
    /// </remarks>
    public sealed class RadiusTooSmallValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "RADIUS_TOO_SMALL";

        private const double MinimumRadius = 100.0;

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null) yield break;
            double radius = body.Core.data.radius;
            if (radius >= MinimumRadius) yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Error,
                $"Body '{body.Core.data.bodyName}' has radius {radius:0.#} m, below the 100 m floor. Increase the radius before testing.");
        }
    }
}
