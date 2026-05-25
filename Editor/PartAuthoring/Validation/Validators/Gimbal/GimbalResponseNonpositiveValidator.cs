using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Gimbal
{
    /// <summary>
    /// Errors when <c>gimbalResponseSpeed &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_Gimbal multiplies <c>gimbalResponseSpeed * fixedDeltaTime</c> when interpolating
    /// toward the target angle. Zero or negative pins the gimbal at the starting position - input
    /// has no effect.
    /// </remarks>
    public sealed class GimbalResponseNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when response speed is non-positive.</summary>
        public const string Code = "GIMBAL_RESPONSE_NONPOSITIVE";

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
                if (module is not Data_Gimbal gimbal)
                {
                    continue;
                }
                if (gimbal.gimbalResponseSpeed > 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"gimbalResponseSpeed is {gimbal.gimbalResponseSpeed:0.###}. Must be > 0 or the gimbal never moves.");
            }
        }
    }
}
