using System.Collections.Generic;
using KSP.Modules;
using Ksp2UnityTools.Editor.Validation;

namespace Ksp2UnityTools.Editor.PartAuthoring.Validation.Validators.ControlSurface
{
    /// <summary>
    /// Errors when <c>ActuatorSpeedNormalScale &lt;= 0</c>.
    /// </summary>
    /// <remarks>
    /// Module_ControlSurface multiplies <c>ActuatorSpeedNormalScale</c> by deltaTime on both
    /// interpolation branches. Zero pins the surface at the starting angle forever.
    /// </remarks>
    public sealed class ControlSurfaceActuatorSpeedNonpositiveValidator : IPartValidator
    {
        /// <summary>Stable code emitted when actuator speed is non-positive.</summary>
        public const string Code = "CTRL_SURFACE_ACTUATOR_SPEED_NONPOSITIVE";

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
                if (module is not Data_ControlSurface ctrl)
                {
                    continue;
                }
                if (ctrl.ActuatorSpeedNormalScale > 0f)
                {
                    continue;
                }
                yield return new ValidationIssue(
                    Code,
                    ValidationSeverity.Error,
                    $"ActuatorSpeedNormalScale is {ctrl.ActuatorSpeedNormalScale:0.###}. Surface never moves.");
            }
        }
    }
}
