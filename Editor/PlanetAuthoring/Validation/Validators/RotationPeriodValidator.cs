using System.Collections.Generic;
using KSP;

namespace Ksp2UnityTools.Editor.PlanetAuthoring.Validation.Validators
{
    /// <summary>
    /// Warns when a rotating body has a zero rotation period and is not tidally locked.
    /// </summary>
    /// <remarks>
    /// Tidally locked bodies have their period overwritten at runtime to match the orbital period, so a zero is fine in that case. Otherwise zero means the body never rotates despite Is Rotating being on.
    /// </remarks>
    public sealed class RotationPeriodValidator : IPlanetValidator
    {
        /// <summary>Stable code identifying issues emitted by this validator.</summary>
        public const string Code = "ROTATION_INVALID";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(CoreCelestialBodyData body)
        {
            if (body?.Core?.data == null)
                yield break;
            var data = body.Core.data;
            if (!data.isRotating || data.isTidallyLocked || data.rotationPeriod != 0)
                yield break;

            yield return new ValidationIssue(
                Code,
                ValidationSeverity.Warning,
                "Rotation Period is 0 and Is Tidally Locked is off. The body will not rotate.");
        }
    }
}
