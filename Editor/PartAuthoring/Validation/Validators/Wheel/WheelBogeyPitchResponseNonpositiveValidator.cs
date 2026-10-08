using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Errors when <c>pitchResponse &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_WheelBogey multiplies <c>pitchResponse * Time.deltaTime</c> when interpolating
    /// toward the target pitch. Zero pins the bogey at its starting angle forever.
    /// </remarks>
    public sealed class WheelBogeyPitchResponseNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when pitch response is non-positive.</summary>
        public const string Code = "WHEEL_BOGEY_PITCH_RESPONSE_NONPOSITIVE";

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
                if (bogey.pitchResponse > 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"pitchResponse is {bogey.pitchResponse:0.###}. Bogey never advances toward target pitch.");
            }
        }
    }
}
