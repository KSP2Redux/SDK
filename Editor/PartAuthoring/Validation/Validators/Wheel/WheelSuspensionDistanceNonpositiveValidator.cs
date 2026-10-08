using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Warns when <c>Data_WheelSuspension.suspensionDistance &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_WheelSuspension writes <c>suspensionDistance</c> straight to
    /// <c>WheelCollider.suspensionDistance</c>. Zero or negative stroke means the suspension
    /// never compresses and the vessel rides directly on the wheel collider with no damping.
    /// </remarks>
    public sealed class WheelSuspensionDistanceNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the stroke is non-positive.</summary>
        public const string Code = "WHEEL_SUSPENSION_DISTANCE_NONPOSITIVE";

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
                if (module is not Data_WheelSuspension suspension)
                {
                    continue;
                }
                if (suspension.suspensionDistance > 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Warning,
                    $"suspensionDistance is {suspension.suspensionDistance:0.###}. Suspension stroke is zero, the vessel rides directly on the collider.");
            }
        }
    }
}
