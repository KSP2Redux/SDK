using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Errors when either <c>bogeyAxis</c> or <c>bogeyUpAxis</c> is the zero vector.
    /// </summary>
    /// <remarks>
    /// Module_WheelBogey uses <c>Quaternion.AngleAxis(angle, bogeyAxis)</c> for the per-frame
    /// rotation and reads <c>bogeyUpAxis</c> to anchor the bogey's orientation. A zero-length
    /// axis produces an undefined rotation - the bogey either does nothing or behaves erratically.
    /// </remarks>
    public sealed class WheelBogeyAxisZeroValidator : IPartValidator
    {
        private const float EPSILON_SQR = 1e-6f;

        /// <summary>Stable code emitted per zero axis.</summary>
        public const string Code = "WHEEL_BOGEY_AXIS_ZERO";

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
                if (bogey.bogeyAxis.sqrMagnitude < EPSILON_SQR)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        "bogeyAxis is the zero vector. Rotation cannot be defined - the bogey will not rotate.");
                }
                if (bogey.bogeyUpAxis.sqrMagnitude < EPSILON_SQR)
                {
                    yield return new ValidationIssue(
                        Code,
                        ValidationSeverity.Error,
                        "bogeyUpAxis is the zero vector. Orientation reference cannot be defined.");
                }
            }
        }
    }
}
