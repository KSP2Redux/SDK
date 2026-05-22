using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Errors when <c>minPitch &gt;= maxPitch</c>.
    /// </summary>
    /// <remarks>
    /// Module_WheelBogey's per-frame pitch update calls <c>Mathf.Clamp(angle, minPitch, maxPitch)</c>.
    /// An inverted or collapsed range degenerates the clamp to a single angle - the bogey cannot
    /// rotate at all.
    /// </remarks>
    public sealed class WheelBogeyPitchRangeInvertedValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the pitch range is inverted.</summary>
        public const string Code = "WHEEL_BOGEY_PITCH_RANGE_INVERTED";

        /// <inheritdoc />
        public IEnumerable<ValidationIssue> Validate(PartValidationContext context)
        {
            var modules = context?.Modules;
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
                if (bogey.minPitch < bogey.maxPitch)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"minPitch ({bogey.minPitch:0.##}°) >= maxPitch ({bogey.maxPitch:0.##}°). Bogey has no valid pitch range.");
            }
        }
    }
}
