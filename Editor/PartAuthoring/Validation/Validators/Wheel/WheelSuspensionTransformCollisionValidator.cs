using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.Wheel
{
    /// <summary>
    /// Errors when <c>suspensionTransformName == suspensionColliderName</c>.
    /// </summary>
    /// <remarks>
    /// Module_WheelSuspension disables the collider on the named transform AND drives the same
    /// transform's local position every fixed step. If both names point at the same GameObject
    /// the wheel collider's own host is repositioned each frame, which conflicts with the
    /// collider's own kinematic suspension simulation.
    /// </remarks>
    public sealed class WheelSuspensionTransformCollisionValidator : IPartValidator
    {
        /// <summary>Stable code emitted when the two transform names collide.</summary>
        public const string Code = "WHEEL_SUSPENSION_TRANSFORM_COLLISION";

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
                if (string.IsNullOrEmpty(suspension.suspensionTransformName) ||
                    string.IsNullOrEmpty(suspension.suspensionColliderName))
                {
                    continue;
                }
                if (suspension.suspensionTransformName != suspension.suspensionColliderName)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"suspensionTransformName and suspensionColliderName both point at '{suspension.suspensionTransformName}'. The collider's host gets repositioned every frame.");
            }
        }
    }
}
