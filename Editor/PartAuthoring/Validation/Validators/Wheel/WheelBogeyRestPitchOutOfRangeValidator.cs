using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Warns when <c>restPitch</c> is outside <c>[minPitch, maxPitch]</c>.
    /// </summary>
    /// <remarks>
    /// Bogey instantiates at <c>restPitch</c> then clamps to the pitch range. An out-of-range
    /// rest pitch produces a visible snap on part load as the bogey jumps to the nearest valid
    /// angle.
    /// </remarks>
    public sealed class WheelBogeyRestPitchOutOfRangeValidator : IPartValidator
    {
        /// <summary>Stable code emitted when rest pitch lies outside the pitch range.</summary>
        public const string Code = "WHEEL_BOGEY_REST_PITCH_OUT_OF_RANGE";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.ModuleDatas;
            if (modules == null)
            {
                yield break;
            }
            foreach (var module in modules)
            {
                if (module is not Data_WheelBogey bogey)
                {
                    continue;
                }
                if (bogey.restPitch >= bogey.minPitch && bogey.restPitch <= bogey.maxPitch)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"restPitch ({bogey.restPitch:0.##}°) lies outside [{bogey.minPitch:0.##}°, {bogey.maxPitch:0.##}°]. The bogey snaps on part load.");
            }
        }
    }
}
